using GraphQL;
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

var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();
