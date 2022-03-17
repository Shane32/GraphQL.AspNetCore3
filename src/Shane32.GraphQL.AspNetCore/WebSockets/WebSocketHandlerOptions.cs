namespace Shane32.GraphQL.AspNetCore.WebSockets;

/// <summary>
/// Configuration options for a WebSocket connection.
/// </summary>
public class WebSocketHandlerOptions
{
    /// <summary>
    /// The amount of time to wait for a GraphQL initialization packet before the connection is closed.
    /// </summary>
    public TimeSpan ConnectionInitWaitTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// The amount of time to wait between sending keep-alive packets.
    /// </summary>
    public TimeSpan KeepAliveTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
