using Microsoft.AspNetCore.Authorization;

namespace GraphQL.AspNetCore3.Errors;

/// <summary>
/// Represents an error indicating that the user is not allowed access to the specified node(s).
/// </summary>
public class AccessDeniedError : ValidationError
{
    /// <inheritdoc cref="AccessDeniedError"/>
    public AccessDeniedError(GraphQLParser.ROM originalQuery, string resource, params ASTNode[] nodes)
    : base(originalQuery, null!, $"Access denied for {resource}.", nodes)
    {
    }

    /// <summary>
    /// Returns the policy that would allow access to these node(s).
    /// </summary>
    public string? PolicyRequired { get; set; }

    /// <inheritdoc cref="AuthorizationResult"/>
    public AuthorizationResult? PolicyAuthorizationResult { get; set; }

    /// <summary>
    /// Returns the list of role memberships that would allow access to these node(s).
    /// </summary>
    public List<string>? RolesRequired { get; set; }
}
