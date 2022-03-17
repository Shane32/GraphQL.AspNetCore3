namespace Shane32.GraphQL.AspNetCore;

/// <summary>
/// Validates that HTTP GET requests only execute queries; not mutations or subscriptions.
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
        if (httpContext != null) {
            if (httpContext.Request.Method == HttpMethod.Get.Method) {
                return new(_getNodeVisitor);
            }
        }
        return default;
    }

    private static readonly INodeVisitor _getNodeVisitor = new MatchingNodeVisitor<GraphQLOperationDefinition>(
        (node, context) => {
            if (node.Operation != OperationType.Query)
                context.ReportError(new InvalidOperationValidationError(context.Document.Source, node));
        });

    private class InvalidOperationValidationError : ValidationError
    {
        public InvalidOperationValidationError(GraphQLParser.ROM originalQuery, ASTNode node)
            : base(originalQuery, null!, "Only query operations allowed for GET requests.", node)
        {
        }
    }
}
