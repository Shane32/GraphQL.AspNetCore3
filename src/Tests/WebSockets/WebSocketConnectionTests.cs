using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;

namespace Tests.WebSockets
{
    public class WebSocketConnectionTests : IDisposable
    {
        private readonly Mock<WebSocket> _mockWebSocket = new Mock<WebSocket>(MockBehavior.Strict);
        private WebSocket _webSocket => _mockWebSocket.Object;
        private readonly Mock<IGraphQLSerializer> _mockSerializer = new Mock<IGraphQLSerializer>(MockBehavior.Strict);
        private IGraphQLSerializer _serializer => _mockSerializer.Object;
        private readonly WebSocketHandlerOptions _options = new();
        private readonly CancellationTokenSource _cts = new();
        private CancellationToken _token => _cts.Token;
        private readonly Mock<TestWebSocketConnection> _mockConnection;
        private TestWebSocketConnection _connection => _mockConnection.Object;

        public WebSocketConnectionTests()
        {
            _mockConnection = new Mock<TestWebSocketConnection>(_webSocket, _serializer, _options, _token);
        }

        public void Dispose()
        {
            _mockWebSocket.Verify();
            _mockSerializer.Verify();
            _cts.Cancel();
            _cts.Dispose();
        }

        [Fact]
        public void Constructor()
        {
            Should.Throw<ArgumentNullException>(() => new WebSocketConnection(null!, _serializer, _options, default));
            Should.Throw<ArgumentNullException>(() => new WebSocketConnection(_webSocket, null!, _options, default));
            Should.Throw<ArgumentNullException>(() => new WebSocketConnection(_webSocket, _serializer, null!, default));
        }

        [Theory]
        [InlineData(-2)]
        [InlineData(-0.5)]
        [InlineData(2147483648d)]
        public void Constructor_InvalidTimeout(double ms)
        {
            _options.DisconnectionTimeout = TimeSpan.FromMilliseconds(ms);
            Should.Throw<ArgumentOutOfRangeException>(() => new WebSocketConnection(_webSocket, _serializer, _options, default));
        }
    }
}
