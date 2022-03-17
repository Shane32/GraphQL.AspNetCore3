namespace Shane32.GraphQL.AspNetCore;

/// <summary>
/// Creates a user context from a <see cref="HttpContext"/>.
/// <br/><br/>
/// The generated user context may be used one or more times while executing
/// a GraphQL request or subscription.
/// </summary>
public interface IUserContextBuilder
{
    /// <inheritdoc cref="IUserContextBuilder"/>
    Task<IDictionary<string, object?>> BuildUserContextAsync(HttpContext context);
}
