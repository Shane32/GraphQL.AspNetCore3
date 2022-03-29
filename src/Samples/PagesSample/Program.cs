using GraphQL;
using GraphQL.MicrosoftDI;
using GraphQL.SystemTextJson;
using Shane32.GraphQL.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddSingleton<Chat.Services.ChatService>();
builder.Services.AddGraphQL(b => b
    .AddAutoSchema<Chat.Schema.Query>(s => s
        .WithMutation<Chat.Schema.Mutation>()
        .WithSubscription<Chat.Schema.Subscription>())
    .AddSystemTextJson());

var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseWebSockets();
// configure the graphql endpoint at "/graphql"
app.UseGraphQL("/graphql");
app.UseRouting();
app.MapRazorPages();

await app.RunAsync();
