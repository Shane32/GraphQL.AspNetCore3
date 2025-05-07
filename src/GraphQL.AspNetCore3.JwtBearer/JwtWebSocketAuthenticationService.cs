// Parts of this code file are based on the JwtBearerHandler class in the Microsoft.AspNetCore.Authentication.JwtBearer package found at:
//   https://github.com/dotnet/aspnetcore/blob/5493b413d1df3aaf00651bdf1cbd8135fa63f517/src/Security/Authentication/JwtBearer/src/JwtBearerHandler.cs
//
// Those sections of code may be subject to the MIT license found at:
//   https://github.com/dotnet/aspnetcore/blob/5493b413d1df3aaf00651bdf1cbd8135fa63f517/LICENSE.txt

using System.Security.Claims;
using GraphQL.AspNetCore3.WebSockets;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GraphQL.AspNetCore3.JwtBearer;

/// <summary>
/// Authenticates WebSocket connections via the 'payload' of the initialization packet.
/// This is necessary because WebSocket connections initiated from the browser cannot
/// authenticate via HTTP headers.
/// <br/><br/>
/// Notes:
/// <list type="bullet">
/// <item>This class is not used when authenticating over GET/POST.</item>
/// <item>
/// This class pulls the <see cref="JwtBearerOptions"/> instance registered by ASP.NET Core during the call to
/// <see cref="JwtBearerExtensions.AddJwtBearer(AuthenticationBuilder, Action{JwtBearerOptions})">AddJwtBearer</see>
/// for the default or configured authentication scheme and authenticates the token
/// based on simplified logic used by <see cref="JwtBearerHandler"/>.
/// </item>
/// <item>
/// The expected format of the payload is <c>{"Authorization":"Bearer TOKEN"}</c> where TOKEN is the JSON Web Token (JWT),
/// mirroring the format of the 'Authorization' HTTP header.
/// </item>
/// <item>
/// When JWT events are enabled via <see cref="JwtBearerAuthenticationOptions.EnableJwtEvents"/>, this implementation
/// will raise the <see cref="JwtBearerEvents.MessageReceived"/>, <see cref="JwtBearerEvents.TokenValidated"/>, 
/// and <see cref="JwtBearerEvents.AuthenticationFailed"/> events as appropriate.
/// </item>
/// <item>
/// Implementation does not call <see cref="Microsoft.Extensions.Logging.ILogger"/> to log authentication events.
/// </item>
/// </list>
/// </summary>
public class JwtWebSocketAuthenticationService : IWebSocketAuthenticationService
{
    private readonly IGraphQLSerializer _graphQLSerializer;
    private readonly IOptionsMonitor<JwtBearerOptions> _jwtBearerOptionsMonitor;
    private readonly string[] _defaultAuthenticationSchemes;
    private readonly JwtBearerAuthenticationOptions _jwtBearerAuthenticationOptions;
    private readonly IAuthenticationSchemeProvider _schemeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtWebSocketAuthenticationService"/> class.
    /// </summary>
    public JwtWebSocketAuthenticationService(
        IGraphQLSerializer graphQLSerializer,
        IOptionsMonitor<JwtBearerOptions> jwtBearerOptionsMonitor,
        IOptions<AuthenticationOptions> authenticationOptions,
        IOptions<JwtBearerAuthenticationOptions> jwtBearerAuthenticationOptions,
        IAuthenticationSchemeProvider schemeProvider)
    {
        _graphQLSerializer = graphQLSerializer;
        _jwtBearerOptionsMonitor = jwtBearerOptionsMonitor;
        var defaultAuthenticationScheme = authenticationOptions.Value.DefaultScheme;
        _defaultAuthenticationSchemes = defaultAuthenticationScheme != null ? [defaultAuthenticationScheme] : [];
        _jwtBearerAuthenticationOptions = jwtBearerAuthenticationOptions.Value;
        _schemeProvider = schemeProvider;
    }

