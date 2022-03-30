using System.Net.WebSockets;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tests.WebSockets;

public class WebSocketHandlerTests : IDisposable
{
    private readonly Mock<IGraphQLSerializer> _mockSerializer = new(MockBehavior.Strict);
    private readonly Mock<IDocumentExecuter> _mockExecuter = new(MockBehavior.Strict);
    private readonly Mock<IServiceScopeFactory> _mockScopeFactory = new(MockBehavior.Strict);
    private readonly WebSocketHandlerOptions _options = new();
    private readonly Mock<IHostApplicationLifetime> _mockAppLifetime = new(MockBehavior.Strict);
    private readonly Mock<HttpContext> _mockHttpContext = new(MockBehavior.Strict);
    private readonly Mock<WebSocket> _mockWebSocket = new(MockBehavior.Strict);
    private readonly Mock<IDictionary<string, object?>> _mockUserContext = new(MockBehavior.Strict);
    private IGraphQLSerializer _serializer => _mockSerializer.Object;
    private IDocumentExecuter _executer => _mockExecuter.Object;
    private IServiceScopeFactory _scopeFactory => _mockScopeFactory.Object;
    private IHostApplicationLifetime _appLifetime => _mockAppLifetime.Object;
    private HttpContext _httpContext => _mockHttpContext.Object;
    private WebSocket _webSocket => _mockWebSocket.Object;
    private IDictionary<string, object?> _userContext => _mockUserContext.Object;
    private readonly Mock<TestWebSocketHandler> _mockHandler;
    private TestWebSocketHandler _handler => _mockHandler.Object;

    public WebSocketHandlerTests()
    {
        _mockHandler = new Mock<TestWebSocketHandler>(_serializer, _executer, _scopeFactory, _options, _appLifetime);
        _mockHandler.CallBase = true;
    }

    public void Dispose()
    {
        _mockSerializer.Verify();
        _mockExecuter.Verify();
        _mockScopeFactory.Verify();
        _mockAppLifetime.Verify();
        _mockHttpContext.Verify();
        _mockWebSocket.Verify();
        _mockUserContext.Verify();
    }

