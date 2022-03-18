namespace Shane32.GraphQL.AspNetCore;

/// <summary>
/// An action result that formats the <see cref="ExecutionResult"/> as JSON.
/// </summary>
public class ExecutionResultActionResult : IActionResult
{
    private readonly ExecutionResult _executionResult;
    private readonly HttpStatusCode _statusCode;

    /// <inheritdoc cref="ExecutionResultActionResult"/>
    public ExecutionResultActionResult(ExecutionResult executionResult, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _executionResult = executionResult;
        _statusCode = statusCode;
    }

    /// <inheritdoc/>
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var writer = context.HttpContext.RequestServices.GetRequiredService<IGraphQLSerializer>();
        var response = context.HttpContext.Response;
        response.ContentType = "application/json";
        response.StatusCode = (int)_statusCode;
        await writer.WriteAsync(response.Body, _executionResult, context.HttpContext.RequestAborted);
    }
}
