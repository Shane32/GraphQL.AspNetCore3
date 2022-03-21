namespace Shane32.GraphQL.AspNetCore.WebSockets;

/// <summary>
/// Configuration options for a WebSocket connection.
/// </summary>
public class WebSocketHandlerOptions
{
    /// <summary>
    /// The amount of time to wait for a GraphQL initialization packet before the connection is closed.
    /// The default is 10 seconds.
    /// </summary>
    public TimeSpan? ConnectionInitWaitTimeout { get; set; }

    /// <summary>
    /// The amount of time to wait between sending keep-alive packets.
    /// The default is 30 seconds.
    /// </summary>
    public TimeSpan? KeepAliveTimeout { get; set; }

    /// <summary>
    /// The amount of time to wait to attempt a graceful teardown of the WebSockets protocol.
    /// The default is 10 seconds.
    /// </summary>
    public TimeSpan? DisconnectionTimeout { get; set; }

    /// <summary>
    /// Disconnects a subscription from the client if the subscription source dispatches an
    /// <see cref="IObserver{T}.OnError(Exception)"/> event.
    /// </summary>
    public bool DisconnectAfterErrorEvent { get; set; } = true;

    /// <summary>
    /// Disconnects a subscription from the client there are any GraphQL errors during a subscription.
    /// </summary>
    public bool DisconnectAfterAnyError { get; set; } = false;
}
