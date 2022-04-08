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
        return default;
        //return new(visitor);
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
            if (_fragmentDefinitionsToCheck?.Count == 0)
                _fragmentDefinitionsToCheck = null;
            else if (_fragmentDefinitionsToCheck != null)
                _fragmentDefinitionsToCheck = new List<GraphQLFragmentDefinition>(_fragmentDefinitionsToCheck);
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
        private bool _processedOperation; // indicates that the operation has been processed
        private List<GraphQLFragmentDefinition>? _fragmentDefinitionsToCheck; // contains a list of the remaining fragments to process, or null if none
        private Dictionary<string, List<(IProvideMetadata, ASTNode)>>? _policiesToCheck; // contains a list of policies that have to be checked after all nodes are processed

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
            if (node == context.Operation) {
                _checkTree = false;
                _checkUntil = null;
                _processedOperation = true;
                if (_fragmentDefinitionsToCheck == null)
                    Finish(context);
            } else if (node is GraphQLFragmentDefinition fragmentDefinition && _fragmentDefinitionsToCheck != null) {
                if (_fragmentDefinitionsToCheck.Remove(fragmentDefinition)) {
                    if (_fragmentDefinitionsToCheck.Count == 0)
                        _fragmentDefinitionsToCheck = null;
                    if (_processedOperation && _fragmentDefinitionsToCheck == null)
                        Finish(context);
                }
            } else if (node == _checkUntil) {
                _checkTree = true;
                _checkUntil = null;
            }
        }

        /// <summary>
        /// Checks the specified graph/field
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="node"></param>
        /// <param name="context"></param>
        private void Validate(IProvideMetadata? obj, ASTNode node, ValidationContext context)
        {
            if (obj == null)
                return;
            var policies = obj.GetPolicies();
            if (policies?.Count > 0) {
                _policiesToCheck ??= new Dictionary<string, List<(IProvideMetadata, ASTNode)>>();
                foreach (var policy in policies) {
                    if (!_policiesToCheck.TryGetValue(policy, out var nodelist)) {
                        nodelist = new List<(IProvideMetadata, ASTNode)>();
                        _policiesToCheck[policy] = nodelist;
                    }
                    nodelist.Add((obj, node));
                }
            }

            var roles = obj.GetRoles();
            if (roles?.Count > 0) {
                foreach (var role in roles) {
                    if (AuthorizeRole(role))
                        return;
                }
                HandleNodeNotInRoles(obj, node, roles, context);
                _checkUntil = node;
                _checkTree = false;
            }
        }

        /// <inheritdoc cref="ClaimsPrincipal.IsInRole(string)"/>
        protected virtual bool AuthorizeRole(string role)
            => ClaimsPrincipal.IsInRole(role);

        /// <summary>
        /// Adds a error to the validation context indicating that the user is not a member of any of
        /// the roles required by this graph, field or query argument.
        /// </summary>
        /// <param name="obj">A graph, field or query argument.</param>
        /// <param name="roles">The list of roles of which the user must be a member.</param>
        /// <param name="node">The AST node where the graph, field or query argument was matched.</param>
        /// <param name="context">The validation context.</param>
        protected virtual void HandleNodeNotInRoles(IProvideMetadata obj, ASTNode node, List<string> roles, ValidationContext context)
        {
            var err = new AccessDeniedError(context.Document.Source, node);
            err.RolesRequired = roles;
            context.ReportError(err);
        }

        /// <summary>
        /// Adds a error to the validation context indicating that the user is not a member of any of
        /// the roles required by this graph, field or query argument.
        /// </summary>
        /// <param name="nodes">A list of graph, field or query arguments being authenticated along with the nodes they were matched from.</param>
        /// <param name="policy">The policy which these nodes are being authenticated against.</param>
        /// <param name="authorizationResult">The result of the authentication request.</param>
        /// <param name="context">The validation context.</param>
        protected virtual void HandleNodesNotInPolicy(List<(IProvideMetadata Obj, ASTNode Node)> nodes, string policy, AuthorizationResult authorizationResult, ValidationContext context)
        {
            var err = new AccessDeniedError(context.Document.Source, nodes.Select(x => x.Node).ToArray());
            err.PolicyRequired = policy;
            err.PolicyAuthorizationResult = authorizationResult;
            context.ReportError(err);
        }

        /// <summary>
        /// Executes when all nodes have been processed.
        /// </summary>
        protected virtual void Finish(ValidationContext context)
        {
            // todo: convert INodeVisitor to async
            FinishAsync(context).GetAwaiter().GetResult();
        }

        private async Task FinishAsync(ValidationContext context)
        {
            if (_policiesToCheck != null) {
                foreach (var pair in _policiesToCheck) {
                    var policy = pair.Key;
                    var nodes = pair.Value;
                    var result = await AuthorizePolicyAsync(policy).ConfigureAwait(false);
                    if (!result.Succeeded) {
                        HandleNodesNotInPolicy(nodes, policy, result, context);
                    }
                }
            }
        }

        /// <inheritdoc cref="AuthorizationServiceExtensions.AuthorizeAsync(IAuthorizationService, ClaimsPrincipal, string)"/>
        protected virtual Task<AuthorizationResult> AuthorizePolicyAsync(string policy)
            => AuthorizationService.AuthorizeAsync(ClaimsPrincipal, policy);
    }
}
