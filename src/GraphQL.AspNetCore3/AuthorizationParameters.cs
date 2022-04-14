using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;

namespace GraphQL.AspNetCore3;

/// <summary>
/// Authorization parameters
/// </summary>
public struct AuthorizationParameters<T>
{
    /// <summary>
    /// Initializes an instance with a specified <see cref="Microsoft.AspNetCore.Http.HttpContext"/>
    /// and parameters copied from the specified instance of <see cref="GraphQLHttpMiddlewareOptions"/>.
    /// </summary>
    public AuthorizationParameters(
        HttpContext httpContext,
        GraphQLHttpMiddlewareOptions middlewareOptions,
        Func<T, Task>? onNotAuthenticated,
        Func<T, Task>? onNotAuthorizedRole,
        Func<T, AuthorizationResult, Task>? onNotAuthorizedPolicy)
    {
        HttpContext = httpContext;
        AuthorizationRequired = middlewareOptions.AuthorizationRequired;
        AuthorizedRoles = middlewareOptions.AuthorizedRoles;
        AuthorizedPolicy = middlewareOptions.AuthorizedPolicy;
        OnNotAuthenticated = onNotAuthenticated;
        OnNotAuthorizedRole = onNotAuthorizedRole;
        OnNotAuthorizedPolicy = onNotAuthorizedPolicy;
    }

    /// <summary>
    /// Gets or sets the <see cref="Microsoft.AspNetCore.Http.HttpContext"/> for the request.
    /// </summary>
    public HttpContext HttpContext { get; set; }

    /// <inheritdoc cref="GraphQLHttpMiddlewareOptions.AuthorizationRequired"/>
    public bool AuthorizationRequired { get; set; }

    /// <inheritdoc cref="GraphQLHttpMiddlewareOptions.AuthorizedRoles"/>
    public List<string>? AuthorizedRoles { get; set; }

    /// <inheritdoc cref="GraphQLHttpMiddlewareOptions.AuthorizedPolicy"/>
    public string? AuthorizedPolicy { get; set; }

    /// <summary>
    /// A delegate which executes if <see cref="AuthorizationRequired"/> is set
    /// but <see cref="IIdentity.IsAuthenticated"/> returns <see langword="false"/>.
    /// </summary>
    public Func<T, Task>? OnNotAuthenticated { get; set; }

    /// <summary>
    /// A delegate which executes if <see cref="AuthorizedRoles"/> is set but
    /// <see cref="ClaimsPrincipal.IsInRole(string)"/> returns <see langword="false"/>
    /// for all roles.
    /// </summary>
    public Func<T, Task>? OnNotAuthorizedRole { get; set; }

    /// <summary>
    /// A delegate which executes if <see cref="AuthorizedPolicy"/> is set but
    /// <see cref="IAuthorizationService.AuthorizeAsync(ClaimsPrincipal, object, string)"/>
    /// returns an unsuccessful <see cref="AuthorizationResult"/> for the specified policy.
    /// </summary>
    public Func<T, AuthorizationResult, Task>? OnNotAuthorizedPolicy { get; set; }
}
