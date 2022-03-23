using GraphQL.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.WebSockets
{
    public class TestNewSubscriptionServer : NewSubscriptionServer
    {
        public TestNewSubscriptionServer(IOperationMessageSendStream sendStream, WebSocketHandlerOptions options,
            IDocumentExecuter executer, IGraphQLSerializer serializer, IServiceScopeFactory serviceScopeFactory,
            IDictionary<string, object?> userContext)
            : base(sendStream, options, executer, serializer, serviceScopeFactory, userContext) { }

        public bool Do_TryInitialize()
            => TryInitialize();

        public Task Do_OnPingAsync(OperationMessage message)
            => OnPingAsync(message);

        public Task Do_OnPongAsync(OperationMessage message)
            => OnPongAsync(message);

        public Task Do_OnSendKeepAliveAsync()
            => OnSendKeepAliveAsync();

        public Task Do_OnConnectionAcknowledgeAsync(OperationMessage message)
            => OnConnectionAcknowledgeAsync(message);

        public Task Do_OnSubscribe(OperationMessage message)
            => OnSubscribeAsync(message);

        public Task Do_OnComplete(OperationMessage message)
            => OnCompleteAsync(message);

        public Task Do_SendErrorResultAsync(string id, ExecutionResult result)
            => SendErrorResultAsync(id, result);

        public Task Do_SendDataAsync(string id, ExecutionResult result)
            => SendDataAsync(id, result);

        public Task Do_SendCompletedAsync(string id)
            => SendCompletedAsync(id);

        public Task<ExecutionResult> Do_ExecuteRequestAsync(OperationMessage message)
            => ExecuteRequestAsync(message);

        public SubscriptionList Get_Subscriptions
            => Subscriptions;
    }
}
