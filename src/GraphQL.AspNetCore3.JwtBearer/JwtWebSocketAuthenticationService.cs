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
/// Events configured in <see cref="JwtBearerOptions.Events"/> are not raised by this implementation.
/// </item>
/// </list>
/// </summary>
public class JwtWebSocketAuthenticationService : IWebSocketAuthenticationService
{
    private readonly IGraphQLSerializer _graphQLSerializer;
    private readonly IOptionsMonitor<JwtBearerOptions> _jwtBearerOptionsMonitor;
    private readonly string[] _defaultAuthenticationSchemes;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtWebSocketAuthenticationService"/> class.
    /// </summary>
    public JwtWebSocketAuthenticationService(IGraphQLSerializer graphQLSerializer, IOptionsMonitor<JwtBearerOptions> jwtBearerOptionsMonitor, IOptions<AuthenticationOptions> authenticationOptions)
    {
        _graphQLSerializer = graphQLSerializer;
        _jwtBearerOptionsMonitor = jwtBearerOptionsMonitor;
        var defaultAuthenticationScheme = authenticationOptions.Value.DefaultAuthenticateScheme;
        _defaultAuthenticationSchemes = defaultAuthenticationScheme != null ? [defaultAuthenticationScheme] : [];
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

                    // follow logic simplified from JwtBearerHandler.HandleAuthenticateAsync, as follows:
                    var tokenValidationParameters = await SetupTokenValidationParametersAsync(options, connection.HttpContext).ConfigureAwait(false);
#if NET8_0_OR_GREATER
                    if (!options.UseSecurityTokenValidators) {
                        foreach (var tokenHandler in options.TokenHandlers) {
                            try {
                                var tokenValidationResult = await tokenHandler.ValidateTokenAsync(token, tokenValidationParameters).ConfigureAwait(false);
                                if (tokenValidationResult.IsValid) {
                                    var principal = new ClaimsPrincipal(tokenValidationResult.ClaimsIdentity);
                                    // set the ClaimsPrincipal for the HttpContext; authentication will take place against this object
                                    connection.HttpContext.User = principal;
                                    return;
                                }
                            } catch {
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
                                    var principal = validator.ValidateToken(token, tokenValidationParameters, out _);
                                    // set the ClaimsPrincipal for the HttpContext; authentication will take place against this object
                                    connection.HttpContext.User = principal;
                                    return;
                                } catch {
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

    private sealed class AuthPayload
    {
        public string? Authorization { get; set; }
    }
}
