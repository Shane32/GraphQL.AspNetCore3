using GraphQL;
using GraphQL.AspNetCore3;
using GraphQL.AspNetCore3.WebSockets;
using GraphQL.MicrosoftDI;
using GraphQL.SystemTextJson;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<Chat.Services.ChatService>();
builder.Services.AddGraphQL(b => b
    .AddAutoSchema<Chat.Schema.Query>(s => s
        .WithMutation<Chat.Schema.Mutation>()
        .WithSubscription<Chat.Schema.Subscription>())
    .AddSystemTextJson());
builder.Services.AddSingleton(typeof(IWebSocketHandler<>), typeof(WebSocketHandler<>));
builder.Services.AddSingleton(new GraphQLHttpMiddlewareOptions()); // for the websocket handler

var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseWebSockets();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();
