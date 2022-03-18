namespace Shane32.GraphQL.AspNetCore;

/// <summary>
/// Validates that HTTP GET requests only execute queries; not mutations or subscriptions.
/// Validates that HTTP POST requests do not execute subscriptions.
/// </summary>
public class OperationTypeValidationRule : IValidationRule
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <inheritdoc cref="OperationTypeValidationRule"/>
    public OperationTypeValidationRule(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc/>
    public ValueTask<INodeVisitor?> ValidateAsync(ValidationContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return default;
        if (httpContext.WebSockets.IsWebSocketRequest)
            return default;
        if (HttpMethods.IsGet(httpContext.Request.Method))
            return new(_getNodeVisitor);
        if (HttpMethods.IsPost(httpContext.Request.Method))
            return new(_postNodeVisitor);
        return default;
    }

    private static readonly INodeVisitor _getNodeVisitor = new MatchingNodeVisitor<GraphQLOperationDefinition>(
        (node, context) => {
            if (context.Operation == node && node.Operation != OperationType.Query)
                context.ReportError(new OperationTypeValidationError(context.Document.Source, node, "Only query operations allowed for GET requests."));
        });

    private static readonly INodeVisitor _postNodeVisitor = new MatchingNodeVisitor<GraphQLOperationDefinition>(
        (node, context) => {
            if (context.Operation == node && node.Operation == OperationType.Subscription)
                context.ReportError(new OperationTypeValidationError(context.Document.Source, node, "Subscription operations are not supported for POST requests."));
        });
}
