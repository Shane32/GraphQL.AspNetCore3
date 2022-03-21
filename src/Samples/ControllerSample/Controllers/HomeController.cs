using GraphQL.Transport;
using GraphQL.Validation;
using Microsoft.AspNetCore.Mvc;
using Shane32.GraphQL.AspNetCore;

namespace ControllerSample.Controllers;

public class HomeController : Controller
{
    private readonly IDocumentExecuter<ISchema> _executer;
    private readonly IGraphQLTextSerializer _serializer;
    private readonly IEnumerable<IValidationRule> _validationRules;
    private readonly IEnumerable<IValidationRule> _cachedDocumentValidationRules;

    public HomeController(IDocumentExecuter<ISchema> executer, IGraphQLTextSerializer serializer, IHttpContextAccessor httpContextAccessor)
    {
        _executer = executer;
        _serializer = serializer;
        var rule = new OperationTypeValidationRule(httpContextAccessor);
        _validationRules = DocumentValidator.CoreRules.Append(rule).ToArray();
        _cachedDocumentValidationRules = new[] { rule };
    }

    public IActionResult Index()
        => View();

    [HttpGet]
    [ActionName("graphql")]
    public Task<IActionResult> GraphQLGetAsync(string query, string? operationName)
        => ExecuteGraphQLRequestAsync(ParseRequest(query, operationName));

    [HttpPost]
    [ActionName("graphql")]
    public async Task<IActionResult> GraphQLPostAsync()
    {
        if (HttpContext.Request.HasFormContentType) {
            var form = await HttpContext.Request.ReadFormAsync(HttpContext.RequestAborted);
            return await ExecuteGraphQLRequestAsync(ParseRequest(form["query"].ToString(), form["operationName"].ToString(), form["variables"].ToString(), form["extensions"].ToString()));
        } else if (HttpContext.Request.HasJsonContentType()) {
            var request = await _serializer.ReadAsync<GraphQLRequest>(HttpContext.Request.Body, HttpContext.RequestAborted);
            return await ExecuteGraphQLRequestAsync(request);
        }
        return BadRequest();
    }

    private GraphQLRequest ParseRequest(string query, string? operationName, string? variables = null, string? extensions = null)
        => new GraphQLRequest {
            Query = query,
            OperationName = operationName,
            Variables = _serializer.Deserialize<Inputs>(variables == "" ? null : variables),
            Extensions = _serializer.Deserialize<Inputs>(extensions == "" ? null : extensions),
        };

    private async Task<IActionResult> ExecuteGraphQLRequestAsync(GraphQLRequest? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Query))
            return BadRequest();
        try {
            return new ExecutionResultActionResult(await _executer.ExecuteAsync(new ExecutionOptions {
                Query = request.Query,
                OperationName = request.OperationName,
                Variables = request.Variables,
                Extensions = request.Extensions,
                CancellationToken = HttpContext.RequestAborted,
                RequestServices = HttpContext.RequestServices,
                ValidationRules = _validationRules,
                CachedDocumentValidationRules = _cachedDocumentValidationRules,
            }));
        }
        catch {
            return BadRequest();
        }
    }
}
