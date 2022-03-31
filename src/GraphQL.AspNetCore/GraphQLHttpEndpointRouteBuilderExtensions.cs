namespace GraphQL.AspNetCore;

/// <summary>
/// Extensions for <see cref="IEndpointRouteBuilder"/> to add <see cref="GraphQLHttpMiddleware{TSchema}"/>
/// or its descendants in the HTTP request pipeline.
/// </summary>
public static class GraphQLHttpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Add the GraphQL middleware to the HTTP request pipeline.
    /// <br/><br/>
    /// Uses the GraphQL schema registered as <see cref="ISchema"/> within the dependency injection
    /// framework to execute the query.
    /// </summary>
    /// <param name="endpoints">Defines a contract for a route builder in an application. A route builder specifies the routes for an application.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="configureMiddleware">A delegate to configure the middleware</param>
    /// <returns>The <see cref="IApplicationBuilder"/> received as parameter</returns>
    public static GraphQLEndpointConventionBuilder MapGraphQL(this IEndpointRouteBuilder endpoints, string pattern = "graphql", Action<GraphQLHttpMiddlewareOptions>? configureMiddleware = null)
        => endpoints.MapGraphQL<ISchema>(pattern, configureMiddleware);

    /// <summary>
    /// Add the GraphQL middleware to the HTTP request pipeline for the specified schema.
    /// </summary>
    /// <typeparam name="TSchema">The implementation of <see cref="ISchema"/> to use</typeparam>
    /// <param name="endpoints">Defines a contract for a route builder in an application. A route builder specifies the routes for an application.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <param name="configureMiddleware">A delegate to configure the middleware</param>
    /// <returns>The <see cref="IApplicationBuilder"/> received as parameter</returns>
    public static GraphQLEndpointConventionBuilder MapGraphQL<TSchema>(this IEndpointRouteBuilder endpoints, string pattern = "graphql", Action<GraphQLHttpMiddlewareOptions>? configureMiddleware = null)
        where TSchema : ISchema
    {
        var opts = new GraphQLHttpMiddlewareOptions();
        configureMiddleware?.Invoke(opts);

        var requestDelegate = endpoints.CreateApplicationBuilder().UseMiddleware<GraphQLHttpMiddleware<TSchema>>(opts).Build();
        return new GraphQLEndpointConventionBuilder(endpoints.Map(pattern, requestDelegate).WithDisplayName("GraphQL"));
    }

    /// <summary>
    /// Add the GraphQL middleware to the HTTP request pipeline for the specified schema.
    /// </summary>
    /// <typeparam name="TSchema">The implementation of <see cref="ISchema"/> to use</typeparam>
    /// <typeparam name="TMiddleware">Custom middleware inherited from <see cref="GraphQLHttpMiddleware{TSchema}"/></typeparam>
    /// <param name="endpoints">Defines a contract for a route builder in an application. A route builder specifies the routes for an application.</param>
    /// <param name="pattern">The route pattern.</param>
    /// <returns>The <see cref="IApplicationBuilder"/> received as parameter</returns>
    public static GraphQLEndpointConventionBuilder MapGraphQL<TSchema, TMiddleware>(this IEndpointRouteBuilder endpoints, string pattern = "graphql")
        where TSchema : ISchema
        where TMiddleware : GraphQLHttpMiddleware<TSchema>
    {
        var requestDelegate = endpoints.CreateApplicationBuilder().UseMiddleware<TMiddleware>().Build();
        return new GraphQLEndpointConventionBuilder(endpoints.Map(pattern, requestDelegate).WithDisplayName("GraphQL"));
    }
}

/// <summary>
/// Builds conventions that will be used for customization of Microsoft.AspNetCore.Builder.EndpointBuilder instances.
/// Special convention builder that allows you to write specific extension methods for ASP.NET Core routing subsystem.
/// </summary>
public class GraphQLEndpointConventionBuilder : IEndpointConventionBuilder
{
    private readonly IEndpointConventionBuilder _builder;

    internal GraphQLEndpointConventionBuilder(IEndpointConventionBuilder builder)
    {
        _builder = builder;
    }

    /// <inheritdoc />
    public void Add(Action<EndpointBuilder> convention) => _builder.Add(convention);
}
