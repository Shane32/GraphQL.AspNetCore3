using GraphQL.AspNetCore3;
using GraphQL.AspNetCore3.JwtBearer;
using GraphQL.DI;

namespace GraphQL;

/// <summary>
/// Extension methods for adding JWT bearer authentication to a GraphQL server for WebSocket communications.
/// </summary>
public static class AspNetCore3JwtBearerExtensions
{
    /// <summary>
    /// Adds JWT bearer authentication to a GraphQL server for WebSocket communications.
    /// </summary>
    public static IGraphQLBuilder AddJwtBearerAuthentication(this IGraphQLBuilder builder)
        => builder.AddJwtBearerAuthentication(options => { });

    /// <inheritdoc cref="AddJwtBearerAuthentication(IGraphQLBuilder)"/>
    public static IGraphQLBuilder AddJwtBearerAuthentication(this IGraphQLBuilder builder, bool enableJwtEvents)
        => builder.AddJwtBearerAuthentication(options => options.EnableJwtEvents = enableJwtEvents);

    /// <inheritdoc cref="AddJwtBearerAuthentication(IGraphQLBuilder)"/>
    public static IGraphQLBuilder AddJwtBearerAuthentication(this IGraphQLBuilder builder, Action<JwtBearerAuthenticationOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);
        builder.AddWebSocketAuthentication<JwtWebSocketAuthenticationService>();
        return builder;
    }
}
