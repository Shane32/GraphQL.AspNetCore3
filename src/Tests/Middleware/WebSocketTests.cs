using System.Net;

namespace Tests.Middleware;

public class WebSocketTests : IDisposable
{
    private TestServer _server = null!;

    private void Configure(Action<GraphQLHttpMiddlewareOptions>? configureOptions = null, Action<IServiceCollection>? configureServices = null)
    {
        if (configureOptions == null)
            configureOptions = _ => { };
        if (configureServices == null)
            configureServices = _ => { };

        var hostBuilder = new WebHostBuilder();
        hostBuilder.ConfigureServices(services => {
            services.AddSingleton<Chat.Services.ChatService>();
            services.AddGraphQL(b => b
                .AddAutoSchema<Chat.Schema.Query>(s => s
                    .WithMutation<Chat.Schema.Mutation>()
                    .WithSubscription<Chat.Schema.Subscription>())
                .AddSchema<Schema2>()
                .AddSystemTextJson());
#if NETCOREAPP2_1 || NET48
            services.AddHostApplicationLifetime();
#endif
            configureServices(services);
        });
        hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL("/graphql", configureOptions);
            app.UseGraphQL<Schema2>("/graphql2", configureOptions);
        });
        _server = new TestServer(hostBuilder);
    }

    private class Schema2 : Schema
    {
        public Schema2()
        {
            Query = new AutoRegisteringObjectGraphType<Query2>();
        }
    }

    private class Query2
    {
        public static string? Var(string? test) => test;

        public static string? Ext(IResolveFieldContext context)
            => context.InputExtensions.TryGetValue("test", out var value) ? value?.ToString() : null;
    }

    public void Dispose() => _server?.Dispose();

    private WebSocketClient BuildClient(string subProtocol = "graphql-ws")
    {
        var webSocketClient = _server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => {
            request.Headers["Sec-WebSocket-Protocol"] = subProtocol;
        };
        webSocketClient.SubProtocols.Add(subProtocol);
        return webSocketClient;
    }

    [Fact]
    public async Task NoConfiguredHandlers()
    {
        var hostBuilder = new WebHostBuilder();
        hostBuilder.ConfigureServices(services => {
            services.AddSingleton<Chat.Services.ChatService>();
            services.AddGraphQL(b => b
                .AddSchema<Schema2>()
                .AddSystemTextJson());
        });

        hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL<MyGraphQLHttpMiddleware>("/graphql");
        });

        _server = new TestServer(hostBuilder);

        var webSocketClient = BuildClient();
        var error = await Should.ThrowAsync<InvalidOperationException>(() => webSocketClient.ConnectAsync(new Uri(_server.BaseAddress, "/graphql"), default));
        error.Message.ShouldBe("Incomplete handshake, status code: 404");
    }

    [Fact]
    public async Task UnsupportedHandler()
    {
        var hostBuilder = new WebHostBuilder();
        hostBuilder.ConfigureServices(services => {
            services.AddSingleton<Chat.Services.ChatService>();
            services.AddGraphQL(b => b
                .AddSchema<Schema2>()
                .AddSystemTextJson());
        });
        hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL<TestMiddleware>("/graphql");
        });

        _server = new TestServer(hostBuilder);
        var webSocketClient = BuildClient();
        var error = await Should.ThrowAsync<InvalidOperationException>(() => webSocketClient.ConnectAsync(new Uri(_server.BaseAddress, "/graphql"), default));
        error.Message.ShouldBe("Incomplete handshake, status code: 400");
    }

    private class TestMiddleware : GraphQLHttpMiddleware
    {
        public TestMiddleware(RequestDelegate next) : base(next, new GraphQLSerializer(), new GraphQLHttpMiddlewareOptions(), new IWebSocketHandler[] { GetMockHandler() }) { }

        private static IWebSocketHandler GetMockHandler()
        {
            var mock = new Mock<IWebSocketHandler>(MockBehavior.Strict);
            mock.Setup(x => x.SupportedSubProtocols).Returns(new string[] { "unsupportedProtocol" });
            return mock.Object;
        }

        protected override Task<ExecutionResult> ExecuteRequestAsync(HttpContext context, GraphQLRequest? request, IServiceProvider serviceProvider, IDictionary<string, object?> userContext)
            => throw new NotImplementedException();

        protected override Task<ExecutionResult> ExecuteScopedRequestAsync(HttpContext context, GraphQLRequest? request, IDictionary<string, object?> userContext)
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task Disabled()
    {
        Configure(o => o.HandleWebSockets = false);

        var webSocketClient = BuildClient();
        var error = await Should.ThrowAsync<InvalidOperationException>(() => webSocketClient.ConnectAsync(new Uri(_server.BaseAddress, "/graphql"), default));
        error.Message.ShouldBe("Incomplete handshake, status code: 404");
    }

    private class MyGraphQLHttpMiddleware : GraphQLHttpMiddleware
    {
        public MyGraphQLHttpMiddleware(
            RequestDelegate next,
            IGraphQLTextSerializer serializer)
            : base(next, serializer, new GraphQLHttpMiddlewareOptions(), Array.Empty<IWebSocketHandler<ISchema>>())
        {
        }

        protected override Task<ExecutionResult> ExecuteRequestAsync(HttpContext context, GraphQLRequest? request, IServiceProvider serviceProvider, IDictionary<string, object?> userContext) => throw new NotImplementedException();
        protected override Task<ExecutionResult> ExecuteScopedRequestAsync(HttpContext context, GraphQLRequest? request, IDictionary<string, object?> userContext) => throw new NotImplementedException();
    }

}
