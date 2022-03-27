using GraphQL.MicrosoftDI;
using GraphQL.SystemTextJson;
using Microsoft.Extensions.DependencyInjection;
using Shane32.GraphQL.AspNetCore;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;

namespace Tests;

public class TestChatApp : TestServer
{
    public TestChatApp()
        : base(ConfigureBuilder())
    {
    }

    private static IWebHostBuilder ConfigureBuilder()
    {
        var hostBuilder = new WebHostBuilder();
        hostBuilder.ConfigureServices(services => {
            services.AddSingleton<Chat.Services.ChatService>();
            services.AddGraphQL(b => b
                .AddAutoSchema<Chat.Schema.Query>(s => s
                    .WithMutation<Chat.Schema.Mutation>()
                    .WithSubscription<Chat.Schema.Subscription>())
                .AddSystemTextJson()
                .AddServer());
        });
        hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL("/graphql");
        });
        return hostBuilder;
    }
}
