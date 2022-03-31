using ServiceLifetime = GraphQL.DI.ServiceLifetime;

namespace GraphQL.AspNetCore3;

/// <summary>
/// GraphQL specific extension methods for <see cref="IGraphQLBuilder"/>.
/// </summary>
public static class GraphQLBuilderExtensions
{
    /// <summary>
    /// Registers the default WebSocket handler with the dependency injection framework and
    /// optionally configures it with the specified configuration delegate.
    /// </summary>
    public static IGraphQLBuilder AddWebSocketHandler(this IGraphQLBuilder builder, Action<WebSocketHandlerOptions>? configure = null)
    {
        builder.Services.Register(typeof(IWebSocketHandler<>), typeof(WebSocketHandler<>), ServiceLifetime.Singleton);
        builder.Services.Configure(configure);
        return builder;
    }

    /// <summary>
    /// Registers the default WebSocket handler with the dependency injection framework and
    /// configures it with the specified configuration delegate.
    /// </summary>
    public static IGraphQLBuilder AddWebSocketHandler(this IGraphQLBuilder builder, Action<WebSocketHandlerOptions, IServiceProvider>? configure)
    {
        builder.Services.Register<IWebSocketHandler, WebSocketHandler>(ServiceLifetime.Singleton);
        builder.Services.Configure(configure);
        return builder;
    }

    /// <summary>
    /// Registers the specified WebSocket handler with the dependency injection framework as a singleton.
    /// </summary>
    public static IGraphQLBuilder AddWebSocketHandler<TWebSocketHandler>(this IGraphQLBuilder builder)
        where TWebSocketHandler : class, IWebSocketHandler
    {
        builder.Services.Register<IWebSocketHandler, TWebSocketHandler>(ServiceLifetime.Singleton);
        return builder;
    }

    /// <summary>
    /// Registers an <see cref="IUserContextBuilder"/> type with the dependency injection framework
    /// and configures it to be used when executing a GraphQL request.
    /// <br/><br/>
    /// Requires <see cref="IHttpContextAccessor"/> to be registered within the dependency injection framework
    /// if calling <see cref="DocumentExecuter.ExecuteAsync(ExecutionOptions)"/> directly.
    /// </summary>
    public static IGraphQLBuilder AddUserContextBuilder<TUserContextBuilder>(this IGraphQLBuilder builder, ServiceLifetime serviceLifetime = ServiceLifetime.Singleton)
        where TUserContextBuilder : class, IUserContextBuilder
    {
        builder.Services.Register<IUserContextBuilder, TUserContextBuilder>(serviceLifetime);
        builder.ConfigureExecutionOptions(async options => {
            if (options.UserContext == null || options.UserContext.Count == 0 && options.UserContext.GetType() == typeof(Dictionary<string, object>)) {
                var requestServices = options.RequestServices ?? throw new MissingRequestServicesException();
                var httpContext = requestServices.GetRequiredService<IHttpContextAccessor>().HttpContext!;
                var contextBuilder = requestServices.GetRequiredService<IUserContextBuilder>();
                options.UserContext = await contextBuilder.BuildUserContextAsync(httpContext);
            }
        });

        return builder;
    }

    /// <summary>
    /// Configures a delegate to be used to create a user context for each GraphQL request.
    /// <br/><br/>
    /// Requires <see cref="IHttpContextAccessor"/> to be registered within the dependency injection framework
    /// if calling <see cref="DocumentExecuter.ExecuteAsync(ExecutionOptions)"/> directly.
    /// </summary>
    public static IGraphQLBuilder AddUserContextBuilder<TUserContext>(this IGraphQLBuilder builder, Func<HttpContext, TUserContext> creator)
        where TUserContext : class, IDictionary<string, object?>
    {
        builder.Services.Register<IUserContextBuilder>(new UserContextBuilder<TUserContext>(creator ?? throw new ArgumentNullException(nameof(creator))));
        builder.ConfigureExecutionOptions(options => {
            if (options.UserContext == null || options.UserContext.Count == 0 && options.UserContext.GetType() == typeof(Dictionary<string, object>)) {
                var requestServices = options.RequestServices ?? throw new MissingRequestServicesException();
                var httpContext = requestServices.GetRequiredService<IHttpContextAccessor>().HttpContext!;
                options.UserContext = creator(httpContext);
            }
        });

        return builder;
    }

    /// <inheritdoc cref="AddUserContextBuilder{TUserContext}(IGraphQLBuilder, Func{HttpContext, TUserContext})"/>
    public static IGraphQLBuilder AddUserContextBuilder<TUserContext>(this IGraphQLBuilder builder, Func<HttpContext, Task<TUserContext>> creator)
        where TUserContext : class, IDictionary<string, object?>
    {
        if (creator == null)
            throw new ArgumentNullException(nameof(creator));
        builder.Services.Register<IUserContextBuilder>(new UserContextBuilder<TUserContext>(context => new(creator(context))));
        builder.ConfigureExecutionOptions(async options => {
            if (options.UserContext == null || options.UserContext.Count == 0 && options.UserContext.GetType() == typeof(Dictionary<string, object>)) {
                var requestServices = options.RequestServices ?? throw new MissingRequestServicesException();
                var httpContext = requestServices.GetRequiredService<IHttpContextAccessor>().HttpContext!;
                options.UserContext = await creator(httpContext);
            }
        });

        return builder;
    }
}
