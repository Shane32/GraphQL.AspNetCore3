namespace GraphQL.AspNetCore3.WebSockets;

/// <summary>
/// Configuration options for a WebSocket connection.
/// </summary>
public class GraphQLWebSocketOptions
{
    /// <summary>
    /// The amount of time to wait for a GraphQL initialization packet before the connection is closed.
    /// The default is 10 seconds.
    /// </summary>
    public TimeSpan? ConnectionInitWaitTimeout { get; set; }

    /// <summary>
    /// The amount of time to wait between sending keep-alive packets.
    /// The default is disabled.
    /// <br/><br/>
    /// Keep in mind that the 'subscription-transport-ws' implementation typically
    /// disconnects clients if a keep-alive packet for more than 20 seconds, when
    /// keep-alive packets are enabled, so it is recommended to keep the keep-alive
    /// packets disabled or use a value less than 20 seconds.
    /// </summary>
    public TimeSpan? KeepAliveTimeout { get; set; }

    /// <summary>
    /// Gets or sets the keep-alive mode used for websocket subscriptions.
    /// This property is only applicable when using the GraphQLWs protocol.
    /// </summary>
    public KeepAliveMode KeepAliveMode { get; set; } = KeepAliveMode.Default;

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
    public bool DisconnectAfterAnyError { get; set; }

    /// <summary>
    /// The list of supported WebSocket sub-protocols.
    /// Defaults to <see cref="GraphQLWs.SubscriptionServer.SubProtocol"/> and <see cref="SubscriptionsTransportWs.SubscriptionServer.SubProtocol"/>.
    /// Adding other sub-protocols require the <see cref="GraphQLHttpMiddleware.CreateMessageProcessor(IWebSocketConnection, string)"/> method
    /// to be overridden to handle the new sub-protocol.
    /// </summary>
    /// <remarks>
    /// When the <see cref="KeepAliveMode"/> is set to <see cref="KeepAliveMode.TimeoutWithPayload"/>, you may wish to remove
    /// <see cref="SubscriptionsTransportWs.SubscriptionServer.SubProtocol"/> from this list to prevent clients from using
    /// protocols which do not support the <see cref="KeepAliveMode.TimeoutWithPayload"/> keep-alive mode.
    /// </remarks>
    public List<string> SupportedWebSocketSubProtocols { get; set; } = [GraphQLWs.SubscriptionServer.SubProtocol, SubscriptionsTransportWs.SubscriptionServer.SubProtocol];
}
