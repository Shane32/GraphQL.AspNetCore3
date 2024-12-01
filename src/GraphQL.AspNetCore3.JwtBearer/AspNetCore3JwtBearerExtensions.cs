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
    {
        builder.AddWebSocketAuthentication<JwtWebSocketAuthenticationService>();
        return builder;
    }
}
