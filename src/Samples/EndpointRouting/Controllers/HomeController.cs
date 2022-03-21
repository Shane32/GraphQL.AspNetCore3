#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using GraphQL;
using GraphQL.Transport;
using GraphQL.Types;
using Microsoft.AspNetCore.Mvc;
using Shane32.GraphQL.AspNetCore;

namespace SampleServer.Controllers;

[Route("Home")]
public class HomeController : Controller
{
    private readonly IDocumentExecuter<ISchema> _executer;
    private readonly IGraphQLTextSerializer _serializer;

    public HomeController(IDocumentExecuter<ISchema> executer, IGraphQLTextSerializer serializer)
    {
        _executer = executer;
        _serializer = serializer;
    }

    [HttpGet("graphql")]
    public Task<IActionResult> GraphQLGetAsync(string query, string? operationName)
        => ExecuteGraphQLRequestAsync(ParseRequest(query, operationName));

    [HttpPost("graphql")]
    public async Task<IActionResult> GraphQLPostAsync(string query, string? variables, string? operationName, string? extensions)
    {
        if (HttpContext.Request.HasFormContentType) {
            return await ExecuteGraphQLRequestAsync(ParseRequest(query, operationName, variables, extensions));
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
            Variables = _serializer.Deserialize<Inputs>(variables),
            Extensions = _serializer.Deserialize<Inputs>(extensions),
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
            }));
        }
        catch {
            return BadRequest();
        }
    }
}
