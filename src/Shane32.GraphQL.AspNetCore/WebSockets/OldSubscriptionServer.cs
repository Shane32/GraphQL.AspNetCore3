namespace Shane32.GraphQL.AspNetCore.WebSockets;

/// <inheritdoc/>
public class OldSubscriptionServer : BaseSubscriptionServer
{
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
    public OldSubscriptionServer(
        IWebSocketConnection sendStream,
        WebSocketHandlerOptions options,
        IDocumentExecuter executer,
        IGraphQLSerializer serializer,
        IServiceScopeFactory serviceScopeFactory,
        IDictionary<string, object?> userContext)
        : base(sendStream, options)
    {
        DocumentExecuter = executer ?? throw new ArgumentNullException(nameof(executer));
        ServiceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        UserContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <inheritdoc/>
    public override async Task OnMessageReceivedAsync(OperationMessage message)
    {
        if (message.Type == OldMessageType.GQL_CONNECTION_TERMINATE) {
            await OnCloseConnectionAsync();
            return;
        } else if (message.Type == OldMessageType.GQL_CONNECTION_INIT) {
            if (!TryInitialize()) {
                await ErrorTooManyInitializationRequestsAsync(message);
            } else {
                await OnConnectionInitAsync(message, false);
            }
            return;
        }
        if (!Initialized) {
            await ErrorNotInitializedAsync(message);
            return;
        }
        switch (message.Type) {
            case OldMessageType.GQL_START:
                await OnStartAsync(message);
                break;
            case OldMessageType.GQL_STOP:
                await OnStopAsync(message);
                break;
            default:
                await ErrorUnrecognizedMessageAsync(message);
                break;
        }
    }

    private static readonly OperationMessage _keepAliveMessage = new() { Type = OldMessageType.GQL_CONNECTION_KEEP_ALIVE };
    /// <inheritdoc/>
    protected override Task OnSendKeepAliveAsync()
        => Client.SendMessageAsync(_keepAliveMessage);

    private static readonly OperationMessage _connectionAckMessage = new() { Type = OldMessageType.GQL_CONNECTION_ACK };
    /// <inheritdoc/>
    protected override Task OnConnectionAcknowledgeAsync(OperationMessage message)
        => Client.SendMessageAsync(_connectionAckMessage);

    /// <summary>
    /// Executes when a request is received to start a subscription.
    /// </summary>
    protected virtual Task OnStartAsync(OperationMessage message)
        => SubscribeAsync(message, true);

    /// <summary>
    /// Executes when a request is received to stop a subscription.
    /// </summary>
    protected virtual Task OnStopAsync(OperationMessage message)
        => UnsubscribeAsync(message.Id);

    /// <inheritdoc/>
    protected override async Task SendErrorResultAsync(string id, ExecutionResult result)
    {
        if (Subscriptions.TryRemove(id)) {
            await Client.SendMessageAsync(new OperationMessage {
                Id = id,
                Type = OldMessageType.GQL_ERROR,
                Payload = result,
            });
        }
    }

    /// <inheritdoc/>
    protected override async Task SendDataAsync(string id, ExecutionResult result)
    {
        if (Subscriptions.Contains(id)) {
            await Client.SendMessageAsync(new OperationMessage {
                Id = id,
                Type = OldMessageType.GQL_DATA,
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
                Type = OldMessageType.GQL_COMPLETE,
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
