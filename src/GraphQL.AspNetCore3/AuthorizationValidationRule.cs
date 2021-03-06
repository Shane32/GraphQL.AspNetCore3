using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;

namespace GraphQL.AspNetCore3;

/// <summary>
/// Validates a document against the configured set of policy and role requirements.
/// </summary>
public class AuthorizationValidationRule : IValidationRule
{
    private readonly IHttpContextAccessor _contextAccessor;

    /// <inheritdoc cref="AuthorizationValidationRule"/>
    public AuthorizationValidationRule(IHttpContextAccessor httpContextAccessor)
    {
        _contextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc/>
    public ValueTask<INodeVisitor?> ValidateAsync(ValidationContext context)
    {
        var httpContext = _contextAccessor.HttpContext
            ?? throw new InvalidOperationException("HttpContext could not be retrieved from IHttpContextAccessor.");
        var user = httpContext.User
            ?? throw new InvalidOperationException("ClaimsPrincipal could not be retrieved from HttpContext.");
        var provider = context.RequestServices
            ?? throw new MissingRequestServicesException();
        var authService = provider.GetService<IAuthorizationService>()
            ?? throw new InvalidOperationException("An instance of IAuthorizationService could not be pulled from the dependency injection framework.");

        var visitor = new AuthorizationVisitor(context, user, authService);
        // if the schema fails authentication, report the error and do not perform any additional authorization checks.
        return visitor.ValidateSchema(context) ? new(visitor) : default;
    }
}

