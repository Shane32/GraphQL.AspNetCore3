using GraphQL.Transport;

namespace Tests;

public class ChatTests : IDisposable
{
    private readonly TestChatApp _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Query()
    {
        var str = await _app.ExecuteGet("/graphql?query={count}");
        str.ShouldBe("{\"data\":{\"count\":0}}");
    }

    [Fact]
    public async Task Mutation()
    {
        var str = await _app.ExecutePost(
            "/graphql",
            "mutation {addMessage(message:{message:\"hello\",from:\"John Doe\"}){id}}");
        str.ShouldBe("{\"data\":{\"addMessage\":{\"id\":\"1\"}}}");

        str = await _app.ExecuteGet("/graphql?query={count}");
        str.ShouldBe("{\"data\":{\"count\":1}}");
    }

    [Theory]
    [InlineData("graphql-ws")]
    [InlineData("graphql-transport-ws")]
    public async Task Subscription(string subProtocol)
    {
        // create websocket connection
        var webSocketClient = _app.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => {
            request.Headers["Sec-WebSocket-Protocol"] = subProtocol;
        };
        webSocketClient.SubProtocols.Add(subProtocol);
        using var webSocket = await webSocketClient.ConnectAsync(new Uri(_app.BaseAddress, "/graphql"), default);

        // send CONNECTION_INIT
        await webSocket.SendMessageAsync(new OperationMessage {
            Type = "connection_init"
        });

        // wait for CONNECTION_ACK
        var message = await webSocket.ReceiveMessageAsync();
        message.Type.ShouldBe("connection_ack");

        // subscribe
        await webSocket.SendMessageAsync(new OperationMessage {
            Type = subProtocol == "graphql-ws" ? "start" : "subscribe",
            Id = "123",
            Payload = new GraphQLRequest {
                Query = "subscription { events { type message { id message from } } }",
            },
        });

        // post a new message on a separate thread
        _ = Task.Run(Mutation);

        // wait for a message sent over this websocket
        message = await webSocket.ReceiveMessageAsync();
        message.Type.ShouldBe(subProtocol == "graphql-ws" ? "data" : "next");
        message.Payload.ShouldBe(@"{""data"":{""events"":{""type"":""NEW_MESSAGE"",""message"":{""id"":""1"",""message"":""hello"",""from"":""John Doe""}}}}");

        // unsubscribe
        await webSocket.SendMessageAsync(new OperationMessage {
            Type = subProtocol == "graphql-ws" ? "stop" : "complete",
            Id = "123",
        });

        if (subProtocol == "graphql-ws") {
            // send close message
            await webSocket.SendMessageAsync(new OperationMessage {
                Type = "connection_terminate",
            });
        } else {
            // close websocket
            await webSocket.CloseOutputAsync(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure, null, default);
        }

        // wait for websocket closure
        (await webSocket.ReceiveCloseAsync()).ShouldBe(System.Net.WebSockets.WebSocketCloseStatus.NormalClosure);
    }
}
