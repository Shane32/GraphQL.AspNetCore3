using System.Security.Claims;
using System.Security.Principal;
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
        var httpContext = _contextAccessor.HttpContext ?? NoHttpContext();
        var user = httpContext.User ?? NoUser();
        var provider = context.RequestServices ?? NoRequestServices();
        var authService = provider.GetService<IAuthorizationService>() ?? NoAuthServiceError();

        var visitor = new AuthorizationVisitor(context, user, authService);
        return visitor.ValidateSchema(context) ? new(visitor) : default;
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
            if (claimsPrincipal.Identity == null)
                throw new InvalidOperationException($"{nameof(claimsPrincipal)}.Identity cannot be null.");
            AuthorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _fragmentDefinitionsToCheck = context.GetRecursivelyReferencedFragments(context.Operation);
            _userIsAuthenticated = claimsPrincipal.Identity.IsAuthenticated;
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
        private readonly List<GraphQLFragmentDefinition>? _fragmentDefinitionsToCheck; // contains a list of fragments to process, or null if none
        private Dictionary<string, AuthorizationResult>? _policyResults; // contains a dictionary of policies that have been checked
        private Dictionary<string, bool>? _roleResults; // contains a dictionary of roles that have been checked
        private readonly Stack<TypeInfo> _onlyAnonymousSelected = new();
        private readonly bool _userIsAuthenticated;
        private Dictionary<string, TypeInfo>? _fragments;
        private List<TodoInfo>? _todo;

        private struct TypeInfo
        {
            public bool AnyAuthenticated;
            public bool AnyAnonymous;
            public List<string>? WaitingOnFragments;
        }

        private class TodoInfo
        {
            public IProvideMetadata Obj { get; set; }
            public ASTNode Node { get; set; }
            public FieldType? ParentField { get; set; }
            public IGraphType? ParentGraph { get; set; }

            public TodoInfo(IProvideMetadata obj, ASTNode node)
            {
                Obj = obj;
                Node = node;
            }
        }

        /// <inheritdoc/>
        public virtual void Enter(ASTNode node, ValidationContext context)
        {
            if (node == context.Operation || (node is GraphQLFragmentDefinition fragmentDefinition && _fragmentDefinitionsToCheck != null && _fragmentDefinitionsToCheck.Contains(fragmentDefinition))) {
                var type = context.TypeInfo.GetLastType();
                if (type != null) {
                    // if type is null that means that no type was configured for this operation in the schema; will produce a separate validation error
                    _onlyAnonymousSelected.Push(new());
                    _checkTree = true;
                }
            } else if (_checkTree) {
                if (node is GraphQLField) {
                    var field = context.TypeInfo.GetFieldDef();
                    // might be null if no match was found in the schema
                    // and skip processing for __typeName
                    if (field != null && field != context.Schema.TypeNameMetaFieldType) {
                        var fieldAnonymousAllowed = field.IsAnonymousAllowed() || field == context.Schema.TypeMetaFieldType || field == context.Schema.SchemaMetaFieldType;
                        var ti = _onlyAnonymousSelected.Pop();
                        if (fieldAnonymousAllowed)
                            ti.AnyAnonymous = true;
                        else
                            ti.AnyAuthenticated = true;
                        _onlyAnonymousSelected.Push(ti);

                        if (!fieldAnonymousAllowed) {
                            Validate(field, node, context);
                        }
                    }
                    // prep for descendants, if any
                    _onlyAnonymousSelected.Push(new());
                } else if (node is GraphQLFragmentSpread fragmentSpread) {
                    var ti = _onlyAnonymousSelected.Pop();
                    var fragmentName = fragmentSpread.FragmentName.Name.StringValue;
                    if (_fragments?.TryGetValue(fragmentName, out var fragmentInfo) == true) {
                        ti.AnyAuthenticated |= fragmentInfo.AnyAuthenticated;
                        ti.AnyAnonymous |= fragmentInfo.AnyAnonymous;
                        if (fragmentInfo.WaitingOnFragments?.Count > 0) {
                            ti.WaitingOnFragments ??= new();
                            ti.WaitingOnFragments.AddRange(fragmentInfo.WaitingOnFragments);
                        }
                    } else {
                        ti.WaitingOnFragments ??= new();
                        ti.WaitingOnFragments.Add(fragmentName);
                    }
                    _onlyAnonymousSelected.Push(ti);
                } else if (node is GraphQLArgument) {
                    // ignore arguments of directives
                    if (context.TypeInfo.GetAncestor(2)?.Kind == ASTNodeKind.Field) {
                        // verify field argument
                        var arg = context.TypeInfo.GetArgument();
                        if (arg != null) {
                            Validate(arg, node, context);
                        }
                    }
                }
            }
        }

        /// <inheritdoc/>
        public virtual void Leave(ASTNode node, ValidationContext context)
        {
            if (!_checkTree)
                return;
            if (node == context.Operation) {
                _checkTree = false;
                PopAndProcess();
            } else if (node is GraphQLFragmentDefinition fragmentDefinition) {
                _checkTree = false;
                var ti = _onlyAnonymousSelected.Pop();
                RecursiveResolve(fragmentDefinition.FragmentName.Name.StringValue, ti);
            } else if (_checkTree && node is GraphQLField) {
                PopAndProcess();
            }

            void PopAndProcess()
            {
                var info = _onlyAnonymousSelected.Pop();
                var type = context.TypeInfo.GetLastType();
                if (type == null)
                    return;
                if (info.AnyAuthenticated || (!info.AnyAnonymous && (info.WaitingOnFragments?.Count ?? 0) == 0)) {
                    Validate(type, node, context);
                } else if (info.WaitingOnFragments?.Count > 0) {
                    _todo ??= new();
                    _todo.Add(new(type, node));
                }
            }

            void RecursiveResolve(string fragmentName, TypeInfo ti)
            {
                if (_fragments == null) {
                    _fragments = new();
                } else {
                    foreach (var fragment in _fragments) {

                    }
                }
                _fragments.TryAdd(fragmentName, ti);
            }
        }

        /// <summary>
        /// Validates authorization rules for the schema.
        /// Returns a value indicating if validation was successful.
        /// </summary>
        public virtual bool ValidateSchema(ValidationContext context)
            => Validate(context.Schema, null, context);

        /// <summary>
        /// Validates authorization rules for the specified schema, graph, field or query argument.
        /// Does not consider <see cref="AuthorizationExtensions.IsAnonymousAllowed(IProvideMetadata)"/>.
        /// Returns a value indicating if validation was successful for this node.
        /// </summary>
        protected virtual bool Validate(IProvideMetadata obj, ASTNode? node, ValidationContext context)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            bool requiresAuthorization = obj.IsAuthorizationRequired();
            if (!requiresAuthorization)
                return true;

            var success = true;
            var policies = obj.GetPolicies();
            if (policies?.Count > 0) {
                requiresAuthorization = false;
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
                requiresAuthorization = false;
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

            if (requiresAuthorization) {
                if (!Authorize()) {
                    HandleNodeNotAuthorized(obj, node, context);
                }
            }

            return success;
        }

        /// <inheritdoc cref="IIdentity.IsAuthenticated"/>
        protected virtual bool Authorize()
            => _userIsAuthenticated;

        /// <inheritdoc cref="ClaimsPrincipal.IsInRole(string)"/>
        protected virtual bool AuthorizeRole(string role)
            => ClaimsPrincipal.IsInRole(role);

        /// <inheritdoc cref="IAuthorizationService.AuthorizeAsync(ClaimsPrincipal, object, string)"/>
        protected virtual AuthorizationResult AuthorizePolicy(string policy)
            => AuthorizationService.AuthorizeAsync(ClaimsPrincipal, policy).GetAwaiter().GetResult();

        /// <summary>
        /// Adds a error to the validation context indicating that the user is not authenticated
        /// as required by this graph, field or query argument.
        /// </summary>
        /// <param name="obj">A graph, field or query argument.</param>
        /// <param name="node">The AST node where the graph, field or query argument was matched, or <see langword="null"/> for the schema.</param>
        /// <param name="context">The validation context.</param>
        protected virtual void HandleNodeNotAuthorized(IProvideMetadata obj, ASTNode? node, ValidationContext context)
        {
            var resource = GenerateResourceDescription(obj, node, context);
            var err = node == null ? new AccessDeniedError(context.Document.Source, resource) : new AccessDeniedError(context.Document.Source, resource, node);
            context.ReportError(err);
        }

        /// <summary>
        /// Adds a error to the validation context indicating that the user is not a member of any of
        /// the roles required by this graph, field or query argument.
        /// </summary>
        /// <param name="obj">A graph, field or query argument.</param>
        /// <param name="roles">The list of roles of which the user must be a member.</param>
        /// <param name="node">The AST node where the graph, field or query argument was matched, or <see langword="null"/> for the schema.</param>
        /// <param name="context">The validation context.</param>
        protected virtual ValidationError HandleNodeNotInRoles(IProvideMetadata obj, ASTNode? node, List<string> roles, ValidationContext context)
        {
            var resource = GenerateResourceDescription(obj, node, context);
            var err = node == null ? new AccessDeniedError(context.Document.Source, resource) : new AccessDeniedError(context.Document.Source, resource, node);
            err.RolesRequired = roles;
            return err;
        }

        /// <summary>
        /// Adds a error to the validation context indicating that the user is not a member of any of
        /// the roles required by this graph, field or query argument.
        /// </summary>
        /// <param name="obj">A graph, field or query argument.</param>
        /// <param name="node">The AST node where the graph, field or query argument was matched, or <see langword="null"/> for the schema.</param>
        /// <param name="policy">The policy which these nodes are being authenticated against.</param>
        /// <param name="authorizationResult">The result of the authentication request.</param>
        /// <param name="context">The validation context.</param>
        protected virtual ValidationError HandleNodeNotInPolicy(IProvideMetadata obj, ASTNode? node, string policy, AuthorizationResult authorizationResult, ValidationContext context)
        {
            var resource = GenerateResourceDescription(obj, node, context);
            var err = node == null ? new AccessDeniedError(context.Document.Source, resource) : new AccessDeniedError(context.Document.Source, resource, node);
            err.PolicyRequired = policy;
            err.PolicyAuthorizationResult = authorizationResult;
            return err;
        }

        /// <summary>
        /// Generates a friendly name for a specified graph, field or query argument.
        /// </summary>
        /// <param name="obj">A graph, field or query argument.</param>
        /// <param name="node">The AST node where the graph, field or query argument was matched, or <see langword="null"/> for the schema.</param>
        /// <param name="context">The validation context.</param>
        protected virtual string GenerateResourceDescription(IProvideMetadata obj, ASTNode? node, ValidationContext context)
        {
            if (obj is ISchema) {
                return "schema";
            } else if (obj is IGraphType graphType) {
                if (node is GraphQLField) {
                    return $"type '{graphType.Name}' for field '{context.TypeInfo.GetFieldDef(0)?.Name}' on type '{context.TypeInfo.GetLastType(2)?.Name}'";
                } else if (node is GraphQLOperationDefinition op) {
                    return $"type '{graphType.Name}' for {op.Operation.ToString().ToLower(System.Globalization.CultureInfo.InvariantCulture)} operation{(!string.IsNullOrEmpty(op.Name?.StringValue) ? $" '{op.Name}'" : null)}";
                } else {
                    return $"type '{graphType.Name}'";
                }
            } else if (obj is FieldType fieldType) {
                return $"field '{fieldType.Name}' on type '{context.TypeInfo.GetLastType(1)?.Name}'";
            } else if (obj is QueryArgument queryArgument) {
                return $"argument '{queryArgument.Name}' for field '{context.TypeInfo.GetFieldDef()?.Name}' on type '{context.TypeInfo.GetLastType(1)?.Name}'";
            } else {
                throw new ArgumentOutOfRangeException(nameof(obj), "Argument 'obj' is not a graph, field or query argument.");
            }
        }

        /// <inheritdoc cref="AuthorizationServiceExtensions.AuthorizeAsync(IAuthorizationService, ClaimsPrincipal, string)"/>
        protected virtual Task<AuthorizationResult> AuthorizePolicyAsync(string policy)
            => AuthorizationService.AuthorizeAsync(ClaimsPrincipal, policy);
    }
}
