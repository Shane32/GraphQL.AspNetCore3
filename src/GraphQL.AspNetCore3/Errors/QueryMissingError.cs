namespace GraphQL.AspNetCore3.Errors;

/// <summary>
/// Represents an error indicating that no GraphQL query was provided to the request.
/// </summary>
public class QueryMissingError : RequestError
{
    /// <inheritdoc cref="QueryMissingError"/>
    public QueryMissingError() : base("GraphQL query is missing.") { }
}