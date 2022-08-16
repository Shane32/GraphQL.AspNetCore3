using ServiceLifetime = GraphQL.DI.ServiceLifetime;

namespace GraphQL.AspNetCore3;

/// <summary>
/// GraphQL specific extension methods for <see cref="IGraphQLBuilder"/>.
/// </summary>
public static class GraphQLBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="AuthorizationValidationRule"/> with the dependency injection framework
    /// and configures it to be used when executing a request.
    /// </summary>
    [Obsolete("Please use AddAuthorizationRule")]
    public static IGraphQLBuilder AddAuthorization(this IGraphQLBuilder builder)
    {
        builder.AddValidationRule<AuthorizationValidationRule>(true);
        return builder;
    }

    /// <summary>
    /// Registers <see cref="AuthorizationValidationRule"/> with the dependency injection framework
    /// and configures it to be used when executing a request.
    /// </summary>
    public static IGraphQLBuilder AddAuthorizationRule(this IGraphQLBuilder builder)
    {
        builder.AddValidationRule<AuthorizationValidationRule>(true);
        return builder;
    }

    /// <summary>
    /// Registers <typeparamref name="TWebSocketAuthenticationService"/> with the dependency injection framework
    /// as a singleton of type <see cref="IWebSocketAuthenticationService"/>.
    /// </summary>
    public static IGraphQLBuilder AddWebSocketAuthentication<TWebSocketAuthenticationService>(this IGraphQLBuilder builder)
        where TWebSocketAuthenticationService : class, IWebSocketAuthenticationService
    {
        builder.Services.Register<IWebSocketAuthenticationService, TWebSocketAuthenticationService>(ServiceLifetime.Singleton);
        return builder;
    }

    /// <summary>
    /// Registers a service of type <see cref="IWebSocketAuthenticationService"/> with the specified factory delegate
    /// with the dependency injection framework as a singleton.
    /// </summary>
    public static IGraphQLBuilder AddWebSocketAuthentication(this IGraphQLBuilder builder, Func<IServiceProvider, IWebSocketAuthenticationService> factory)
    {
        builder.Services.Register(factory, ServiceLifetime.Singleton);
        return builder;
    }

    /// <summary>
    /// Registers a specified instance of type <see cref="IWebSocketAuthenticationService"/> with the
    /// dependency injection framework.
    /// </summary>
    public static IGraphQLBuilder AddWebSocketAuthentication(this IGraphQLBuilder builder, IWebSocketAuthenticationService webSocketAuthenticationService)
    {
        builder.Services.Register(webSocketAuthenticationService);
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
                options.UserContext = await contextBuilder.BuildUserContextAsync(httpContext, null);
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

    /// <summary>
    /// Configures a delegate to be used to create a user context for each GraphQL request.
    /// <br/><br/>
    /// Requires <see cref="IHttpContextAccessor"/> to be registered within the dependency injection framework
    /// if calling <see cref="DocumentExecuter.ExecuteAsync(ExecutionOptions)"/> directly.
    /// </summary>
    public static IGraphQLBuilder AddUserContextBuilder<TUserContext>(this IGraphQLBuilder builder, Func<HttpContext, object?, TUserContext> creator)
        where TUserContext : class, IDictionary<string, object?>
    {
        builder.Services.Register<IUserContextBuilder>(new UserContextBuilder<TUserContext>(creator ?? throw new ArgumentNullException(nameof(creator))));
        builder.ConfigureExecutionOptions(options => {
            if (options.UserContext == null || options.UserContext.Count == 0 && options.UserContext.GetType() == typeof(Dictionary<string, object>)) {
                var requestServices = options.RequestServices ?? throw new MissingRequestServicesException();
                var httpContext = requestServices.GetRequiredService<IHttpContextAccessor>().HttpContext!;
                options.UserContext = creator(httpContext, null);
            }
        });

        return builder;
    }

    /// <inheritdoc cref="AddUserContextBuilder{TUserContext}(IGraphQLBuilder, Func{HttpContext, TUserContext})"/>
    public static IGraphQLBuilder AddUserContextBuilder<TUserContext>(this IGraphQLBuilder builder, Func<HttpContext, object?, Task<TUserContext>> creator)
        where TUserContext : class, IDictionary<string, object?>
    {
        if (creator == null)
            throw new ArgumentNullException(nameof(creator));
        builder.Services.Register<IUserContextBuilder>(new UserContextBuilder<TUserContext>((context, payload) => new(creator(context, payload))));
        builder.ConfigureExecutionOptions(async options => {
            if (options.UserContext == null || options.UserContext.Count == 0 && options.UserContext.GetType() == typeof(Dictionary<string, object>)) {
                var requestServices = options.RequestServices ?? throw new MissingRequestServicesException();
                var httpContext = requestServices.GetRequiredService<IHttpContextAccessor>().HttpContext!;
                options.UserContext = await creator(httpContext, null);
            }
        });

        return builder;
    }
}
