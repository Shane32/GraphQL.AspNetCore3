using ServiceLifetime = GraphQL.DI.ServiceLifetime;

namespace Shane32.GraphQL.AspNetCore;

/// <summary>
/// GraphQL specific extension methods for <see cref="IGraphQLBuilder"/>.
/// </summary>
public static class GraphQLBuilderExtensions
{
    /// <summary>
    /// Registers the default HTTP middleware and WebSockets handler with the dependency injection framework.
    /// </summary>
    public static IGraphQLBuilder AddServer(this IGraphQLBuilder builder, Action<GraphQLHttpMiddlewareOptions>? configureMiddleware = null, Action<WebSocketHandlerOptions>? configureWebSockets = null)
    {
        AddHttpMiddleware(builder, configureMiddleware);
        AddWebSocketHandler(builder, configureWebSockets);
        return builder;
    }

    /// <summary>
    /// Registers the default HTTP middleware and WebSockets handler with the dependency injection framework.
    /// </summary>
    public static IGraphQLBuilder AddServer(this IGraphQLBuilder builder, Action<GraphQLHttpMiddlewareOptions, IServiceProvider>? configureMiddleware, Action<WebSocketHandlerOptions, IServiceProvider>? configureWebSockets = null)
    {
        AddHttpMiddleware(builder, configureMiddleware);
        AddWebSocketHandler(builder, configureWebSockets);
        return builder;
    }

    /// <summary>
    /// Registers the default HTTP middleware with the dependency injection framework.
    /// </summary>
    public static IGraphQLBuilder AddHttpMiddleware(this IGraphQLBuilder builder, Action<GraphQLHttpMiddlewareOptions>? configure = null)
    {
        builder.Services.Register(typeof(GraphQLHttpMiddleware<>), typeof(GraphQLHttpMiddleware<>), ServiceLifetime.Singleton);
        builder.Services.Configure(configure);
        builder.Services.TryRegister<IHttpContextAccessor, HttpContextAccessor>(ServiceLifetime.Singleton);
        return builder;
    }

    /// <summary>
    /// Registers the default HTTP middleware with the dependency injection framework.
    /// </summary>
    public static IGraphQLBuilder AddHttpMiddleware(this IGraphQLBuilder builder, Action<GraphQLHttpMiddlewareOptions, IServiceProvider>? configure)
    {
        builder.Services.Register(typeof(GraphQLHttpMiddleware<>), typeof(GraphQLHttpMiddleware<>), ServiceLifetime.Singleton);
        builder.Services.Configure(configure);
        builder.Services.TryRegister<IHttpContextAccessor, HttpContextAccessor>(ServiceLifetime.Singleton);
        return builder;
    }

    /// <summary>
    /// Registers HTTP middleware for the specified schema with the dependency injection framework.
    /// </summary>
    public static IGraphQLBuilder AddHttpMiddleware<TSchema, TMiddleware>(this IGraphQLBuilder builder)
        where TSchema : ISchema
        where TMiddleware : GraphQLHttpMiddleware<TSchema>
    {
        builder.Services.Register<TMiddleware>(ServiceLifetime.Singleton);
        return builder;
    }

    /// <summary>
    /// Registers the default WebSocket handler with the dependency injection framework and
    /// optionally configures it with the specified configuration delegate.
    /// </summary>
    public static IGraphQLBuilder AddWebSocketHandler(this IGraphQLBuilder builder, Action<WebSocketHandlerOptions>? configure = null)
    {
        builder.Services.Register<IWebSocketHandler, WebSocketHandler>(ServiceLifetime.Singleton);
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
    /// Set up a delegate to create the UserContext for each GraphQL request.
    /// </summary>
    public static IGraphQLBuilder AddUserContextBuilder<TUserContext>(this IGraphQLBuilder builder, Func<HttpContext, TUserContext> creator)
        where TUserContext : class, IDictionary<string, object?>
    {
        builder.Services.Register<IUserContextBuilder>(new UserContextBuilder<TUserContext>(creator));
        builder.ConfigureExecutionOptions(options => {
            if (options.UserContext == null || options.UserContext.Count == 0 && options.UserContext.GetType() == typeof(Dictionary<string, object>)) {
                var requestServices = options.RequestServices ?? throw new MissingRequestServicesException();
                var httpContext = requestServices.GetRequiredService<IHttpContextAccessor>().HttpContext!;
                options.UserContext = creator(httpContext);
            }
        });

        return builder;
    }

    /// <summary>
    /// Set up a delegate to create the UserContext for each GraphQL request.
    /// </summary>
    public static IGraphQLBuilder AddUserContextBuilder<TUserContext>(this IGraphQLBuilder builder, Func<HttpContext, Task<TUserContext>> creator)
        where TUserContext : class, IDictionary<string, object?>
    {
        builder.Services.Register<IUserContextBuilder>(new UserContextBuilder<TUserContext>(creator));
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
