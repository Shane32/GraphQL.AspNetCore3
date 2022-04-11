using System.Security.Claims;
using GraphQL.AspNetCore3.Errors;
using GraphQL.Validation;
using GraphQLParser.AST;
using Microsoft.AspNetCore.Authorization;

namespace Tests;

public class AuthorizationTests
{
    private readonly Schema _schema = new();
    private readonly ObjectGraphType _query = new() { Name = "QueryType" };
    private readonly FieldType _field = new() { Name = "parent" };
    private readonly ObjectGraphType _childGraph = new() { Name = "ChildType" };
    private readonly FieldType _childField = new() { Name = "child" };
    private readonly QueryArgument _argument = new(typeof(StringGraphType)) { Name = "Arg" };
    private readonly QueryArguments _arguments = new();
    private ClaimsPrincipal _principal = new(new ClaimsIdentity());
    private bool _policyPasses;

    public AuthorizationTests()
    {
        _arguments.Add(_argument);
        _childField.Arguments = _arguments;
        _childField.Type = typeof(StringGraphType);
        _childGraph.AddField(_childField);
        _field.ResolvedType = _childGraph;
        _query.AddField(_field);
        _schema.Query = _query;
    }

    private void SetAuthorized()
    {
        // set principal to an authenticated user in the role "MyRole"
        _principal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.Role, "MyRole") }, "Cookie"));
        _policyPasses = true;
    }

    private IValidationResult Validate(string query, bool shouldPassCoreRules = true)
    {
        var mockAuthorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        mockAuthorizationService.Setup(x => x.AuthorizeAsync(_principal, null, It.IsAny<string>())).Returns<ClaimsPrincipal, object, string>((_, _, policy) => {
            if (policy == "MyPolicy" && _policyPasses)
                return Task.FromResult(AuthorizationResult.Success());
            return Task.FromResult(AuthorizationResult.Failed());
        });
        var mockServices = new Mock<IServiceProvider>(MockBehavior.Strict);
        mockServices.Setup(x => x.GetService(typeof(IAuthorizationService))).Returns(mockAuthorizationService.Object);
        var mockHttpContext = new Mock<HttpContext>(MockBehavior.Strict);
        mockHttpContext.Setup(x => x.User).Returns(_principal);
        var mockContextAccessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
        mockContextAccessor.Setup(x => x.HttpContext).Returns(mockHttpContext.Object);
        var document = GraphQLParser.Parser.Parse(query);
        var validator = new DocumentValidator();
        if (shouldPassCoreRules) {
            var (coreRulesResult, _) = validator.ValidateAsync(new ValidationOptions {
                Document = document,
                Extensions = Inputs.Empty,
                Operation = (GraphQLOperationDefinition)document.Definitions.First(x => x.Kind == ASTNodeKind.OperationDefinition),
                Schema = _schema,
                UserContext = new Dictionary<string, object?>(),
                Variables = Inputs.Empty,
                RequestServices = mockServices.Object,
            }).GetAwaiter().GetResult(); // there is no async code being tested
            coreRulesResult.IsValid.ShouldBeTrue("Query does not pass core rules");
        }
        var (result, variables) = validator.ValidateAsync(new ValidationOptions {
            Document = document,
            Extensions = Inputs.Empty,
            Operation = (GraphQLOperationDefinition)document.Definitions.First(x => x.Kind == ASTNodeKind.OperationDefinition),
            Rules = new IValidationRule[] { new AuthorizationValidationRule(mockContextAccessor.Object) },
            Schema = _schema,
            UserContext = new Dictionary<string, object?>(),
            Variables = Inputs.Empty,
            RequestServices = mockServices.Object,
        }).GetAwaiter().GetResult(); // there is no async code being tested
        return result;
    }

    [Fact]
    public void Simple()
    {
        var ret = Validate(@"{ parent { child(arg: null) } }");
        ret.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(Mode.Authorize, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, -1, "Access denied for schema.")]
    [InlineData(Mode.None, Mode.Authorize, Mode.None, Mode.None, Mode.None, Mode.None, 1, "Access denied for type 'QueryType' for query operation.")]
    [InlineData(Mode.None, Mode.None, Mode.Authorize, Mode.None, Mode.None, Mode.None, 3, "Access denied for field 'parent' on type 'QueryType'.")]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.Authorize, Mode.None, Mode.None, 3, "Access denied for type 'ChildType' for field 'parent' on type 'QueryType'.")]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.Authorize, Mode.None, 12, "Access denied for field 'child' on type 'ChildType'.")]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.Authorize, 18, "Access denied for argument 'arg' for field 'child' on type 'ChildType'.")]
    public void ErrorMessages(Mode schemaMode, Mode queryMode, Mode fieldMode, Mode childMode, Mode childFieldMode, Mode argumentMode, int column, string message)
    {
        Apply(_schema, schemaMode);
        Apply(_query, queryMode);
        Apply(_field, fieldMode);
        Apply(_childGraph, childMode);
        Apply(_childField, childFieldMode);
        Apply(_argument, argumentMode);
        var ret = Validate(@"{ parent { child(arg: null) } }");
        ret.IsValid.ShouldBeFalse();
        ret.Errors.Count.ShouldBe(1);
        ret.Errors[0].Message.ShouldBe(message);
        if (column == -1)
            ret.Errors[0].Locations.ShouldBeNull();
        else {
            ret.Errors[0].Locations.ShouldNotBeNull();
            ret.Errors[0].Locations!.Count.ShouldBe(1);
            ret.Errors[0].Locations![0].Line.ShouldBe(1);
            ret.Errors[0].Locations![0].Column.ShouldBe(column);
        }
        var err = ret.Errors[0].ShouldBeOfType<AccessDeniedError>();
        err.Code.ShouldBe("ACCESS_DENIED");
        err.PolicyAuthorizationResult.ShouldBeNull();
        err.PolicyRequired.ShouldBeNull();
        err.RolesRequired.ShouldBeNull();
    }

    [Theory]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, false, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.Authorize, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.Authorize, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.RoleSuccess, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.RoleSuccess, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.RoleFailure, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.RoleFailure, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.RoleMultiple, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.RoleMultiple, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.PolicySuccess, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.PolicySuccess, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.PolicyFailure, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.PolicyFailure, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.PolicyMultiple, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.PolicyMultiple, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.Anonymous, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, false, true)]
    [InlineData(Mode.Anonymous, Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.Authorize, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.Authorize, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.RoleSuccess, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.RoleSuccess, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.RoleFailure, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.RoleFailure, Mode.None, Mode.None, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.RoleMultiple, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.RoleMultiple, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.PolicySuccess, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.PolicySuccess, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.PolicyFailure, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.PolicyFailure, Mode.None, Mode.None, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.PolicyMultiple, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.PolicyMultiple, Mode.None, Mode.None, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.Anonymous, Mode.None, Mode.None, Mode.None, Mode.None, false, true)]
    [InlineData(Mode.None, Mode.Anonymous, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.Authorize, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.Authorize, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.RoleSuccess, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.RoleSuccess, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.RoleFailure, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.RoleFailure, Mode.None, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.RoleMultiple, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.RoleMultiple, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.PolicySuccess, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.PolicySuccess, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.PolicyFailure, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.PolicyFailure, Mode.None, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.PolicyMultiple, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.PolicyMultiple, Mode.None, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.Anonymous, Mode.None, Mode.None, Mode.None, false, true)]
    [InlineData(Mode.None, Mode.None, Mode.Anonymous, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.Authorize, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.Authorize, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.RoleSuccess, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.RoleSuccess, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.RoleFailure, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.RoleFailure, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.RoleMultiple, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.RoleMultiple, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.PolicySuccess, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.PolicySuccess, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.PolicyFailure, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.PolicyFailure, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.PolicyMultiple, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.PolicyMultiple, Mode.None, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.Anonymous, Mode.None, Mode.None, false, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.Anonymous, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.Authorize, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.Authorize, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleSuccess, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleSuccess, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleFailure, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleFailure, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleMultiple, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleMultiple, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicySuccess, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicySuccess, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicyFailure, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicyFailure, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicyMultiple, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicyMultiple, Mode.None, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.Anonymous, Mode.None, false, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.Anonymous, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.Authorize, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.Authorize, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleSuccess, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleSuccess, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleFailure, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleFailure, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleMultiple, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.RoleMultiple, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicySuccess, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicySuccess, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicyFailure, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicyFailure, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicyMultiple, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.PolicyMultiple, true, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.Anonymous, false, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, Mode.Anonymous, true, true)]
    [InlineData(Mode.Authorize, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, false, false)]
    [InlineData(Mode.Authorize, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, true, true)]
    [InlineData(Mode.Anonymous, Mode.Authorize, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, false, true)] // query is authorize, but field is anonymous
    [InlineData(Mode.Anonymous, Mode.Authorize, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, true, true)]
    [InlineData(Mode.Anonymous, Mode.Anonymous, Mode.Authorize, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, false, false)]
    [InlineData(Mode.Anonymous, Mode.Anonymous, Mode.Authorize, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, true, true)]
    [InlineData(Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Authorize, Mode.Anonymous, Mode.Anonymous, false, true)] // child graph is authorize, but child field is anonymous
    [InlineData(Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Authorize, Mode.Anonymous, Mode.Anonymous, true, true)]
    [InlineData(Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Authorize, Mode.Anonymous, false, false)]
    [InlineData(Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Authorize, Mode.Anonymous, true, true)]
    [InlineData(Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Authorize, false, false)]
    [InlineData(Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Anonymous, Mode.Authorize, true, true)]
    public void Matrix(Mode schemaMode, Mode queryMode, Mode fieldMode, Mode childMode, Mode childFieldMode, Mode argumentMode, bool authenticated, bool isValid)
    {
        Apply(_schema, schemaMode);
        Apply(_query, queryMode);
        Apply(_field, fieldMode);
        Apply(_childGraph, childMode);
        Apply(_childField, childFieldMode);
        Apply(_argument, argumentMode);
        if (authenticated)
            SetAuthorized();

        // simple test
        var ret = Validate(@"{ parent { child(arg: null) } }");
        ret.IsValid.ShouldBe(isValid);

        // inline fragment
        ret = Validate(@"{ parent { ... on ChildType { child(arg: null) } } }");
        ret.IsValid.ShouldBe(isValid);

        // fragment prior to query
        ret = Validate(@"fragment frag on ChildType { child(arg: null) } { parent { ...frag } }");
        ret.IsValid.ShouldBe(isValid);

        // fragment after query
        ret = Validate(@"{ parent { ...frag } } fragment frag on ChildType { child(arg: null) }");
        ret.IsValid.ShouldBe(isValid);

        // nested fragments prior to query
        ret = Validate(@"fragment nestedFrag on ChildType { child(arg: null) } fragment frag on ChildType { ...nestedFrag } { parent { ...frag } }");
        ret.IsValid.ShouldBe(isValid);

        // nested fragments after query
        ret = Validate(@"{ parent { ...frag } } fragment frag on ChildType { ...nestedFrag } fragment nestedFrag on ChildType { child(arg: null) }");
        ret.IsValid.ShouldBe(isValid);

        // nested fragments around query 1
        ret = Validate(@"fragment frag on ChildType { ...nestedFrag } { parent { ...frag } } fragment nestedFrag on ChildType { child(arg: null) }");
        ret.IsValid.ShouldBe(isValid);

        // nested fragments around query 2
        ret = Validate(@"fragment nestedFrag on ChildType { child(arg: null) } { parent { ...frag } } fragment frag on ChildType { ...nestedFrag }");
        ret.IsValid.ShouldBe(isValid);
    }

    [Theory]
    [InlineData(Mode.None, Mode.None, false, true)]
    [InlineData(Mode.None, Mode.None, true, true)]
    [InlineData(Mode.Authorize, Mode.None, false, false)] // schema requires authentication, so introspection queries fail
    [InlineData(Mode.Authorize, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.Authorize, false, true)]  // query type requires authentication, but __schema is an AllowAnonymous type
    [InlineData(Mode.None, Mode.Authorize, true, true)]
    public void Introspection(Mode schemaMode, Mode queryMode, bool authenticated, bool isValid)
    {
        Apply(_schema, schemaMode);
        Apply(_query, queryMode);
        if (authenticated)
            SetAuthorized();

        var ret = Validate(@"{ __schema { types { name } } __typename }");
        ret.IsValid.ShouldBe(isValid);

        ret = Validate(@"{ __schema { types { name } } }");
        ret.IsValid.ShouldBe(isValid);

        ret = Validate(@"{ __type(name: ""QueryType"") { name } }");
        ret.IsValid.ShouldBe(isValid);

        ret = Validate(@"{ __type(name: ""QueryType"") { name } __typename }");
        ret.IsValid.ShouldBe(isValid);
    }

    [Theory]
    [InlineData(Mode.None, Mode.None, false, false, true)]
    [InlineData(Mode.None, Mode.None, false, true, true)]
    [InlineData(Mode.None, Mode.None, true, false, true)]
    [InlineData(Mode.None, Mode.None, true, true, true)]
    [InlineData(Mode.Authorize, Mode.None, false, false, false)] // selecting only __typename is not enough to allow QueryType to pass validation
    [InlineData(Mode.Authorize, Mode.None, false, true, true)]
    [InlineData(Mode.Authorize, Mode.None, true, false, false)]
    [InlineData(Mode.Authorize, Mode.None, true, true, true)]
    [InlineData(Mode.Authorize, Mode.Anonymous, false, false, false)]
    [InlineData(Mode.Authorize, Mode.Anonymous, false, true, true)]
    [InlineData(Mode.Authorize, Mode.Anonymous, true, false, true)] // at least only anonymous field, and no authenticated fields, were selected in the query, so validation passes
    [InlineData(Mode.Authorize, Mode.Anonymous, true, true, true)]
    public void OnlyTypeName(Mode queryMode, Mode fieldMode, bool includeField, bool authenticated, bool isValid)
    {
        Apply(_query, queryMode);
        Apply(_field, fieldMode);
        if (authenticated)
            SetAuthorized();

        IValidationResult ret;

        if (includeField) {
            ret = Validate("{ __typename parent { child } }");
        } else {
            ret = Validate("{ __typename }");
        }
        ret.IsValid.ShouldBe(isValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothAuthorizedAndAnonymousFields(bool authenticated)
    {
        Apply(_query, Mode.Authorize);
        _query.AddField(new FieldType { Name = "test", Type = typeof(StringGraphType) }).AllowAnonymous();
        if (authenticated)
            SetAuthorized();

        var ret = Validate("{ parent { child } test }");
        ret.IsValid.ShouldBe(authenticated);
    }

    [Fact]
    public void UnusedOperationsAreIgnored()
    {
        Apply(_field, Mode.Authorize);
        Apply(_childGraph, Mode.Authorize);
        _query.AddField(new FieldType { Name = "test", Type = typeof(StringGraphType) });
        var ret = Validate("query op1 { test } query op2 { parent { child } }");
        ret.IsValid.ShouldBeTrue();
        ret = Validate("query op1 { parent { child } } query op2 { test }");
        ret.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void UnusedFragmentsAreIgnored()
    {
        Apply(_field, Mode.Authorize);
        Apply(_childGraph, Mode.Authorize);
        _query.AddField(new FieldType { Name = "test", Type = typeof(StringGraphType) });
        var ret = Validate("query op1 { ...frag1 } query op2 { ...frag2 } fragment frag1 on QueryType { test } fragment frag2 on QueryType { parent { child } }");
        ret.IsValid.ShouldBeTrue();
        ret = Validate("query op1 { ...frag1 } query op2 { ...frag2 } fragment frag1 on QueryType { parent { child } } fragment frag2 on QueryType { test }");
        ret.IsValid.ShouldBeFalse();
    }

    private void Apply(IProvideMetadata obj, Mode mode)
    {
        switch (mode) {
            case Mode.None:
                break;
            case Mode.RoleSuccess:
                obj.AuthorizeWithRoles("MyRole");
                break;
            case Mode.RoleFailure:
                obj.AuthorizeWithRoles("FailingRole");
                break;
            case Mode.RoleMultiple:
                obj.AuthorizeWithRoles("MyRole", "FailingRole");
                break;
            case Mode.Authorize:
                obj.Authorize();
                break;
            case Mode.PolicySuccess:
                obj.AuthorizeWithPolicy("MyPolicy");
                break;
            case Mode.PolicyFailure:
                obj.AuthorizeWithPolicy("FailingPolicy");
                break;
            case Mode.PolicyMultiple:
                obj.AuthorizeWithPolicy("MyPolicy");
                obj.AuthorizeWithPolicy("FailingPolicy");
                break;
            case Mode.Anonymous:
                obj.AllowAnonymous();
                break;
        }
    }

    [Fact]
    public void Constructors()
    {
        Should.Throw<ArgumentNullException>(() => new AuthorizationValidationRule(null!));
        Should.Throw<ArgumentNullException>(() => new AuthorizationValidationRule.AuthorizationVisitor(null!, _principal, Mock.Of<IAuthorizationService>()));
        Should.Throw<ArgumentNullException>(() => new AuthorizationValidationRule.AuthorizationVisitor(new ValidationContext(), null!, Mock.Of<IAuthorizationService>()));
        Should.Throw<ArgumentNullException>(() => new AuthorizationValidationRule.AuthorizationVisitor(new ValidationContext(), _principal, null!));
        Should.Throw<InvalidOperationException>(() => new AuthorizationValidationRule.AuthorizationVisitor(new ValidationContext(), new ClaimsPrincipal(), Mock.Of<IAuthorizationService>()))
            .Message.ShouldBe("claimsPrincipal.Identity cannot be null.");
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void MiscErrors(bool noHttpContext, bool noClaimsPrincipal, bool noRequestServices, bool noAuthenticationService)
    {
        var mockAuthorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        mockAuthorizationService.Setup(x => x.AuthorizeAsync(_principal, null, It.IsAny<string>())).Returns<ClaimsPrincipal, object, string>((_, _, policy) => {
            if (policy == "MyPolicy" && _policyPasses)
                return Task.FromResult(AuthorizationResult.Success());
            return Task.FromResult(AuthorizationResult.Failed());
        });
        var mockServices = new Mock<IServiceProvider>(MockBehavior.Strict);
        mockServices.Setup(x => x.GetService(typeof(IAuthorizationService))).Returns(noAuthenticationService ? null! : mockAuthorizationService.Object);
        var mockHttpContext = new Mock<HttpContext>(MockBehavior.Strict);
        mockHttpContext.Setup(x => x.User).Returns(noClaimsPrincipal ? null! : _principal);
        var mockContextAccessor = new Mock<IHttpContextAccessor>(MockBehavior.Strict);
        mockContextAccessor.Setup(x => x.HttpContext).Returns(noHttpContext ? null! : mockHttpContext.Object);
        var document = GraphQLParser.Parser.Parse("{ __typename }");
        var validator = new DocumentValidator();

        var err = Should.Throw<Exception>(() => validator.ValidateAsync(new ValidationOptions {
            Document = document,
            Extensions = Inputs.Empty,
            Operation = (GraphQLOperationDefinition)document.Definitions.Single(x => x.Kind == ASTNodeKind.OperationDefinition),
            Rules = new IValidationRule[] { new AuthorizationValidationRule(mockContextAccessor.Object) },
            Schema = _schema,
            UserContext = new Dictionary<string, object?>(),
            Variables = Inputs.Empty,
            RequestServices = noRequestServices ? null : mockServices.Object,
        }).GetAwaiter().GetResult()); // there is no async code being tested

        if (noHttpContext)
            err.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("HttpContext could not be retrieved from IHttpContextAccessor.");

        if (noClaimsPrincipal)
            err.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("ClaimsPrincipal could not be retrieved from HttpContext.User.");

        if (noRequestServices)
            err.ShouldBeOfType<MissingRequestServicesException>();

        if (noAuthenticationService)
            err.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("An instance of IAuthorizationService could not be pulled from the dependency injection framework.");
    }

    public enum Mode
    {
        None,
        Authorize,
        RoleSuccess,
        RoleFailure,
        RoleMultiple,
        PolicySuccess,
        PolicyFailure,
        PolicyMultiple,
        Anonymous,
    }
}
