using GraphQL;
using GraphQL.AspNetCore3;
using GraphQL.MicrosoftDI;
using GraphQL.NewtonsoftJson;

namespace Net48Sample;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_1);

        services.AddSingleton<Chat.Services.ChatService>();
        services.AddGraphQL(b => b
            .AddAutoSchema<Chat.Schema.Query>(s => s
                .WithMutation<Chat.Schema.Mutation>()
                .WithSubscription<Chat.Schema.Subscription>())
            .AddNewtonsoftJson());
        services.AddHostApplicationLifetime();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env)
    {
        app.UseDeveloperExceptionPage();
        app.UseWebSockets();
        app.UseMvc();

        // configure the graphql endpoint at "/graphql"
        app.UseGraphQL("/graphql");
    }
}
