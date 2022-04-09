using System.Security.Claims;
using GraphQL.AspNetCore3.Errors;
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
        // if not accessing over HTTP, skip authentication checks
        var httpContext = _contextAccessor.HttpContext ?? NoHttpContext();
        var user = httpContext.User ?? NoUser();
        var provider = context.RequestServices ?? NoRequestServices();
        var authService = provider.GetService<IAuthorizationService>() ?? NoAuthServiceError();
        var visitor = new AuthorizationVisitor(context, user, authService);
        return new(visitor);
    }

    private static HttpContext NoHttpContext()
        => throw new InvalidOperationException("HttpContext could not be retrieved from IHttpContextAccessor.");

    private static ClaimsPrincipal NoUser()
        => throw new InvalidOperationException("ClaimsPrincipal could not be retrieved from HttpContext.User.");

    private static IServiceProvider NoRequestServices()
        => throw new MissingRequestServicesException();

    private static IAuthorizationService NoAuthServiceError()
        => throw new InvalidOperationException("An instance of IAuthorizationService could not be pulled from the dependency injection framework.");

    /// <inheritdoc cref="AuthorizationValidationRule"/>
    public class AuthorizationVisitor : INodeVisitor
    {
        /// <inheritdoc cref="AuthorizationVisitor"/>
        public AuthorizationVisitor(ValidationContext context, ClaimsPrincipal claimsPrincipal, IAuthorizationService authorizationService)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            ClaimsPrincipal = claimsPrincipal ?? throw new ArgumentNullException(nameof(claimsPrincipal));
            AuthorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _fragmentDefinitionsToCheck = context.GetRecursivelyReferencedFragments(context.Operation);
        }

        /// <summary>
        /// The user that this authorization visitor will authenticate against.
        /// </summary>
        public ClaimsPrincipal ClaimsPrincipal { get; }

        /// <summary>
        /// The authorization service that is used to authorize policy requests.
        /// </summary>
        public IAuthorizationService AuthorizationService { get; }

        private bool _checkTree; // used to skip processing fragments that do not apply
        private ASTNode? _checkUntil; // used to pause processing graph decendants that have already failed a role check
        private readonly List<GraphQLFragmentDefinition>? _fragmentDefinitionsToCheck; // contains a list of fragments to process, or null if none
        private Dictionary<string, AuthorizationResult>? _policyResults; // contains a dictionary of policies that have been checked
        private Dictionary<string, bool>? _roleResults; // contains a dictionary of roles that have been checked

        /// <inheritdoc/>
        public virtual void Enter(ASTNode node, ValidationContext context)
        {
            if (node == context.Operation || (node is GraphQLFragmentDefinition fragmentDefinition && _fragmentDefinitionsToCheck != null && _fragmentDefinitionsToCheck.Contains(fragmentDefinition))) {
                _checkTree = true;
                Validate(context.TypeInfo.GetLastType(), node, context);
            } else if (_checkTree) {
                if (node is GraphQLField) {
                    // verify field
                    Validate(context.TypeInfo.GetFieldDef(), node, context);
                    Validate(context.TypeInfo.GetLastType(), node, context);
                } else if (node is GraphQLArgument) {
                    // ignore arguments of directives
                    if (context.TypeInfo.GetAncestor(0) is GraphQLField) {
                        // verify field argument
                        Validate(context.TypeInfo.GetArgument(), node, context);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public virtual void Leave(ASTNode node, ValidationContext context)
        {
            if (node == context.Operation || node is GraphQLFragmentDefinition) {
                _checkTree = false;
                _checkUntil = null;
            } else if (node == _checkUntil) {
                _checkTree = true;
                _checkUntil = null;
            }
        }

        /// <summary>
        /// Validates the specified graph, field or query argument.
        /// </summary>
        protected virtual void Validate(IProvideMetadata? obj, ASTNode node, ValidationContext context)
        {
            if (obj == null)
                return;

            var success = true;
            var policies = obj.GetPolicies();
            if (policies?.Count > 0) {
                _policyResults ??= new Dictionary<string, AuthorizationResult>();
                foreach (var policy in policies) {
                    if (!_policyResults.TryGetValue(policy, out var result)) {
                        result = AuthorizePolicy(policy);
                        _policyResults.Add(policy, result);
                    }
                    if (!result.Succeeded) {
                        context.ReportError(HandleNodeNotInPolicy(obj, node, policy, result, context));
                        success = false;
                    }
                }
            }

            var roles = obj.GetRoles();
            if (roles?.Count > 0) {
                _roleResults ??= new Dictionary<string, bool>();
                foreach (var role in roles) {
                    if (!_roleResults.TryGetValue(role, out var result)) {
                        result = AuthorizeRole(role);
                        _roleResults.Add(role, result);
                    }
                    if (result)
                        goto PassRoles;
                }
                context.ReportError(HandleNodeNotInRoles(obj, node, roles, context));
            }
        PassRoles:

            if (!success) {
                _checkUntil = node;
                _checkTree = false;
            }
        }

        /// <inheritdoc cref="ClaimsPrincipal.IsInRole(string)"/>
        protected virtual bool AuthorizeRole(string role)
            => ClaimsPrincipal.IsInRole(role);

        /// <inheritdoc cref="IAuthorizationService.AuthorizeAsync(ClaimsPrincipal, object, string)"/>
        protected virtual AuthorizationResult AuthorizePolicy(string policy)
            => AuthorizationService.AuthorizeAsync(ClaimsPrincipal, policy).GetAwaiter().GetResult();

        /// <summary>
        /// Adds a error to the validation context indicating that the user is not a member of any of
        /// the roles required by this graph, field or query argument.
        /// </summary>
        /// <param name="obj">A graph, field or query argument.</param>
        /// <param name="roles">The list of roles of which the user must be a member.</param>
        /// <param name="node">The AST node where the graph, field or query argument was matched.</param>
        /// <param name="context">The validation context.</param>
        protected virtual ValidationError HandleNodeNotInRoles(IProvideMetadata obj, ASTNode node, List<string> roles, ValidationContext context)
        {
            var err = new AccessDeniedError(context.Document.Source, GenerateResourceDescription(obj, node, context), node);
            err.RolesRequired = roles;
            return err;
        }

        /// <summary>
        /// Adds a error to the validation context indicating that the user is not a member of any of
        /// the roles required by this graph, field or query argument.
        /// </summary>
        /// <param name="obj">A graph, field or query argument.</param>
        /// <param name="node">The AST node where the graph, field or query argument was matched.</param>
        /// <param name="policy">The policy which these nodes are being authenticated against.</param>
        /// <param name="authorizationResult">The result of the authentication request.</param>
        /// <param name="context">The validation context.</param>
        protected virtual ValidationError HandleNodeNotInPolicy(IProvideMetadata obj, ASTNode node, string policy, AuthorizationResult authorizationResult, ValidationContext context)
        {
            var err = new AccessDeniedError(context.Document.Source, GenerateResourceDescription(obj, node, context), node);
            err.PolicyRequired = policy;
            err.PolicyAuthorizationResult = authorizationResult;
            return err;
        }

        /// <summary>
        /// Generates a friendly name for a specified graph, field or query argument.
        /// </summary>
        /// <param name="obj">A graph, field or query argument.</param>
        /// <param name="node">The AST node where the graph, field or query argument was matched.</param>
        /// <param name="context">The validation context.</param>
        protected virtual string GenerateResourceDescription(IProvideMetadata obj, ASTNode node, ValidationContext context)
        {
            if (obj is IGraphType graphType) {
                return $"type '{graphType.Name}'";
            } else if (obj is FieldType fieldType) {
                return $"field '{fieldType.Name}' on type '{context.TypeInfo.GetLastType(1)?.Name}'";
            } else if (obj is QueryArgument queryArgument) {
                return $"argument '{queryArgument.Name}' for field '{context.TypeInfo.GetFieldDef()?.Name}' on type '{context.TypeInfo.GetLastType(2)?.Name}'";
            } else {
                throw new ArgumentOutOfRangeException(nameof(obj), "Argument 'obj' is not a graph, field or query argument.");
            }
        }

        /// <inheritdoc cref="AuthorizationServiceExtensions.AuthorizeAsync(IAuthorizationService, ClaimsPrincipal, string)"/>
        protected virtual Task<AuthorizationResult> AuthorizePolicyAsync(string policy)
            => AuthorizationService.AuthorizeAsync(ClaimsPrincipal, policy);
    }
}
