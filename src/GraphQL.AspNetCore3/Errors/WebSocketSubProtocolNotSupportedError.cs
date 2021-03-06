namespace GraphQL.AspNetCore3.Errors;

/// <summary>
/// Represents an error indicating that none of the requested websocket sub-protocols are supported.
/// </summary>
public class WebSocketSubProtocolNotSupportedError : RequestError
{
    /// <inheritdoc cref="WebSocketSubProtocolNotSupportedError"/>
    public WebSocketSubProtocolNotSupportedError(IEnumerable<string> requestedSubProtocols)
        : base($"Invalid WebSocket sub-protocol(s): {string.Join(",", requestedSubProtocols.Select(x => $"'{x}'"))}")
    {
    }
}
