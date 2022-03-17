namespace Shane32.GraphQL.AspNetCore;

/// <summary>
/// Represents a user context builder based on a delegate.
/// </summary>
public class UserContextBuilder<TUserContext> : IUserContextBuilder
    where TUserContext : IDictionary<string, object?>
{
    private readonly Func<HttpContext, Task<TUserContext>> _func;

    /// <summary>
    /// Initializes a new instance with the specified delegate.
    /// </summary>
    public UserContextBuilder(Func<HttpContext, Task<TUserContext>> func)
    {
        _func = func ?? throw new ArgumentNullException(nameof(func));
    }

    /// <summary>
    /// Initializes a new instance with the specified delegate.
    /// </summary>
    public UserContextBuilder(Func<HttpContext, TUserContext> func)
    {
        if (func == null)
            throw new ArgumentNullException(nameof(func));

        _func = x => Task.FromResult(func(x));
    }

    /// <inheritdoc/>
    public async Task<IDictionary<string, object?>> BuildUserContextAsync(HttpContext httpContext)
        => await _func(httpContext);
}
