using Microsoft.AspNetCore.Mvc;

namespace Tests;

public class ExecutionResultActionResultTests
{
    [Fact]
    public async Task Basic()
    {
        var _hostBuilder = new WebHostBuilder();
        _hostBuilder.ConfigureServices(services => {
            services.AddSingleton<Chat.Services.ChatService>();
            services.AddGraphQL(b => b
                .AddAutoSchema<Chat.Schema.Query>()
                .AddSystemTextJson());
            services.AddRouting();
            services.AddControllers();
        });
        _hostBuilder.Configure(app => {
            app.UseRouting();
            app.UseEndpoints(endpoints => {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        });
        var server = new TestServer(_hostBuilder);
        var str = await server.ExecuteGet("/graphql?query={count}");
        str.ShouldBe("{\"data\":{\"count\":0}}");
    }
}

[Route("/")]
public class TestController : Controller
{
    private readonly IDocumentExecuter _documentExecuter;

    public TestController(IDocumentExecuter<ISchema> documentExecuter)
    {
        _documentExecuter = documentExecuter;
    }

    [HttpGet]
    [Route("graphql")]
    public async Task<IActionResult> Test(string query)
    {
        var result = await _documentExecuter.ExecuteAsync(new() {
            Query = query,
            RequestServices = HttpContext.RequestServices,
            CancellationToken = HttpContext.RequestAborted,
        });
        return new ExecutionResultActionResult(result);
    }
}
