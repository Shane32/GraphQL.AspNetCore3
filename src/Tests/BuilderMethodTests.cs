namespace Tests;

public class BuilderMethodTests
{
    private readonly WebHostBuilder _hostBuilder;

    public BuilderMethodTests()
    {
        _hostBuilder = new WebHostBuilder();
        _hostBuilder.ConfigureServices(services => {
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
        });
    }

    private class Schema2 : Schema
    {
        public Schema2(IServiceProvider serviceProvider) : base(serviceProvider)
        {
            Query = serviceProvider.GetRequiredService<AutoRegisteringObjectGraphType<Query2>>();
        }
    }

    public class Query2
    {
        public static string? UserInfo([FromUserContext] MyUserContext myContext) => myContext.UserInfo;
    }

    public class MyUserContext : Dictionary<string, object?>
    {
        public string? UserInfo { get; set; }
    }

    [Fact]
    public void WebSocketAuthenticationService_Typed()
    {
        var services = new ServiceCollection();
        services.AddGraphQL(b => b.AddWebSocketAuthentication<MyWebSocketAuthenticationService>());
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IWebSocketAuthenticationService>();
        Should.Throw<NotImplementedException>(() => service.AuthenticateAsync(null!, null!, null!));
    }

    [Fact]
    public void WebSocketAuthenticationService_Factory()
    {
        var services = new ServiceCollection();
        services.AddGraphQL(b => b.AddWebSocketAuthentication(_ => new MyWebSocketAuthenticationService()));
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IWebSocketAuthenticationService>();
        Should.Throw<NotImplementedException>(() => service.AuthenticateAsync(null!, null!, null!));
    }

    [Fact]
    public void WebSocketAuthenticationService_Instance()
    {
        var services = new ServiceCollection();
        services.AddGraphQL(b => b.AddWebSocketAuthentication(new MyWebSocketAuthenticationService()));
        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IWebSocketAuthenticationService>();
        Should.Throw<NotImplementedException>(() => service.AuthenticateAsync(null!, null!, null!));
    }

    private class MyWebSocketAuthenticationService : IWebSocketAuthenticationService
    {
        public Task AuthenticateAsync(IWebSocketConnection connection, string subProtocol, OperationMessage operationMessage) => throw new NotImplementedException();
    }

    [Fact]
    public async Task Basic()
    {
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL("/graphql");
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task Basic_PathString()
    {
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL(new PathString("/graphql"));
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task Basic_WithSchema()
    {
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL<ISchema>("/graphql");
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task Basic_PathString_WithSchema()
    {
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL<ISchema>(new PathString("/graphql"));
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task SpecificMiddleware()
    {
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL<GraphQLHttpMiddleware<ISchema>>("/graphql", new GraphQLHttpMiddlewareOptions());
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task SpecificMiddleware_PathString()
    {
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL<GraphQLHttpMiddleware<ISchema>>(new PathString("/graphql"), new GraphQLHttpMiddlewareOptions());
        });
        await VerifyAsync();
    }

#if !NETCOREAPP2_1 && !NET48
    [Fact]
    public async Task EndpointRouting()
    {
        _hostBuilder.ConfigureServices(services => services.AddRouting());
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapGraphQL("graphql");
            });
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task EndpointRouting_WithSchema()
    {
        _hostBuilder.ConfigureServices(services => services.AddRouting());
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapGraphQL<ISchema>("graphql");
            });
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task EndpointRouting_WithMiddleware()
    {
        _hostBuilder.ConfigureServices(services => services.AddRouting());
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapGraphQL<GraphQLHttpMiddleware<ISchema>>("graphql", new GraphQLHttpMiddlewareOptions());
            });
        });
        await VerifyAsync();
    }
#endif

    [Fact]
    public async Task UserContextBuilder_Configure1()
    {
        _hostBuilder.ConfigureServices(services => {
            services.AddGraphQL(b => b.AddUserContextBuilder<MyUserContextBuilder>());
        });
        await VerifyUserContextAsync("fromBuilder");
    }

    [Fact]
    public async Task UserContextBuilder_Configure2()
    {
        _hostBuilder.ConfigureServices(services => {
            services.AddGraphQL(b => b.AddUserContextBuilder(context => Task.FromResult(new MyUserContext { UserInfo = "test2" })));
        });
        await VerifyUserContextAsync("test2");
    }

    [Fact]
    public async Task UserContextBuilder_Configure3()
    {
        _hostBuilder.ConfigureServices(services => {
            services.AddGraphQL(b => b.AddUserContextBuilder(context => new MyUserContext { UserInfo = "test3" }));
        });
        await VerifyUserContextAsync("test3");
    }

    private class MyUserContextBuilder : IUserContextBuilder
    {
        public ValueTask<IDictionary<string, object?>> BuildUserContextAsync(HttpContext context, object? payload)
            => new ValueTask<IDictionary<string, object?>>(new MyUserContext { UserInfo = "fromBuilder" });
    }

    private async Task VerifyUserContextAsync(string value)
    {
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL<Schema2>();
        });
        using var server = new TestServer(_hostBuilder);
        var str = await server.ExecuteGet("/graphql?query={userInfo}");
        str.ShouldBe(@"{""data"":{""userInfo"":""" + value + @"""}}");
    }

    private async Task VerifyAsync()
    {
        using var server = new TestServer(_hostBuilder);

        await server.VerifyChatSubscriptionAsync();
    }
}
