namespace Shane32.GraphQL.AspNetCore.WebSockets;

/// <summary>
/// Manages a WebSocket message stream.
/// </summary>
public abstract class BaseSubscriptionServer : IOperationMessageReceiveStream
{
    private volatile int _initialized = 0;
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly WebSocketHandlerOptions _options;

    /// <summary>
    /// Returns a <see cref="IOperationMessageSendStream"/> instance that can be used
    /// to send messages to the client.
    /// </summary>
    protected IOperationMessageSendStream Client { get; }

    /// <summary>
    /// Returns a <see cref="System.Threading.CancellationToken"/> that is signaled
    /// when the WebSockets connection is closed.
    /// </summary>
    protected CancellationToken CancellationToken { get; }

    /// <summary>
    /// Returns a synchronized list of subscriptions.
    /// </summary>
    protected SubscriptionList Subscriptions { get; }

    private static readonly TimeSpan _defaultKeepAliveTimeout = Timeout.InfiniteTimeSpan;
    private static readonly TimeSpan _defaultConnectionTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Initailizes a new instance with the specified parameters.
    /// </summary>
    /// <param name="sendStream">The WebSockets stream used to send data packets or close the connection.</param>
    /// <param name="options">Configuration options for this instance</param>
    public BaseSubscriptionServer(
        IOperationMessageSendStream sendStream,
        WebSocketHandlerOptions options)
    {
        if (options.ConnectionInitWaitTimeout.HasValue) {
            if (options.ConnectionInitWaitTimeout.Value != Timeout.InfiniteTimeSpan && options.ConnectionInitWaitTimeout.Value <= TimeSpan.Zero || options.ConnectionInitWaitTimeout.Value > TimeSpan.FromSeconds(int.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(options) + "." + nameof(WebSocketHandlerOptions.ConnectionInitWaitTimeout));
        }
        if (options.KeepAliveTimeout.HasValue) {
            if (options.KeepAliveTimeout.Value != Timeout.InfiniteTimeSpan && options.KeepAliveTimeout.Value <= TimeSpan.Zero || options.KeepAliveTimeout.Value > TimeSpan.FromSeconds(int.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(options) + "." + nameof(WebSocketHandlerOptions.KeepAliveTimeout));
        }
        _options = options;
        Client = sendStream ?? throw new ArgumentNullException(nameof(sendStream));
        _cancellationTokenSource = new();
        CancellationToken = _cancellationTokenSource.Token;
        Subscriptions = new(CancellationToken);
    }

    /// <inheritdoc/>
    public void StartConnectionInitTimer()
    {
        var connectInitWaitTimeout = _options.ConnectionInitWaitTimeout ?? _defaultConnectionTimeout;
        if (connectInitWaitTimeout != Timeout.InfiniteTimeSpan) {
            _ = Task.Run(async () => {
                await Task.Delay(connectInitWaitTimeout, CancellationToken);
                if (_initialized == 0)
                    await OnConnectionInitWaitTimeoutAsync();
            });
        }
    }

    /// <summary>
    /// Executes once the initialization timeout has expired without being initialized.
    /// </summary>
    protected virtual Task OnConnectionInitWaitTimeoutAsync()
        => ErrorConnectionInitializationTimeoutAsync();

    /// <summary>
    /// Called when the WebSocket connection (not necessarily the HTTP connection) has been terminated.
    /// Disposes of all active subscriptions, cancels all existing requests,
    /// and prevents any further responses.
    /// </summary>
    public virtual void Dispose()
    {
        var cts = Interlocked.Exchange(ref _cancellationTokenSource, null);
        if (cts != null) {
            cts.Cancel();
            cts.Dispose();
            Subscriptions.Dispose(); //redundant
        }
    }

    /// <summary>
    /// Indicates if the connection has been initialized yet.
    /// </summary>
    protected bool Initialized
        => _initialized == 1;

    /// <summary>
    /// Sets the initialized flag if it has not already been set.
    /// Returns <see langword="false"/> if it was already set.
    /// </summary>
    protected bool TryInitialize()
        => Interlocked.Exchange(ref _initialized, 1) == 0;

    /// <summary>
    /// Executes when a message has been received from the client.
    /// </summary>
    /// <exception cref="OperationCanceledException"/>
    public abstract Task OnMessageReceivedAsync(OperationMessage message);

    /// <summary>
    /// Executes upon a request to close the connection from the client.
    /// </summary>
    protected virtual Task OnCloseConnectionAsync()
        => Client.CloseConnectionAsync();

    /// <summary>
    /// Sends a fatal error message indicating that the initialization timeout has expired
    /// without the connection being initialized.
    /// </summary>
    protected virtual Task ErrorConnectionInitializationTimeoutAsync()
        => Client.CloseConnectionAsync(4408, "Connection initialization timeout");

    /// <summary>
    /// Sends a fatal error message indicating that the client attempted to initialize
    /// the connection more than one time.
    /// </summary>
    protected virtual Task ErrorTooManyInitializationRequestsAsync()
        => Client.CloseConnectionAsync(4429, "Too many initialization requests");

    /// <summary>
    /// Sends a fatal error message indicating that the client attempted to subscribe
    /// to an event stream before initialization was complete.
    /// </summary>
    protected virtual Task ErrorNotInitializedAsync()
        => Client.CloseConnectionAsync(4401, "Unauthorized");

    /// <summary>
    /// Sends a fatal error message indicating that the client attempted to use an
    /// unrecognized message type.
    /// </summary>
    protected virtual Task ErrorUnrecognizedMessageAsync()
        => Client.CloseConnectionAsync(4400, "Unrecognized message");

    /// <summary>
    /// Sends a fatal error message indicating that the client attempted to subscribe
    /// to an event stream with an empty id.
    /// </summary>
    protected virtual Task ErrorIdCannotBeBlankAsync()
        => Client.CloseConnectionAsync(4400, "Id cannot be blank");

    /// <summary>
    /// Sends a fatal error message indicating that the client attempted to subscribe
    /// to an event stream with an id that was already in use.
    /// </summary>
    protected virtual Task ErrorIdAlreadyExistsAsync(string id)
        => Client.CloseConnectionAsync(4409, $"Subscriber for {id} already exists");

    /// <summary>
    /// Executes when the client is attempting to initalize the connection.
    /// By default this acknowledges the connection via <see cref="OnConnectionAcknowledgeAsync(OperationMessage)"/>
    /// and then starts sending keep-alive messages via <see cref="OnSendKeepAliveAsync"/> if configured to do so.
    /// Keep-alive messages are only sent if no messages have been sent over the WebSockets connection for the
    /// length of time configured in <see cref="WebSocketHandlerOptions.KeepAliveTimeout"/>.
    /// </summary>
    protected virtual async Task OnConnectionInitAsync(OperationMessage message, bool smartKeepAlive)
    {
        await OnConnectionAcknowledgeAsync(message);

        var keepAliveTimeout = _options.KeepAliveTimeout ?? _defaultKeepAliveTimeout;
        if (keepAliveTimeout > TimeSpan.Zero) {
            if (smartKeepAlive)
                _ = StartSmartKeepAliveLoopAsync();
            else
                _ = StartKeepAliveLoopAsync();
        }

        async Task StartKeepAliveLoopAsync()
        {
            var keepAliveTimeout = _options.KeepAliveTimeout ?? _defaultKeepAliveTimeout;
            while (true) {
                await Task.Delay(keepAliveTimeout, CancellationToken);
                await OnSendKeepAliveAsync();
            }
        }

        /*
         * This 'smart' keep-alive logic doesn't work with subscription-transport-ws because the client will
         * terminate a connection 20 seconds after the last keep-alive was received (or it will never
         * terminate if no keep-alive was ever received).
         * 
         * See: https://github.com/graphql/graphql-playground/issues/1247
         */
        async Task StartSmartKeepAliveLoopAsync()
        {
            var keepAliveTimeout = _options.KeepAliveTimeout ?? _defaultKeepAliveTimeout;
            var lastKeepAliveSent = Client.LastMessageSentAt;
            while (true) {
                var lastSent = Client.LastMessageSentAt;
                var lastComm = lastKeepAliveSent > lastSent ? lastKeepAliveSent : lastSent;
                var now = DateTime.UtcNow;
                var timePassed = now.Subtract(lastComm);
                if (timePassed > keepAliveTimeout) {
                    await OnSendKeepAliveAsync();
                    lastKeepAliveSent = now;
                    await Task.Delay(keepAliveTimeout, CancellationToken);
                } else if (timePassed < keepAliveTimeout) {
                    var timeToWait = keepAliveTimeout - timePassed;
                    await Task.Delay(timeToWait, CancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Executes when a keep-alive message needs to be sent.
    /// </summary>
    protected abstract Task OnSendKeepAliveAsync();

    /// <summary>
    /// Executes when a connection request needs to be acknowledged.
    /// </summary>
    protected abstract Task OnConnectionAcknowledgeAsync(OperationMessage message);

    /// <summary>
    /// Executes when a new subscription request has occurred.
    /// Optionally disconnects any existing subscription associated with the same id.
    /// </summary>
    protected virtual async Task SubscribeAsync(OperationMessage message, bool overwrite)
    {
        if (string.IsNullOrEmpty(message.Id)) {
            await ErrorIdCannotBeBlankAsync();
            return;
        }

        var dummyDisposer = new DummyDisposer();

        try {
            if (overwrite) {
                Subscriptions[message.Id] = dummyDisposer;
            } else {
                if (!Subscriptions.TryAdd(message.Id, dummyDisposer)) {
                    await ErrorIdAlreadyExistsAsync(message.Id);
                    return;
                }
            }

            var result = await ExecuteRequestAsync(message);
            if (!Subscriptions.Contains(message.Id, dummyDisposer))
                return;
            if (result is SubscriptionExecutionResult subscriptionExecutionResult && subscriptionExecutionResult.Streams?.Count == 1) {
                // do not return a result, but set up a subscription
                var stream = subscriptionExecutionResult.Streams!.Single().Value;
                // note that this may immediately trigger some notifications
                var disposer = stream.Subscribe(new Observer(this, message.Id, _options.DisconnectAfterErrorEvent, _options.DisconnectAfterAnyError));
                try {
                    if (Subscriptions.CompareExchange(message.Id, dummyDisposer, disposer)) {
                        disposer = null;
                    }
                } finally {
                    disposer?.Dispose();
                }
            } else if (result.Executed && result.Data != null) {
                await SendSingleResultAsync(message.Id, result);
            } else {
                await SendErrorResultAsync(message.Id, result);
            }
        } catch (OperationCanceledException) when (CancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception ex) {
            if (!Subscriptions.Contains(message.Id, dummyDisposer))
                return;
            var error = await HandleErrorDuringSubscribeAsync(ex);
            await SendErrorResultAsync(message.Id, error);
        }
    }

    /// <summary>
    /// Creates an <see cref="ExecutionError"/> for an unknown <see cref="Exception"/>.
    /// </summary>
    protected virtual Task<ExecutionError> HandleErrorDuringSubscribeAsync(Exception ex)
        => Task.FromResult<ExecutionError>(new UnhandledError("Unable to set up subscription for the requested field.", ex));

    /// <summary>
    /// Sends a single result to the client for a subscription or request, along with a notice
    /// that it was the last result in the event stream.
    /// </summary>
    protected virtual async Task SendSingleResultAsync(string id, ExecutionResult result)
    {
        await SendDataAsync(id, result);
        await SendCompletedAsync(id);
    }

    /// <summary>
    /// Sends an execution error to the client during set-up of a subscription.
    /// </summary>
    protected virtual Task SendErrorResultAsync(string id, ExecutionError error)
        => SendErrorResultAsync(id, new ExecutionResult { Errors = new ExecutionErrors { error } });

    /// <summary>
    /// Sends an error result to the client during set-up of a subscription.
    /// </summary>
    protected abstract Task SendErrorResultAsync(string id, ExecutionResult result);

    /// <summary>
    /// Sends a data packet to the client for a subscription event.
    /// </summary>
    protected abstract Task SendDataAsync(string id, ExecutionResult result);

    /// <summary>
    /// Sends a notice that a subscription has completed and no more data packets will be sent.
    /// </summary>
    protected abstract Task SendCompletedAsync(string id);

    /// <summary>
    /// Executes a GraphQL request. The request is inside <see cref="OperationMessage.Payload"/>
    /// and will need to be deserialized by <see cref="IGraphQLSerializer.ReadNode{T}(object?)"/>
    /// into a <see cref="GraphQLRequest"/> instance.
    /// </summary>
    protected abstract Task<ExecutionResult> ExecuteRequestAsync(OperationMessage message);

    /// <summary>
    /// Unsubscribes from a subscription event stream.
    /// </summary>
    protected virtual Task UnsubscribeAsync(string id)
    {
        Subscriptions.TryRemove(id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Wraps an unhandled exception within an <see cref="ExecutionError"/> instance.
    /// </summary>
    protected virtual Task<ExecutionError> HandleErrorFromSourceAsync(Exception exception)
        => Task.FromResult<ExecutionError>(new UnhandledError("Unhandled exception", exception));

    /// <summary>
    /// Handles messages from the event source.
    /// </summary>
    private class Observer : IObserver<ExecutionResult>
    {
        private readonly BaseSubscriptionServer _handler;
        private readonly string _id;
        private readonly bool _closeAfterOnError;
        private readonly bool _closeAfterAnyError;
        private int _done;

        public Observer(BaseSubscriptionServer handler, string id, bool closeAfterOnError, bool closeAfterAnyError)
        {
            _handler = handler;
            _id = id;
            _closeAfterOnError = closeAfterOnError;
            _closeAfterAnyError = closeAfterAnyError;
        }

        public void OnCompleted()
        {
            if (Interlocked.Exchange(ref _done, 1) == 1)
                return;
            try {
                _ = _handler.SendCompletedAsync(_id);
            } catch { }
        }

        public async void OnError(Exception error)
        {
            if (Interlocked.Exchange(ref _done, 1) == 1)
                return;
            try {
                if (error != null) {
                    var executionError = error is ExecutionError ee ? ee : await _handler.HandleErrorFromSourceAsync(error);
                    if (executionError != null) {
                        var result = new ExecutionResult {
                            Errors = new ExecutionErrors { executionError },
                        };
                        await _handler.SendDataAsync(_id, result);
                    }
                }
            } catch { }
            try {
                if (_closeAfterOnError)
                    await _handler.SendCompletedAsync(_id);
            }
            catch { }
        }

        public async void OnNext(ExecutionResult value)
        {
            if (Interlocked.CompareExchange(ref _done, 0, 0) == 1)
                return;
            if (value == null)
                return;
            try {
                await _handler.SendDataAsync(_id, value);
                if (_closeAfterAnyError && value.Errors != null && value.Errors.Count > 0) {
                    await _handler.SendCompletedAsync(_id);
                }
            } catch { }
        }
    }

    private class DummyDisposer : IDisposable
    {
        public void Dispose() { }
    }
}