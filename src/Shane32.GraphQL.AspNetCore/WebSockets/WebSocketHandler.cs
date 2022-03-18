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
    private readonly WebSocketHandlerOptions _webSocketHandlerOptions;

    private static readonly TimeSpan _defaultConnectionTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _defaultKeepAliveTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan _defaultDisconnectionTimeout = TimeSpan.FromSeconds(10);

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
        _webSocketHandlerOptions = webSocketHandlerOptions ?? throw new ArgumentNullException(nameof(webSocketHandlerOptions));
    }

    private static readonly IEnumerable<string> _supportedSubProtocols = new List<string>(new[] { "graphql-transport-ws", "graphql-ws" }).AsReadOnly();
    /// <inheritdoc/>
    public IEnumerable<string> SupportedSubProtocols => _supportedSubProtocols;

    /// <inheritdoc/>
    public Task ExecuteAsync(HttpContext httpContext, WebSocket webSocket, string subProtocol, IDictionary<string, object?> userContext, CancellationToken cancellationToken)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));
        if (webSocket == null)
            throw new ArgumentNullException(nameof(webSocket));
        if (userContext == null)
            throw new ArgumentNullException(nameof(userContext));
        var webSocketConnection = new WebSocketConnection(webSocket, _serializer, _webSocketHandlerOptions.DisconnectionTimeout ?? _defaultDisconnectionTimeout, cancellationToken);
        IOperationMessageReceiveStream? operationMessageReceiveStream = null;
        try {
            switch (subProtocol) {
                case "graphql-transport-ws":
                    operationMessageReceiveStream = new NewSubscriptionServer(
                        webSocketConnection,
                        _webSocketHandlerOptions.ConnectionInitWaitTimeout ?? _defaultConnectionTimeout,
                        _webSocketHandlerOptions.KeepAliveTimeout ?? _defaultKeepAliveTimeout,
                        _executer,
                        _serializer,
                        _serviceScopeFactory,
                        userContext);
                    break;
                case "graphql-ws":
                    operationMessageReceiveStream = new OldSubscriptionServer(
                        webSocketConnection,
                        _webSocketHandlerOptions.ConnectionInitWaitTimeout ?? _defaultConnectionTimeout,
                        _webSocketHandlerOptions.KeepAliveTimeout ?? _defaultKeepAliveTimeout,
                        _executer,
                        _serializer,
                        _serviceScopeFactory,
                        userContext);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(subProtocol));
            }
            return webSocketConnection.ExecuteAsync(operationMessageReceiveStream);
        } finally {
            operationMessageReceiveStream?.Dispose();
        }
    }
}
