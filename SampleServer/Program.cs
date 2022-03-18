using GraphQL;
using GraphQL.MicrosoftDI;
using GraphQL.SystemTextJson;
using Shane32.GraphQL.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddGraphQL(b => b
    .AddAutoSchema<Query>()
    .AddSystemTextJson()
    .AddServer());

var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseGraphQL("/graphql");
app.UseRouting();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
});
await app.RunAsync();

internal class Query
{
    public static string Hero() => "Luke Skywalker";
}
