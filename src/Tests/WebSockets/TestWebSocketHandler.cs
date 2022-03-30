using System.Net.WebSockets;
using Microsoft.Extensions.Hosting;

namespace Tests.WebSockets;

public class TestWebSocketHandler : WebSocketHandler
{
    public TestWebSocketHandler(
        IGraphQLSerializer serializer,
        IDocumentExecuter executer,
        IServiceScopeFactory serviceScopeFactory,
        WebSocketHandlerOptions webSocketHandlerOptions,
        IHostApplicationLifetime hostApplicationLifetime)
        : base(serializer, executer, serviceScopeFactory, webSocketHandlerOptions, hostApplicationLifetime)
    {
    }

    public IOperationMessageProcessor Do_CreateReceiveStream(IWebSocketConnection sendStream, string subProtocol, IDictionary<string, object?> userContext)
        => CreateReceiveStream(sendStream, subProtocol, userContext);

    public IWebSocketConnection Do_CreateWebSocketConnection(WebSocket webSocket, CancellationToken cancellationToken)
        => CreateWebSocketConnection(webSocket, cancellationToken);
}
