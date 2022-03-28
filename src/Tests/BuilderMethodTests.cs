using GraphQL.MicrosoftDI;
using GraphQL.SystemTextJson;
using GraphQL.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shane32.GraphQL.AspNetCore;

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
                .AddSystemTextJson());
        });
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

    private async Task VerifyAsync()
    {
        using var server = new TestServer(_hostBuilder);

        await server.VerifyChatSubscriptionAsync();
    }
}
