using System.Net;
using GraphQL.AspNetCore3.Errors;
using Microsoft.Extensions.Hosting;

namespace Tests.Middleware;

public class MiscTests
{
    [Fact]
    public void Constructors()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var serializer = Mock.Of<IGraphQLTextSerializer>();
        var options = new GraphQLHttpMiddlewareOptions();
        var executer = Mock.Of<IDocumentExecuter<ISchema>>();
        var scopeFactory = Mock.Of<IServiceScopeFactory>();
        var appLifetime = Mock.Of<IHostApplicationLifetime>();
        var provider = Mock.Of<IServiceProvider>();
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(null!, serializer, executer, scopeFactory, options, provider, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, null!, executer, scopeFactory, options, provider, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, null!, scopeFactory, options, provider, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, null!, options, provider, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, scopeFactory, null!, provider, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, scopeFactory, options, null!, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, scopeFactory, options, provider, null!));
        _ = new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, scopeFactory, options, provider, appLifetime);

        Should.Throw<ArgumentNullException>(() => new MyMiddleware2(null!, serializer, executer, scopeFactory, options, null));
        Should.Throw<ArgumentNullException>(() => new MyMiddleware2(next, null!, executer, scopeFactory, options, null));
        Should.Throw<ArgumentNullException>(() => new MyMiddleware2(next, serializer, null!, scopeFactory, options, null));
        Should.Throw<ArgumentNullException>(() => new MyMiddleware2(next, serializer, executer, null!, options, null));
        Should.Throw<ArgumentNullException>(() => new MyMiddleware2(next, serializer, executer, scopeFactory, null!, null));
        _ = new MyMiddleware2(next, serializer, executer, scopeFactory, options, null);

    }

    private class MyMiddleware2 : GraphQLHttpMiddleware<ISchema>
    {
        public MyMiddleware2(RequestDelegate next, IGraphQLTextSerializer serializer, IDocumentExecuter<ISchema> documentExecuter, IServiceScopeFactory serviceScopeFactory, GraphQLHttpMiddlewareOptions options, IEnumerable<IWebSocketHandler<ISchema>>? webSocketHandlers)
            : base(next, serializer, documentExecuter, serviceScopeFactory, options, webSocketHandlers)
        {
        }
    }

    [Fact]
    public async Task WriteErrorResponseString()
    {
        var mockMiddleware = new Mock<MyMiddleware>(MockBehavior.Strict);
        var mockContext = Mock.Of<HttpContext>();
        mockMiddleware.Protected().Setup<Task>("WriteErrorResponseAsync", mockContext, HttpStatusCode.OK, ItExpr.IsAny<ExecutionError>())
            .Returns<HttpContext, HttpStatusCode, ExecutionError>((_, _, error) => {
                error.ShouldBeOfType<ExecutionError>();
                error.Message.ShouldBe("testing");
                return Task.CompletedTask;
            });
        mockMiddleware.Protected().Setup<Task>("WriteErrorResponseAsync", mockContext, HttpStatusCode.OK, "testing").CallBase();
        await mockMiddleware.Object.Do_WriteErrorResponseAsync(mockContext, HttpStatusCode.OK, "testing");
    }

    [Fact]
    public void InvalidContentTypeError_NoMessage()
    {
        var err = new InvalidContentTypeError();
        err.Message.ShouldBe("Invalid 'Content-Type' header.");
    }

    [Fact]
    public void JsonInvalidError_NoMessage()
    {
        var err = new JsonInvalidError();
        err.Message.ShouldBe("JSON body text could not be parsed.");
    }

    [Fact]
    public void RequestError_WithInnerException()
    {
        var err = new RequestError("test", new DivideByZeroException());
        var str = new GraphQLSerializer().Serialize(err);
        str.ShouldBe(@"{""message"":""test"",""extensions"":{""code"":""REQUEST_ERROR"",""codes"":[""REQUEST_ERROR"",""DIVIDE_BY_ZERO""]}}");
    }

    [Fact]
    public void WebSocketSubProtocolNotSupportedError_Message()
    {
        var err = new WebSocketSubProtocolNotSupportedError(new string[] { "test1", "test2" });
        var str = new GraphQLSerializer().Serialize(err);
        str.ShouldBe(@"{""message"":""Invalid WebSocket sub-protocol(s): \u0027test1\u0027,\u0027test2\u0027"",""extensions"":{""code"":""WEB_SOCKET_SUB_PROTOCOL_NOT_SUPPORTED"",""codes"":[""WEB_SOCKET_SUB_PROTOCOL_NOT_SUPPORTED""]}}");
    }

    public class MyMiddleware : GraphQLHttpMiddleware<ISchema>
    {
        public MyMiddleware() : base(
            _ => Task.CompletedTask,
            Mock.Of<IGraphQLTextSerializer>(),
            Mock.Of<IDocumentExecuter<ISchema>>(),
            Mock.Of<IServiceScopeFactory>(),
            new GraphQLHttpMiddlewareOptions(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<IHostApplicationLifetime>())
        {
        }

        public Task Do_WriteErrorResponseAsync(HttpContext context, HttpStatusCode httpStatusCode, string message)
            => WriteErrorResponseAsync(context, httpStatusCode, message);
    }
}