    /// <inheritdoc/>
    public async Task AuthenticateAsync(AuthenticationRequest authenticationRequest)
    {
        var connection = authenticationRequest.Connection;
        var operationMessage = authenticationRequest.OperationMessage;
        var schemes = authenticationRequest.AuthenticationSchemes.Any() ? authenticationRequest.AuthenticationSchemes : _defaultAuthenticationSchemes;
        try {
            // for connections authenticated via HTTP headers, no need to reauthenticate
            if (connection.HttpContext.User.Identity?.IsAuthenticated ?? false)
                return;

            // attempt to read the 'Authorization' key from the payload object and verify it contains "Bearer XXXXXXXX"
            var authPayload = _graphQLSerializer.ReadNode<AuthPayload>(operationMessage.Payload);
            if (authPayload != null && authPayload.Authorization != null && authPayload.Authorization.StartsWith("Bearer ", StringComparison.Ordinal)) {
                // pull the token from the value
                var token = authPayload.Authorization.Substring(7);

                // try to authenticate with each of the configured authentication schemes
                foreach (var scheme in schemes) {
                    var options = _jwtBearerOptionsMonitor.Get(scheme);

                    // If JWT events are enabled, trigger the MessageReceived event
                    if (_jwtBearerAuthenticationOptions.EnableJwtEvents) {
                        var messageResult = await TriggerMessageReceivedEventAsync(connection.HttpContext, options, token, scheme).ConfigureAwait(false);
                        if (messageResult.Handled) {
                            if (messageResult.Success) {
                                connection.HttpContext.User = messageResult.Principal!;
                                return;
                            }
                            continue;
                        }

                        token = messageResult.Token;
                    }

                    // follow logic simplified from JwtBearerHandler.HandleAuthenticateAsync, as follows:
                    var tokenValidationParameters = await SetupTokenValidationParametersAsync(options, connection.HttpContext).ConfigureAwait(false);
#if NET8_0_OR_GREATER
                    if (!options.UseSecurityTokenValidators) {
                        foreach (var tokenHandler in options.TokenHandlers) {
                            try {
                                var tokenValidationResult = await tokenHandler.ValidateTokenAsync(token, tokenValidationParameters).ConfigureAwait(false);
                                if (tokenValidationResult.IsValid) {
                                    var principal = new ClaimsPrincipal(tokenValidationResult.ClaimsIdentity);
                                    
                                    // If JWT events are enabled, trigger the TokenValidated event
                                    if (_jwtBearerAuthenticationOptions.EnableJwtEvents)
                                    {
                                        var validatedResult = await TriggerTokenValidatedEventAsync(connection.HttpContext, options, principal, tokenValidationResult.SecurityToken, scheme).ConfigureAwait(false);
                                        if (validatedResult.Handled && !validatedResult.Success)
                                        {
                                            continue;
                                        }
                                        
                                        principal = validatedResult.Principal ?? principal;
                                    }
                                    
                                    // set the ClaimsPrincipal for the HttpContext; authentication will take place against this object
                                    connection.HttpContext.User = principal;
                                    return;
                                }
                            } catch (Exception ex) {
                                // If JWT events are enabled, trigger the AuthenticationFailed event
                                if (_jwtBearerAuthenticationOptions.EnableJwtEvents)
                                {
                                    var failedResult = await TriggerAuthenticationFailedEventAsync(connection.HttpContext, options, ex, scheme).ConfigureAwait(false);
                                    if (failedResult.Handled && failedResult.Success)
                                    {
                                        connection.HttpContext.User = failedResult.Principal!;
                                        return;
                                    }
                                }
                                
                                // no errors during authentication should throw an exception
                                // specifically, attempting to validate an invalid JWT token may result in an exception
                            }
                        }
                    } else {
#else
                    {
#endif
#pragma warning disable CS0618 // Type or member is obsolete
                        foreach (var validator in options.SecurityTokenValidators) {
                            if (validator.CanReadToken(token)) {
                                try {
                                    var principal = validator.ValidateToken(token, tokenValidationParameters, out var securityToken);

                                    // If JWT events are enabled, trigger the TokenValidated event
                                    if (_jwtBearerAuthenticationOptions.EnableJwtEvents) {
                                        var validatedResult = await TriggerTokenValidatedEventAsync(connection.HttpContext, options, principal, securityToken, scheme).ConfigureAwait(false);
                                        if (validatedResult.Handled && !validatedResult.Success) {
                                            continue;
                                        }

                                        principal = validatedResult.Principal ?? principal;
                                    }

                                    // set the ClaimsPrincipal for the HttpContext; authentication will take place against this object
                                    connection.HttpContext.User = principal;
                                    return;
                                } catch (Exception ex) {
                                    // If JWT events are enabled, trigger the AuthenticationFailed event
                                    if (_jwtBearerAuthenticationOptions.EnableJwtEvents) {
                                        var failedResult = await TriggerAuthenticationFailedEventAsync(connection.HttpContext, options, ex, scheme).ConfigureAwait(false);
                                        if (failedResult.Handled && failedResult.Success) {
                                            connection.HttpContext.User = failedResult.Principal!;
                                            return;
                                        }
                                    }

                                    // no errors during authentication should throw an exception
                                    // specifically, attempting to validate an invalid JWT token will result in an exception
                                }
                            }
                        }
#pragma warning restore CS0618 // Type or member is obsolete
                    }
                }
            }
        } catch {
            // no errors during authentication should throw an exception
            // specifically, parsing invalid JSON will result in an exception
        }
    }

    private static async ValueTask<TokenValidationParameters> SetupTokenValidationParametersAsync(JwtBearerOptions options, HttpContext httpContext)
    {
        // Clone to avoid cross request race conditions for updated configurations.
        var tokenValidationParameters = options.TokenValidationParameters.Clone();

#if NET8_0_OR_GREATER
        if (options.ConfigurationManager is BaseConfigurationManager baseConfigurationManager) {
            tokenValidationParameters.ConfigurationManager = baseConfigurationManager;
        } else {
#else
        {
#endif
            if (options.ConfigurationManager != null) {
                // GetConfigurationAsync has a time interval that must pass before new http request will be issued.
                var configuration = await options.ConfigurationManager.GetConfigurationAsync(httpContext.RequestAborted).ConfigureAwait(false);
                var issuers = new[] { configuration.Issuer };
                tokenValidationParameters.ValidIssuers = (tokenValidationParameters.ValidIssuers == null ? issuers : tokenValidationParameters.ValidIssuers.Concat(issuers));
                tokenValidationParameters.IssuerSigningKeys = (tokenValidationParameters.IssuerSigningKeys == null ? configuration.SigningKeys : tokenValidationParameters.IssuerSigningKeys.Concat(configuration.SigningKeys));
            }
        }

        return tokenValidationParameters;
    }

    private async Task<EventResult> TriggerMessageReceivedEventAsync(HttpContext httpContext, JwtBearerOptions options, string token, string schemeName)
    {
        var scheme = await _schemeProvider.GetSchemeAsync(schemeName)
            ?? throw new InvalidOperationException($"Authentication scheme '{schemeName}' not found.");

        var messageReceivedContext = new MessageReceivedContext(httpContext, scheme, options) {
            Token = token
        };

        if (options.Events != null && options.Events.MessageReceived != null) {
            await options.Events.MessageReceived(messageReceivedContext).ConfigureAwait(false);
        }

        var result = new EventResult { Token = messageReceivedContext.Token };

        // If the event provided a principal, use it directly
        if (messageReceivedContext.Result?.Succeeded == true) {
            result.Handled = true;
            result.Success = true;
            result.Principal = messageReceivedContext.Principal;
        }

        return result;
    }

    private async Task<EventResult> TriggerTokenValidatedEventAsync(HttpContext httpContext, JwtBearerOptions options, ClaimsPrincipal principal, SecurityToken securityToken, string schemeName)
    {
        var scheme = await _schemeProvider.GetSchemeAsync(schemeName)
            ?? throw new InvalidOperationException($"Authentication scheme '{schemeName}' not found.");

        var tokenValidatedContext = new TokenValidatedContext(httpContext, scheme, options) {
            Principal = principal,
            SecurityToken = securityToken
        };

        if (options.Events != null && options.Events.TokenValidated != null) {
            await options.Events.TokenValidated(tokenValidatedContext).ConfigureAwait(false);
        }

        var result = new EventResult();

        // If the event failed or replaced the principal
        if (tokenValidatedContext.Result != null) {
            result.Handled = true;
            result.Success = tokenValidatedContext.Result.Succeeded;
            if (tokenValidatedContext.Result.Succeeded) {
                result.Principal = tokenValidatedContext.Principal;
            }
        }

        return result;
    }

    private async Task<EventResult> TriggerAuthenticationFailedEventAsync(HttpContext httpContext, JwtBearerOptions options, Exception exception, string schemeName)
    {
        var scheme = await _schemeProvider.GetSchemeAsync(schemeName)
            ?? throw new InvalidOperationException($"Authentication scheme '{schemeName}' not found.");

        var authenticationFailedContext = new AuthenticationFailedContext(httpContext, scheme, options) {
            Exception = exception
        };

        if (options.Events != null && options.Events.AuthenticationFailed != null) {
            await options.Events.AuthenticationFailed(authenticationFailedContext).ConfigureAwait(false);
        }

        var result = new EventResult();

        // If the event handled the exception and succeeded
        if (authenticationFailedContext.Result != null) {
            result.Handled = true;
            result.Success = authenticationFailedContext.Result.Succeeded;
            if (authenticationFailedContext.Result.Succeeded) {
                result.Principal = authenticationFailedContext.Principal;
            }
        }

        return result;
    }

    private sealed class EventResult
    {
        public bool Handled { get; set; }
        public bool Success { get; set; }
        public ClaimsPrincipal? Principal { get; set; }
        public string Token { get; set; } = string.Empty;
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public sealed class AuthPayload
    {
        public string? Authorization { get; set; }
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
