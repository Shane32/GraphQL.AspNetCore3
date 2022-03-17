using ServiceLifetime = GraphQL.DI.ServiceLifetime;

namespace Shane32.GraphQL.AspNetCore;

/// <summary>
/// GraphQL specific extension methods for <see cref="IGraphQLBuilder"/>.
/// </summary>
public static class GraphQLBuilderExtensions
{
    /// <summary>
    /// Registers HTTP middleware with the dependency injection framework.
    /// </summary>
    public static IGraphQLBuilder AddHttpMiddleware(this IGraphQLBuilder builder)
        => AddHttpMiddleware<ISchema>(builder);

    /// <summary>
    /// Registers HTTP middleware for the specified schema with the dependency injection framework.
    /// </summary>
    public static IGraphQLBuilder AddHttpMiddleware<TSchema>(this IGraphQLBuilder builder)
        where TSchema : ISchema
        => AddHttpMiddleware<TSchema, GraphQLHttpMiddleware<TSchema>>(builder);

    /// <summary>
    /// Registers HTTP middleware for the specified schema with the dependency injection framework.
    /// </summary>
    public static IGraphQLBuilder AddHttpMiddleware<TSchema, TMiddleware>(this IGraphQLBuilder builder)
        where TSchema : ISchema
        where TMiddleware : GraphQLHttpMiddleware<TSchema>
    {
        builder.Services.TryRegister<IWebSocketHandler, WebSocketHandler>(ServiceLifetime.Singleton);
        builder.Services.Register<TMiddleware>(ServiceLifetime.Singleton);
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
        builder.ConfigureExecutionOptions(async options =>
        {
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
        builder.ConfigureExecutionOptions(options =>
        {
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
        builder.ConfigureExecutionOptions(async options =>
        {
            if (options.UserContext == null || options.UserContext.Count == 0 && options.UserContext.GetType() == typeof(Dictionary<string, object>)) {
                var requestServices = options.RequestServices ?? throw new MissingRequestServicesException();
                var httpContext = requestServices.GetRequiredService<IHttpContextAccessor>().HttpContext!;
                options.UserContext = await creator(httpContext);
            }
        });

        return builder;
    }
}
