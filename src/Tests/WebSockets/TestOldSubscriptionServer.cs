using GraphQL.Transport;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.WebSockets;

public class TestOldSubscriptionServer : OldSubscriptionServer
{
    public TestOldSubscriptionServer(IWebSocketConnection sendStream, WebSocketHandlerOptions options,
        IDocumentExecuter executer, IGraphQLSerializer serializer, IServiceScopeFactory serviceScopeFactory,
        IDictionary<string, object?> userContext)
        : base(sendStream, options, executer, serializer, serviceScopeFactory, userContext) { }

    public bool Do_TryInitialize()
        => TryInitialize();

    public Task Do_OnSendKeepAliveAsync()
        => OnSendKeepAliveAsync();

    public Task Do_OnConnectionAcknowledgeAsync(OperationMessage message)
        => OnConnectionAcknowledgeAsync(message);

    public Task Do_OnStart(OperationMessage message)
        => OnStartAsync(message);

    public Task Do_OnStop(OperationMessage message)
        => OnStopAsync(message);

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

    public IGraphQLSerializer Get_Serializer => Serializer;

    public IDictionary<string, object?> Get_UserContext => UserContext;

    public IDocumentExecuter Get_DocumentExecuter => DocumentExecuter;

    public IServiceScopeFactory Get_ServiceScopeFactory => ServiceScopeFactory;
}
