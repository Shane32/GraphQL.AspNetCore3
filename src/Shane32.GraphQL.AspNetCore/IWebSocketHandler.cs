namespace Shane32.GraphQL.AspNetCore;

/// <inheritdoc/>
public interface IWebSocketHandler<TSchema> : IWebSocketHandler
    where TSchema : ISchema
{
}

/// <summary>
/// Handles a WebSocket connection based on the sub-protocol specified.
/// </summary>
public interface IWebSocketHandler
{
    /// <summary>
    /// Executes a specified WebSocket request, returning once the connection is closed.
    /// </summary>
    Task ExecuteAsync(HttpContext httpContext, WebSocket webSocket, string subProtocol, IDictionary<string, object?> userContext, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a list of supported WebSocket sub-protocols.
    /// </summary>
    IEnumerable<string> SupportedSubProtocols { get; }
}
