namespace Shane32.GraphQL.AspNetCore.WebSockets;

/// <inheritdoc cref="WebSocketHandler"/>
public class WebSocketHandler<TSchema> : WebSocketHandler, IWebSocketHandler<TSchema>
    where TSchema : ISchema
{
    /// <inheritdoc cref="WebSocketHandler.WebSocketHandler(IGraphQLSerializer, IDocumentExecuter, IServiceScopeFactory, WebSocketHandlerOptions)"/>
    public WebSocketHandler(
        IGraphQLSerializer serializer,
        IDocumentExecuter<TSchema> executer,
        IServiceScopeFactory serviceScopeFactory,
        WebSocketHandlerOptions options)
        : base(serializer, executer, serviceScopeFactory, options)
    {
    }
}

/// <inheritdoc cref="IWebSocketHandler"/>
public class WebSocketHandler : IWebSocketHandler
{
    private readonly IGraphQLSerializer _serializer;
    private readonly IDocumentExecuter _executer;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Gets the configuration options for this instance.
    /// </summary>
    protected WebSocketHandlerOptions Options { get; }

    private static readonly IEnumerable<string> _supportedSubProtocols = new List<string>(new[] { "graphql-transport-ws", "graphql-ws" }).AsReadOnly();
    /// <inheritdoc/>
    public virtual IEnumerable<string> SupportedSubProtocols => _supportedSubProtocols;

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    /// <param name="serializer">The <see cref="IGraphQLSerializer"/> instance used to serialize and deserialize <see cref="OperationMessage"/> messages.</param>
    /// <param name="executer">The <see cref="IDocumentExecuter"/> instance used to execute GraphQL requests.</param>
    /// <param name="serviceScopeFactory">The service scope factory used to create a dependency injection service scope for each request.</param>
    /// <param name="webSocketHandlerOptions">Configuration options for the WebSocket connections.</param>
    public WebSocketHandler(
        IGraphQLSerializer serializer,
        IDocumentExecuter executer,
        IServiceScopeFactory serviceScopeFactory,
        WebSocketHandlerOptions webSocketHandlerOptions)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _executer = executer ?? throw new ArgumentNullException(nameof(executer));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        Options = webSocketHandlerOptions ?? throw new ArgumentNullException(nameof(webSocketHandlerOptions));
    }

    /// <inheritdoc/>
    public virtual async Task ExecuteAsync(HttpContext httpContext, WebSocket webSocket, string subProtocol, IDictionary<string, object?> userContext)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));
        if (webSocket == null)
            throw new ArgumentNullException(nameof(webSocket));
        if (userContext == null)
            throw new ArgumentNullException(nameof(userContext));
        System.Diagnostics.Debug.WriteLine($"WebSocket connection over protocol {subProtocol}");
        var webSocketConnection = new WebSocketConnection(webSocket, _serializer, Options, httpContext.RequestAborted);
        using var operationMessageReceiveStream = CreateSendStream(webSocketConnection, subProtocol, userContext);
        System.Diagnostics.Debug.WriteLine($"Starting WebSocket connection message handler");
        await webSocketConnection.ExecuteAsync(operationMessageReceiveStream);
        System.Diagnostics.Debug.WriteLine($"Finished WebSocket connection");
    }

    /// <summary>
    /// Builds an <see cref="IOperationMessageReceiveStream"/> for the specified sub-protocol.
    /// </summary>
    protected virtual IOperationMessageReceiveStream CreateSendStream(IOperationMessageSendStream webSocketConnection, string subProtocol, IDictionary<string, object?> userContext)
    {
        switch (subProtocol) {
            case "graphql-transport-ws":
                return new NewSubscriptionServer(
                    webSocketConnection,
                    Options,
                    _executer,
                    _serializer,
                    _serviceScopeFactory,
                    userContext);
            case "graphql-ws":
                return new OldSubscriptionServer(
                    webSocketConnection,
                    Options,
                    _executer,
                    _serializer,
                    _serviceScopeFactory,
                    userContext);
        }
        throw new ArgumentOutOfRangeException(nameof(subProtocol));
    }
}
