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

    private IValidationResult Validate(string query)
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
        var (result, variables) = validator.ValidateAsync(new ValidationOptions {
            Document = document,
            Extensions = Inputs.Empty,
            Operation = (GraphQLOperationDefinition)document.Definitions.Single(x => x.Kind == ASTNodeKind.OperationDefinition),
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
        var ret = Validate(@"{ parent { child(arg: null) } }");
        ret.IsValid.ShouldBe(isValid);
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
