using System.Net.WebSockets;
using GraphQL.Transport;

namespace Tests.WebSockets;

public class TestWebSocketConnection : WebSocketConnection
{
    public TestWebSocketConnection(
        WebSocket webSocket,
        IGraphQLSerializer serializer,
        WebSocketHandlerOptions options,
        CancellationToken cancellationToken)
        : base(webSocket, serializer, options, cancellationToken)
    {
    }

    public Task Do_OnDispatchMessageAsync(IOperationMessageProcessor operationMessageReceiveStream, OperationMessage message)
        => OnDispatchMessageAsync(operationMessageReceiveStream, message);

    public Task Do_OnSendMessageAsync(OperationMessage message)
        => OnSendMessageAsync(message);

    public Task Do_OnCloseOutputAsync(WebSocketCloseStatus closeStatus, string? closeDescription)
        => OnCloseOutputAsync(closeStatus, closeDescription);

    public TimeSpan Get_DefaultDisconnectionTimeout
        => DefaultDisconnectionTimeout;
}
