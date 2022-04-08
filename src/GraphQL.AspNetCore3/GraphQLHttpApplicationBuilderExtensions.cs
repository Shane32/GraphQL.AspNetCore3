namespace GraphQL.AspNetCore3;

/// <summary>
/// Extensions for <see cref="IApplicationBuilder"/> to add <see cref="GraphQLHttpMiddleware{TSchema}"/>
/// or its descendants in the HTTP request pipeline.
/// </summary>
public static class GraphQLHttpApplicationBuilderExtensions
{
    /// <summary>
    /// Add the GraphQL middleware to the HTTP request pipeline.
    /// <br/><br/>
    /// Uses the GraphQL schema registered as <see cref="ISchema"/> within the dependency injection
    /// framework to execute the query.
    /// </summary>
    /// <param name="builder">The application builder</param>
    /// <param name="path">The path to the GraphQL endpoint which defaults to '/graphql'</param>
    /// <param name="configureMiddleware">A delegate to configure the middleware</param>
    /// <returns>The <see cref="IApplicationBuilder"/> received as parameter</returns>
    public static IApplicationBuilder UseGraphQL(this IApplicationBuilder builder, string path = "/graphql", Action<GraphQLHttpMiddlewareOptions>? configureMiddleware = null)
        => builder.UseGraphQL<ISchema>(path, configureMiddleware);

    /// <summary>
    /// Add the GraphQL middleware to the HTTP request pipeline.
    /// <br/><br/>
    /// Uses the GraphQL schema registered as <see cref="ISchema"/> within the dependency injection
    /// framework to execute the query.
    /// </summary>
    /// <param name="builder">The application builder</param>
    /// <param name="path">The path to the GraphQL endpoint</param>
    /// <param name="configureMiddleware">A delegate to configure the middleware</param>
    /// <returns>The <see cref="IApplicationBuilder"/> received as parameter</returns>
    public static IApplicationBuilder UseGraphQL(this IApplicationBuilder builder, PathString path, Action<GraphQLHttpMiddlewareOptions>? configureMiddleware = null)
        => builder.UseGraphQL<ISchema>(path, configureMiddleware);

    /// <summary>
    /// Add the GraphQL middleware to the HTTP request pipeline for the specified schema.
    /// </summary>
    /// <typeparam name="TSchema">The implementation of <see cref="ISchema"/> to use</typeparam>
    /// <param name="builder">The application builder</param>
    /// <param name="path">The path to the GraphQL endpoint which defaults to '/graphql'</param>
    /// <param name="configureMiddleware">A delegate to configure the middleware</param>
    /// <returns>The <see cref="IApplicationBuilder"/> received as parameter</returns>
    public static IApplicationBuilder UseGraphQL<TSchema>(this IApplicationBuilder builder, string path = "/graphql", Action<GraphQLHttpMiddlewareOptions>? configureMiddleware = null)
        where TSchema : ISchema
        => builder.UseGraphQL<TSchema>(new PathString(path), configureMiddleware);

    /// <summary>
    /// Add the GraphQL middleware to the HTTP request pipeline for the specified schema.
    /// </summary>
    /// <typeparam name="TSchema">The implementation of <see cref="ISchema"/> to use</typeparam>
    /// <param name="builder">The application builder</param>
    /// <param name="path">The path to the GraphQL endpoint</param>
    /// <param name="configureMiddleware">A delegate to configure the middleware</param>
    /// <returns>The <see cref="IApplicationBuilder"/> received as parameter</returns>
    public static IApplicationBuilder UseGraphQL<TSchema>(this IApplicationBuilder builder, PathString path, Action<GraphQLHttpMiddlewareOptions>? configureMiddleware = null)
        where TSchema : ISchema
    {
        var opts = new GraphQLHttpMiddlewareOptions();
        configureMiddleware?.Invoke(opts);
        return builder.UseWhen(
            context => context.Request.Path.StartsWithSegments(path, out var remaining) && string.IsNullOrEmpty(remaining),
            b => b.UseMiddleware<GraphQLHttpMiddleware<TSchema>>(opts));
    }

    /// <summary>
    /// Add the GraphQL custom middleware to the HTTP request pipeline for the specified schema.
    /// </summary>
    /// <typeparam name="TMiddleware">Custom middleware inherited from <see cref="GraphQLHttpMiddleware{TSchema}"/></typeparam>
    /// <param name="builder">The application builder</param>
    /// <param name="path">The path to the GraphQL endpoint which defaults to '/graphql'</param>
    /// <returns>The <see cref="IApplicationBuilder"/> received as parameter</returns>
    public static IApplicationBuilder UseGraphQL<TMiddleware>(this IApplicationBuilder builder, string path = "/graphql")
        where TMiddleware : GraphQLHttpMiddleware
        => builder.UseGraphQL<TMiddleware>(new PathString(path));

    /// <summary>
    /// Add the GraphQL custom middleware to the HTTP request pipeline for the specified schema.
    /// </summary>
    /// <typeparam name="TMiddleware">Custom middleware inherited from <see cref="GraphQLHttpMiddleware{TSchema}"/></typeparam>
    /// <param name="builder">The application builder</param>
    /// <param name="path">The path to the GraphQL endpoint</param>
    /// <returns>The <see cref="IApplicationBuilder"/> received as parameter</returns>
    public static IApplicationBuilder UseGraphQL<TMiddleware>(this IApplicationBuilder builder, PathString path)
        where TMiddleware : GraphQLHttpMiddleware
    {
        return builder.UseWhen(
            context => context.Request.Path.StartsWithSegments(path, out var remaining) && string.IsNullOrEmpty(remaining),
            b => b.UseMiddleware<TMiddleware>());
    }
}