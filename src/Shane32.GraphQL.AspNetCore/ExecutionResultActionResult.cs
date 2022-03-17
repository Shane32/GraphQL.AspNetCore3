namespace Shane32.GraphQL.AspNetCore;

/// <inheritdoc cref="IActionResult"/>
public class ExecutionResultActionResult : IActionResult
{
    private readonly ExecutionResult _executionResult;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public ExecutionResultActionResult(ExecutionResult executionResult)
    {
        _executionResult = executionResult;
    }

    /// <inheritdoc/>
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var writer = context.HttpContext.RequestServices.GetRequiredService<IGraphQLSerializer>();
        var response = context.HttpContext.Response;
        response.ContentType = "application/json";
        response.StatusCode = (int)HttpStatusCode.OK;
        await writer.WriteAsync(response.Body, _executionResult, context.HttpContext.RequestAborted);
    }
}
