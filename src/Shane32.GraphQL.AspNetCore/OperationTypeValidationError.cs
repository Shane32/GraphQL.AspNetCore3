namespace Shane32.GraphQL.AspNetCore;

/// <summary>
/// Represents a validation error indicating that the requested operation is not valid
/// for the type of HTTP request.
/// </summary>
public class OperationTypeValidationError : ValidationError
{
    /// <inheritdoc cref="OperationTypeValidationError"/>
    public OperationTypeValidationError(GraphQLParser.ROM originalQuery, ASTNode node, string message)
        : base(originalQuery, null!, message, node)
    {
    }
}
