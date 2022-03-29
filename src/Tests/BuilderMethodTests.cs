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
        _hostBuilder.ConfigureServices(services => services.AddSingleton<GraphQLHttpMiddlewareOptions>());
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL<ISchema, GraphQLHttpMiddleware<ISchema>>("/graphql");
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task SpecificMiddleware_PathString()
    {
        _hostBuilder.ConfigureServices(services => services.AddSingleton<GraphQLHttpMiddlewareOptions>());
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL<ISchema, GraphQLHttpMiddleware<ISchema>>(new PathString("/graphql"));
        });
        await VerifyAsync();
    }

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
        _hostBuilder.ConfigureServices(services => services.AddSingleton<GraphQLHttpMiddlewareOptions>());
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapGraphQL<ISchema, GraphQLHttpMiddleware<ISchema>>("graphql");
            });
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task WebSocketHandler_Configure()
    {
        _hostBuilder.ConfigureServices(services => {
            services.AddGraphQL(b => b.AddWebSocketHandler(c => { }));
        });
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL();
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task WebSocketHandler_Configure2()
    {
        _hostBuilder.ConfigureServices(services => {
            services.AddGraphQL(b => b.AddWebSocketHandler((c, p) => { }));
        });
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL();
        });
        await VerifyAsync();
    }

    [Fact]
    public async Task WebSocketHandler_Configure3()
    {
        _hostBuilder.ConfigureServices(services => {
            services.AddSingleton<WebSocketHandlerOptions>();
            services.AddGraphQL(b => b.AddWebSocketHandler<WebSocketHandler<ISchema>>());
        });
        _hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL();
        });
        await VerifyAsync();
    }

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
        public ValueTask<IDictionary<string, object?>> BuildUserContextAsync(HttpContext context)
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
