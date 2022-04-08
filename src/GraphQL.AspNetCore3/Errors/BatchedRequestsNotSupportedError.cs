namespace GraphQL.AspNetCore3.Errors;

/// <summary>
/// Represents an error indicating that batched requests are not supported.
/// </summary>
public class BatchedRequestsNotSupportedError : RequestError
{
    /// <inheritdoc cref="BatchedRequestsNotSupportedError"/>
    public BatchedRequestsNotSupportedError() : base("Batched requests are not supported.") { }
}
