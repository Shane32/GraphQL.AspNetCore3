using System.Net.WebSockets;
using Microsoft.Extensions.Hosting;

namespace Tests.WebSockets;

public class TestWebSocketHandler : WebSocketHandler
{
    public TestWebSocketHandler(
        IGraphQLSerializer serializer,
        IDocumentExecuter executer,
        IServiceScopeFactory serviceScopeFactory,
        GraphQLHttpMiddlewareOptions options,
        IHostApplicationLifetime hostApplicationLifetime)
        : base(serializer, executer, serviceScopeFactory, options, hostApplicationLifetime)
    {
    }

    public IOperationMessageProcessor Do_CreateReceiveStream(IWebSocketConnection sendStream, string subProtocol, IUserContextBuilder userContextBuilder)
        => CreateReceiveStream(sendStream, subProtocol, userContextBuilder);

    public IWebSocketConnection Do_CreateWebSocketConnection(HttpContext httpContext, WebSocket webSocket, CancellationToken cancellationToken)
        => CreateWebSocketConnection(httpContext, webSocket, cancellationToken);
}
