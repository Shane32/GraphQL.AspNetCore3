namespace GraphQL.AspNetCore3.WebSockets.GraphQLWs;

/// <inheritdoc/>
public class SubscriptionServer : BaseSubscriptionServer
{
    /// <summary>
    /// The WebSocket sub-protocol used for this protocol.
    /// </summary>
    public const string SubProtocol = "graphql-transport-ws";

    /// <summary>
    /// Returns the <see cref="IDocumentExecuter"/> used to execute requests.
    /// </summary>
    protected IDocumentExecuter DocumentExecuter { get; }

    /// <summary>
    /// Returns the <see cref="IServiceScopeFactory"/> used to create a service scope for request execution.
    /// </summary>
    protected IServiceScopeFactory ServiceScopeFactory { get; }

    /// <summary>
    /// Returns the user context used to execute requests.
    /// </summary>
    protected IDictionary<string, object?> UserContext { get; }

    /// <summary>
    /// Returns the <see cref="IGraphQLSerializer"/> used to deserialize <see cref="OperationMessage"/> payloads.
    /// </summary>
    protected IGraphQLSerializer Serializer { get; }

    /// <summary>
    /// Initailizes a new instance with the specified parameters.
    /// </summary>
    /// <param name="sendStream">The WebSockets stream used to send data packets or close the connection.</param>
    /// <param name="options">Configuration options for this instance.</param>
    /// <param name="executer">The <see cref="IDocumentExecuter"/> to use to execute GraphQL requests.</param>
    /// <param name="serializer">The <see cref="IGraphQLSerializer"/> to use to deserialize payloads stored within <see cref="OperationMessage.Payload"/>.</param>
    /// <param name="serviceScopeFactory">A <see cref="IServiceScopeFactory"/> to create service scopes for execution of GraphQL requests.</param>
    /// <param name="userContext">The user context to pass to the <see cref="IDocumentExecuter"/>.</param>
    /// <param name="authorizationService">An optional service to authorize connections.</param>
    public SubscriptionServer(
        IWebSocketConnection sendStream,
        WebSocketHandlerOptions options,
        IDocumentExecuter executer,
        IGraphQLSerializer serializer,
        IServiceScopeFactory serviceScopeFactory,
        IDictionary<string, object?> userContext,
        IWebSocketAuthorizationService? authorizationService = null)
        : base(sendStream, options, authorizationService)
    {
        DocumentExecuter = executer ?? throw new ArgumentNullException(nameof(executer));
        ServiceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        UserContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <inheritdoc/>
    public override async Task OnMessageReceivedAsync(OperationMessage message)
    {
        if (message.Type == MessageType.Ping) {
            await OnPingAsync(message);
            return;
        } else if (message.Type == MessageType.Pong) {
            await OnPongAsync(message);
            return;
        } else if (message.Type == MessageType.ConnectionInit) {
            if (Initialized) {
                await ErrorTooManyInitializationRequestsAsync(message);
            } else {
                await OnConnectionInitAsync(message, true);
            }
            return;
        }
        if (!Initialized) {
            await ErrorNotInitializedAsync(message);
            return;
        }
        switch (message.Type) {
            case MessageType.Subscribe:
                await OnSubscribeAsync(message);
                break;
            case MessageType.Complete:
                await OnCompleteAsync(message);
                break;
            default:
                await ErrorUnrecognizedMessageAsync(message);
                break;
        }
    }

    /// <summary>
    /// GQL_PONG is a requrired response to a ping, and also a unidirectional keep-alive packet,
    /// whereas GQL_PING is a bidirectional keep-alive packet.
    /// </summary>
    private static readonly OperationMessage _pongMessage = new() { Type = MessageType.Pong };

    /// <summary>
    /// Executes when a ping message is received.
    /// </summary>
    protected virtual Task OnPingAsync(OperationMessage message)
        => Client.SendMessageAsync(_pongMessage);

    /// <summary>
    /// Executes when a pong message is received.
    /// </summary>
    protected virtual Task OnPongAsync(OperationMessage message)
        => Task.CompletedTask;

    /// <inheritdoc/>
    protected override Task OnSendKeepAliveAsync()
        => Client.SendMessageAsync(_pongMessage);

    private static readonly OperationMessage _connectionAckMessage = new() { Type = MessageType.ConnectionAck };
    /// <inheritdoc/>
    protected override Task OnConnectionAcknowledgeAsync(OperationMessage message)
        => Client.SendMessageAsync(_connectionAckMessage);

    /// <summary>
    /// Executes when a request is received to start a subscription.
    /// </summary>
    protected virtual Task OnSubscribeAsync(OperationMessage message)
        => SubscribeAsync(message, false);

    /// <summary>
    /// Executes when a request is received to stop a subscription.
    /// </summary>
    protected virtual Task OnCompleteAsync(OperationMessage message)
        => UnsubscribeAsync(message.Id);

    /// <inheritdoc/>
    protected override async Task SendErrorResultAsync(string id, ExecutionResult result)
    {
        if (Subscriptions.TryRemove(id)) {
            await Client.SendMessageAsync(new OperationMessage {
                Id = id,
                Type = MessageType.Error,
                Payload = result.Errors?.ToArray() ?? Array.Empty<ExecutionError>(),
            });
        }
    }

    /// <inheritdoc/>
    protected override async Task SendDataAsync(string id, ExecutionResult result)
    {
        if (Subscriptions.Contains(id)) {
            await Client.SendMessageAsync(new OperationMessage {
                Id = id,
                Type = MessageType.Next,
                Payload = result,
            });
        }
    }

    /// <inheritdoc/>
    protected override async Task SendCompletedAsync(string id)
    {
        if (Subscriptions.TryRemove(id)) {
            await Client.SendMessageAsync(new OperationMessage {
                Id = id,
                Type = MessageType.Complete,
            });
        }
    }

    /// <inheritdoc/>
    protected override async Task<ExecutionResult> ExecuteRequestAsync(OperationMessage message)
    {
        var request = Serializer.ReadNode<GraphQLRequest>(message.Payload)!;
        using var scope = ServiceScopeFactory.CreateScope();
        return await DocumentExecuter.ExecuteAsync(new ExecutionOptions {
            Query = request.Query,
            Variables = request.Variables,
            Extensions = request.Extensions,
            OperationName = request.OperationName,
            UserContext = UserContext,
            RequestServices = scope.ServiceProvider,
            CancellationToken = CancellationToken,
        });
    }
}
