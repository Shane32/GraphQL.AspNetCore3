using System.Security.Claims;
using System.Security.Principal;
using Microsoft.AspNetCore.Authorization;

namespace GraphQL.AspNetCore3;

/// <summary>
/// Validates a document against the configured set of policy and role requirements.
/// </summary>
public class AuthorizationValidationRule : ValidationRuleBase
{
    /// <inheritdoc/>
    public override async ValueTask<INodeVisitor?> GetPreNodeVisitorAsync(ValidationContext context)
    {
        var user = context.User
            ?? throw new InvalidOperationException("User could not be retrieved from ValidationContext. Please be sure it is set in ExecutionOptions.User.");
        var provider = context.RequestServices
            ?? throw new MissingRequestServicesException();
        var authService = provider.GetService<IAuthorizationService>()
            ?? throw new InvalidOperationException("An instance of IAuthorizationService could not be pulled from the dependency injection framework.");

        var visitor = new AuthorizationVisitor(context, user, authService);
        // if the schema fails authentication, report the error and do not perform any additional authorization checks.
        return await visitor.ValidateSchemaAsync(context) ? visitor : null;
    }
}

