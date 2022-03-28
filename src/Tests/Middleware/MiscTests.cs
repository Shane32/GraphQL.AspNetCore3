using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shane32.GraphQL.AspNetCore;

namespace Tests.Middleware;

public class MiscTests
{
    [Fact]
    public void Constructors()
    {
        RequestDelegate next = _ => Task.CompletedTask;
        var serializer = Mock.Of<IGraphQLTextSerializer>();
        var handlers = new IWebSocketHandler<ISchema>[0];
        var options = new GraphQLHttpMiddlewareOptions();
        var executer = Mock.Of<IDocumentExecuter<ISchema>>();
        var scopeFactory = Mock.Of<IServiceScopeFactory>();
        var appLifetime = Mock.Of<IHostApplicationLifetime>();
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(null!, serializer, executer, scopeFactory, options, appLifetime, handlers));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, null!, executer, scopeFactory, options, appLifetime, handlers));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, null!, scopeFactory, options, appLifetime, handlers));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, null!, options, appLifetime, handlers));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, scopeFactory, null!, appLifetime, handlers));
        _ = new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, scopeFactory, options, null!, handlers);
        _ = new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, scopeFactory, options, appLifetime, null!);

        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(null!, serializer, executer, scopeFactory, options, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, null!, executer, scopeFactory, options, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, null!, scopeFactory, options, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, null!, options, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, scopeFactory, null!, appLifetime));
        Should.Throw<ArgumentNullException>(() => new GraphQLHttpMiddleware<ISchema>(next, serializer, executer, scopeFactory, options, null!));
    }
}
