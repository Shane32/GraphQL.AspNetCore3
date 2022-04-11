using System.Security.Claims;
using GraphQL.Validation;
using GraphQLParser.AST;
using Microsoft.AspNetCore.Authorization;

namespace Tests;

public class AuthorizationTests
{
    private readonly Schema _schema = new();
    private readonly ObjectGraphType _query = new();
    private readonly FieldType _field = new();
    private readonly ObjectGraphType _childGraph = new();
    private readonly FieldType _childField = new();
    private ClaimsPrincipal _principal = new(new ClaimsIdentity());

    public AuthorizationTests()
    {
        _childField.Name = "child";
        _childField.Type = typeof(StringGraphType);
        _childGraph.AddField(_childField);
        _field.ResolvedType = _childGraph;
        _field.Name = "parent";
        _query.AddField(_field);
        _schema.Query = _query;
    }

    private void SetAuthorized()
    {
        // set principal to an authenticated user in the role "MyRole"
        _principal = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] { new Claim(ClaimTypes.Role, "MyRole") }, "Cookie"));
    }

    private IValidationResult Validate(string query)
    {
        var mockAuthorizationService = new Mock<IAuthorizationService>(MockBehavior.Strict);
        mockAuthorizationService.Setup(x => x.AuthorizeAsync(_principal, null, It.IsAny<string>())).Returns<ClaimsPrincipal, object, string>((_, _, policy) => {
            if (policy == "MyPolicy")
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
        }).GetAwaiter().GetResult();
        return result;
    }

    [Fact]
    public void Simple()
    {
        var ret = Validate(@"{ parent { child } }");
        ret.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, false, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.Authorize, Mode.None, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.Authorize, Mode.None, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.Authorize, Mode.None, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.Authorize, Mode.None, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.Authorize, Mode.None, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.Authorize, Mode.None, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.Authorize, Mode.None, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.Authorize, Mode.None, true, true)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.Authorize, false, false)]
    [InlineData(Mode.None, Mode.None, Mode.None, Mode.None, Mode.Authorize, true, true)]
    public void Matrix(Mode schemaMode, Mode queryMode, Mode fieldMode, Mode childMode, Mode childFieldMode, bool authenticated, bool isValid)
    {
        Apply(_schema, schemaMode);
        Apply(_query, queryMode);
        Apply(_field, fieldMode);
        Apply(_childGraph, childMode);
        Apply(_childField, childFieldMode);
        if (authenticated)
            SetAuthorized();
        var ret = Validate(@"{ parent { child } }");
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
