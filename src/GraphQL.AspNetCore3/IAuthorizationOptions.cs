using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;

namespace GraphQL.AspNetCore3;

/// <summary>
/// Provides authorization options.
/// </summary>
public interface IAuthorizationOptions
{
    /// <summary>
    /// Gets a list of the authentication schemes the authentication requirements are evaluated against.
    /// When no schemes are specified, the default authentication scheme is used.
    /// </summary>
    IEnumerable<string> AuthenticationSchemes { get; }

    /// <summary>
    /// If set, requires that <see cref="IIdentity.IsAuthenticated"/> return <see langword="true"/>
    /// for the user within <see cref="HttpContext.User"/>
    /// prior to executing the GraphQL request or accepting the WebSocket connection.
    /// Technically this property should be named as AuthenticationRequired but for
    /// ASP.NET Core / GraphQL.NET naming and design decisions it was called so.
    /// </summary>
    bool AuthorizationRequired { get; }

    /// <summary>
    /// Requires that <see cref="ClaimsPrincipal.IsInRole(string)"/> return <see langword="true"/>
    /// for the user within <see cref="HttpContext.User"/>
    /// for at least one role in the list prior to executing the GraphQL request or accepting
    /// the WebSocket connection.  If no roles are specified, authorization is not checked.
    /// Also implies <see cref="AuthorizationRequired"/> is enabled even if it is set to <see langword="false"/>.
    /// </summary>
    IEnumerable<string> AuthorizedRoles { get; }

    /// <summary>
    /// If set, requires that <see cref="IAuthorizationService.AuthorizeAsync(ClaimsPrincipal, object, string)"/>
    /// return a successful result for the user within <see cref="HttpContext.User"/>
    /// for the specified policy before executing the GraphQL
    /// request or accepting the WebSocket connection.
    /// Also implies <see cref="AuthorizationRequired"/> is enabled even if it is set to <see langword="false"/>.
    /// </summary>
    string? AuthorizedPolicy { get; }
}
