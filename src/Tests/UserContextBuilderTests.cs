namespace Tests;

public class UserContextBuilderTests : IDisposable
{
    private TestServer _server = null!;
    private HttpClient _client = null!;

    private void Configure(Action<IGraphQLBuilder> configureBuilder)
    {
        var hostBuilder = new WebHostBuilder();
        hostBuilder.ConfigureServices(services => {
            services.AddSingleton<Chat.Services.ChatService>();
            services.AddGraphQL(b => {
                configureBuilder(b);
                b.AddAutoSchema<MyQuery>();
                b.AddSystemTextJson();
            });
            services.AddHttpContextAccessor();
        });
        hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL("/graphql");
        });
        _server = new TestServer(hostBuilder);
        _client = _server.CreateClient();
    }

    public void Dispose() => _server?.Dispose();

    private class MyQuery
    {
        public static string? Test([FromUserContext] MyUserContext ctx) => ctx.Name;
    }

    [Fact]
    public void NullChecks()
    {
        Func<HttpContext, MyUserContext> func = null!;
        Should.Throw<ArgumentNullException>(() => new UserContextBuilder<MyUserContext>(func));
        Func<HttpContext, ValueTask<MyUserContext>> func2 = null!;
        Should.Throw<ArgumentNullException>(() => new UserContextBuilder<MyUserContext>(func2));
    }

    [Fact]
    public async Task Sync_Works()
    {
        var context = Mock.Of<HttpContext>(MockBehavior.Strict);
        var userContext = new MyUserContext();
        var builder = new UserContextBuilder<MyUserContext>(context2 => {
            context2.ShouldBe(context);
            return userContext;
        });
        (await builder.BuildUserContextAsync(context)).ShouldBe(userContext);
    }

    [Fact]
    public async Task Async_Works()
    {
        var context = Mock.Of<HttpContext>(MockBehavior.Strict);
        var userContext = new MyUserContext();
        var builder = new UserContextBuilder<MyUserContext>(context2 => {
            context2.ShouldBe(context);
            return new ValueTask<MyUserContext>(userContext);
        });
        (await builder.BuildUserContextAsync(context)).ShouldBe(userContext);
    }

    private async Task Test(string name)
    {
        using var response = await _client.GetAsync("/graphql?query={test}");
        response.EnsureSuccessStatusCode();
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""data"":{""test"":""" + name + @"""}}");
    }

    private async Task TestDirect(string name)
    {
        var executer = _server.Services.GetRequiredService<IDocumentExecuter<ISchema>>();
        var result = await executer.ExecuteAsync(new ExecutionOptions {
            Query = "{test}",
            RequestServices = _server.Services,
        });
        var serializer = _server.Services.GetRequiredService<IGraphQLTextSerializer>();
        var actual = serializer.Serialize(result);
        actual.ShouldBe(@"{""data"":{""test"":""" + name + @"""}}");
    }

    [Fact]
    public async Task Builder1()
    {
        Configure(b => b.AddUserContextBuilder(ctx => new MyUserContext { Name = "John Doe" }));
        await Test("John Doe");
    }

    [Fact]
    public async Task Builder1Direct()
    {
        Configure(b => b.AddUserContextBuilder(ctx => new MyUserContext { Name = "John Doe" }));
        await TestDirect("John Doe");
    }

    [Fact]
    public void Builder1Null()
    {
        Should.Throw<ArgumentNullException>(() => Configure(b => b.AddUserContextBuilder((Func<HttpContext, MyUserContext>)null!)));
    }

    [Fact]
    public async Task Builder2()
    {
        Configure(b => b.AddUserContextBuilder(ctx => Task.FromResult(new MyUserContext { Name = "John Doe" })));
        await Test("John Doe");
    }

    [Fact]
    public async Task Builder2Direct()
    {
        Configure(b => b.AddUserContextBuilder(ctx => Task.FromResult(new MyUserContext { Name = "John Doe" })));
        await TestDirect("John Doe");
    }

    [Fact]
    public void Builder2Null()
    {
        Should.Throw<ArgumentNullException>(() => Configure(b => b.AddUserContextBuilder((Func<HttpContext, Task<MyUserContext>>)null!)));
    }

    [Fact]
    public async Task Builder3()
    {
        Configure(b => b.AddUserContextBuilder<MyBuilder>());
        await Test("John Doe");
    }

    [Fact]
    public async Task Builder3Direct()
    {
        Configure(b => b.AddUserContextBuilder<MyBuilder>());
        await TestDirect("John Doe");
    }

    private class MyBuilder : IUserContextBuilder
    {
        public ValueTask<IDictionary<string, object?>> BuildUserContextAsync(HttpContext context)
            => new(new MyUserContext { Name = "John Doe" });
    }

    private class MyUserContext : Dictionary<string, object?>
    {
        public string? Name { get; set; }
    }
}
