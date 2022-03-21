using GraphQL.MicrosoftDI;
using GraphQL.SystemReactive;
using GraphQL.SystemTextJson;
using Shane32.GraphQL.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<Chat.Services.ChatService>();
builder.Services.AddGraphQL(b => b
    .AddAutoSchema<Chat.Schema.Query>(s => s
        .WithMutation<Chat.Schema.Mutation>()
        .WithSubscription<Chat.Schema.Subscription>())
    .AddSubscriptionExecutionStrategy()
    .AddSystemTextJson()
    .AddServer());

var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseWebSockets();
// configure the graphql endpoint at "/graphql"
app.UseGraphQL("/graphql");
// configure GraphiQL at "/"
app.UseGraphQLPlayground(new GraphQL.Server.Ui.Playground.PlaygroundOptions { GraphQLEndPoint = new PathString("/graphql") }, "/");
//app.UseRouting();
//app.UseEndpoints(endpoints =>
//{
//    endpoints.MapControllerRoute(
//        name: "default",
//        pattern: "{controller=Home}/{action=Index}/{id?}");
//});
await app.RunAsync();
