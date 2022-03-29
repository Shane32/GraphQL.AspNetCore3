namespace Shane32.GraphQL.AspNetCore.WebSockets;

/// <inheritdoc cref="WebSocketHandler"/>
public class WebSocketHandler<TSchema> : WebSocketHandler, IWebSocketHandler<TSchema>
    where TSchema : ISchema
{
    /// <inheritdoc cref="WebSocketHandler(IGraphQLSerializer, IDocumentExecuter, IServiceScopeFactory, WebSocketHandlerOptions, IHostApplicationLifetime)"/>
    public WebSocketHandler(
        IGraphQLSerializer serializer,
        IDocumentExecuter<TSchema> executer,
        IServiceScopeFactory serviceScopeFactory,
        WebSocketHandlerOptions options,
        IHostApplicationLifetime hostApplicationLifetime)
        : base(serializer, executer, serviceScopeFactory, options, hostApplicationLifetime)
    {
    }
}

/// <inheritdoc cref="IWebSocketHandler"/>
public class WebSocketHandler : IWebSocketHandler
{
    private readonly IGraphQLSerializer _serializer;
    private readonly IDocumentExecuter _executer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;

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
    /// <param name="hostApplicationLifetime">The <see cref="IHostApplicationLifetime"/> instance that signals when the application is shutting down.</param>
    public WebSocketHandler(
        IGraphQLSerializer serializer,
        IDocumentExecuter executer,
        IServiceScopeFactory serviceScopeFactory,
        WebSocketHandlerOptions webSocketHandlerOptions,
        IHostApplicationLifetime hostApplicationLifetime)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _executer = executer ?? throw new ArgumentNullException(nameof(executer));
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        Options = webSocketHandlerOptions ?? throw new ArgumentNullException(nameof(webSocketHandlerOptions));
        _hostApplicationLifetime = hostApplicationLifetime ?? throw new ArgumentNullException(nameof(hostApplicationLifetime));
    }

    /// <inheritdoc/>
    public virtual async Task ExecuteAsync(HttpContext httpContext, WebSocket webSocket, string subProtocol, IDictionary<string, object?> userContext)
    {
        if (httpContext == null)
            throw new ArgumentNullException(nameof(httpContext));
        if (webSocket == null)
            throw new ArgumentNullException(nameof(webSocket));
        if (subProtocol == null)
            throw new ArgumentNullException(nameof(subProtocol));
        if (userContext == null)
            throw new ArgumentNullException(nameof(userContext));
        var appStoppingToken = _hostApplicationLifetime.ApplicationStopping;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted, appStoppingToken);
        if (cts.Token.IsCancellationRequested)
            return;
        try {
            var webSocketConnection = CreateWebSocketConnection(webSocket, cts.Token);
            using var operationMessageReceiveStream = CreateReceiveStream(webSocketConnection, subProtocol, userContext);
            await webSocketConnection.ExecuteAsync(operationMessageReceiveStream);
        } catch (OperationCanceledException) when (appStoppingToken.IsCancellationRequested) {
            // terminate all pending WebSockets connections when the application is in the process of stopping
        }
    }

    /// <summary>
    /// Creates an <see cref="IWebSocketConnection"/>, a WebSocket message pump.
    /// </summary>
    protected virtual IWebSocketConnection CreateWebSocketConnection(WebSocket webSocket, CancellationToken cancellationToken)
        => new WebSocketConnection(webSocket, _serializer, Options, cancellationToken);

    /// <summary>
    /// Builds an <see cref="IOperationMessageReceiveStream"/> for the specified sub-protocol.
    /// </summary>
    protected virtual IOperationMessageReceiveStream CreateReceiveStream(IOperationMessageSendStream webSocketConnection, string subProtocol, IDictionary<string, object?> userContext)
    {
        switch (subProtocol) {
            case "graphql-transport-ws": {
                var server = new NewSubscriptionServer(
                    webSocketConnection,
                    Options,
                    _executer,
                    _serializer,
                    _serviceScopeFactory,
                    userContext);
                server.StartConnectionInitTimer();
                return server;
            }
            case "graphql-ws": {
                var server = new OldSubscriptionServer(
                    webSocketConnection,
                    Options,
                    _executer,
                    _serializer,
                    _serviceScopeFactory,
                    userContext);
                server.StartConnectionInitTimer();
                return server;
            }
        }
        throw new ArgumentOutOfRangeException(nameof(subProtocol));
    }
}