    [Fact]
    public void Null_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new WebSocketHandler(
            null!, _executer, _scopeFactory, _options, _appLifetime));
        Should.Throw<ArgumentNullException>(() => new WebSocketHandler(
            _serializer, null!, _scopeFactory, _options, _appLifetime));
        Should.Throw<ArgumentNullException>(() => new WebSocketHandler(
            _serializer, _executer, null!, _options, _appLifetime));
        Should.Throw<ArgumentNullException>(() => new WebSocketHandler(
            _serializer, _executer, _scopeFactory, null!, _appLifetime));
        Should.Throw<ArgumentNullException>(() => new WebSocketHandler(
            _serializer, _executer, _scopeFactory, _options, null!));
        Should.Throw<ArgumentNullException>(() => _handler.ExecuteAsync(
            null!, _webSocket, "", _userContext));
        Should.Throw<ArgumentNullException>(() => _handler.ExecuteAsync(
            _httpContext, null!, "", _userContext));
        Should.Throw<ArgumentNullException>(() => _handler.ExecuteAsync(
            _httpContext, _webSocket, null!, _userContext));
        Should.Throw<ArgumentNullException>(() => _handler.ExecuteAsync(
            _httpContext, _webSocket, "", null!));
    }

    [Fact]
    public async Task ExecuteAsync_CanceledHttpContextReturns()
    {
        _mockAppLifetime.Setup(x => x.ApplicationStopping).Returns(default(CancellationToken)).Verifiable();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _mockHttpContext.Setup(x => x.RequestAborted).Returns(cts.Token).Verifiable();
        await _handler.ExecuteAsync(_httpContext, _webSocket, "", _userContext);
    }

    [Fact]
    public async Task ExecuteAsync_CanceledAppContextReturns()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();
        _mockAppLifetime.Setup(x => x.ApplicationStopping).Returns(cts.Token).Verifiable();
        _mockHttpContext.Setup(x => x.RequestAborted).Returns(default(CancellationToken)).Verifiable();
        await _handler.ExecuteAsync(_httpContext, _webSocket, "", _userContext);
    }

    [Fact]
    public async Task ExecuteAsync()
    {
        var subProtocol = "abc";
        _mockAppLifetime.Setup(x => x.ApplicationStopping).Returns(default(CancellationToken)).Verifiable();
        _mockHttpContext.Setup(x => x.RequestAborted).Returns(default(CancellationToken)).Verifiable();
        var mockWebSocketConnection = new Mock<IWebSocketConnection>(MockBehavior.Strict);
        var webSocketConnection = mockWebSocketConnection.Object;
        _mockHandler.Protected().Setup<IWebSocketConnection>("CreateWebSocketConnection", _webSocket, ItExpr.IsAny<CancellationToken>())
            .Returns(webSocketConnection).Verifiable();
        var mockSubServer = new Mock<IOperationMessageProcessor>(MockBehavior.Strict);
        var subServer = mockSubServer.Object;
        _mockHandler.Protected().Setup<IOperationMessageProcessor>("CreateReceiveStream", webSocketConnection, subProtocol, _userContext)
            .Returns(subServer).Verifiable();
        mockSubServer.Setup(x => x.Dispose()).Verifiable();
        mockWebSocketConnection.Setup(x => x.ExecuteAsync(subServer)).Returns(Task.CompletedTask).Verifiable();
        await _handler.ExecuteAsync(_httpContext, _webSocket, subProtocol, _userContext);
        mockWebSocketConnection.Verify();
        mockSubServer.Verify();
    }

    [Fact]
    public async Task ExecuteAsync_EatsAppShutdown()
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var subProtocol = "abc";
        _mockAppLifetime.Setup(x => x.ApplicationStopping).Returns(token).Verifiable();
        _mockHttpContext.Setup(x => x.RequestAborted).Returns(default(CancellationToken)).Verifiable();
        var mockWebSocketConnection = new Mock<IWebSocketConnection>(MockBehavior.Strict);
        var webSocketConnection = mockWebSocketConnection.Object;
        CancellationToken token2 = default;
        _mockHandler.Protected().Setup<IWebSocketConnection>("CreateWebSocketConnection", _webSocket, ItExpr.IsAny<CancellationToken>())
            .Returns<WebSocket, CancellationToken>((_, token3) => {
                token2 = token3;
                return webSocketConnection;
            }).Verifiable();
        var mockSubServer = new Mock<IOperationMessageProcessor>(MockBehavior.Strict);
        var subServer = mockSubServer.Object;
        _mockHandler.Protected().Setup<IOperationMessageProcessor>("CreateReceiveStream", webSocketConnection, subProtocol, _userContext)
            .Returns(subServer).Verifiable();
        mockSubServer.Setup(x => x.Dispose()).Verifiable();
        mockWebSocketConnection.Setup(x => x.ExecuteAsync(subServer)).Returns<IOperationMessageProcessor>(_ => {
            cts.Cancel();
            token2.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }).Verifiable();
        await _handler.ExecuteAsync(_httpContext, _webSocket, subProtocol, _userContext);
        mockWebSocketConnection.Verify();
        mockSubServer.Verify();
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsHttpContextCanceled()
    {
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        var subProtocol = "abc";
        _mockAppLifetime.Setup(x => x.ApplicationStopping).Returns(default(CancellationToken)).Verifiable();
        _mockHttpContext.Setup(x => x.RequestAborted).Returns(token).Verifiable();
        var mockWebSocketConnection = new Mock<IWebSocketConnection>(MockBehavior.Strict);
        var webSocketConnection = mockWebSocketConnection.Object;
        CancellationToken token2 = default;
        _mockHandler.Protected().Setup<IWebSocketConnection>("CreateWebSocketConnection", _webSocket, ItExpr.IsAny<CancellationToken>())
            .Returns<WebSocket, CancellationToken>((_, token3) => {
                token2 = token3;
                return webSocketConnection;
            }).Verifiable();
        var mockSubServer = new Mock<IOperationMessageProcessor>(MockBehavior.Strict);
        var subServer = mockSubServer.Object;
        _mockHandler.Protected().Setup<IOperationMessageProcessor>("CreateReceiveStream", webSocketConnection, subProtocol, _userContext)
            .Returns(subServer).Verifiable();
        mockSubServer.Setup(x => x.Dispose()).Verifiable();
        mockWebSocketConnection.Setup(x => x.ExecuteAsync(subServer)).Returns<IOperationMessageProcessor>(_ => {
            cts.Cancel();
            token2.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }).Verifiable();
        await Should.ThrowAsync<OperationCanceledException>(() => _handler.ExecuteAsync(_httpContext, _webSocket, subProtocol, _userContext));
        mockWebSocketConnection.Verify();
        mockSubServer.Verify();
    }

    [Fact]
    public void CreateWebSocketConnection()
    {
        var connection = _handler.Do_CreateWebSocketConnection(_webSocket, default);
        connection.ShouldBeOfType<WebSocketConnection>();
    }

    [Fact]
    public void CreateSendStream_Old()
    {
        _options.ConnectionInitWaitTimeout = Timeout.InfiniteTimeSpan;
        var mockSendStream = new Mock<IWebSocketConnection>(MockBehavior.Strict);
        var receiveStream = _handler.Do_CreateReceiveStream(mockSendStream.Object, "graphql-transport-ws", _userContext);
        receiveStream.ShouldBeOfType<NewSubscriptionServer>();
    }

    [Fact]
    public void CreateSendStream_New()
    {
        _options.ConnectionInitWaitTimeout = Timeout.InfiniteTimeSpan;
        var mockSendStream = new Mock<IWebSocketConnection>(MockBehavior.Strict);
        var receiveStream = _handler.Do_CreateReceiveStream(mockSendStream.Object, "graphql-ws", _userContext);
        receiveStream.ShouldBeOfType<OldSubscriptionServer>();
    }

    [Fact]
    public void CreateSendStream_Invalid()
    {
        var mockSendStream = new Mock<IWebSocketConnection>(MockBehavior.Strict);
        Should.Throw<ArgumentOutOfRangeException>(() => _handler.Do_CreateReceiveStream(mockSendStream.Object, "unknown", _userContext));
    }

    [Fact]
    public void DerivedConstructor()
    {
        _ = new WebSocketHandler<ISchema>(_serializer, Mock.Of<IDocumentExecuter<ISchema>>(MockBehavior.Strict), _scopeFactory, _options, _appLifetime);
    }

    [Fact]
    public void SupportedSubProtocols()
    {
        _handler.SupportedSubProtocols.ShouldBe(new[] { "graphql-transport-ws", "graphql-ws" });
    }
}
