using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace GraphQL.AspNetCore3.JwtBearer;

/// <summary>
/// Options for JWT Bearer authentication in GraphQL WebSocket connections.
/// </summary>
public class JwtBearerAuthenticationOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether JWT events should be enabled.
    /// When enabled, the <see cref="JwtWebSocketAuthenticationService"/> will raise the
    /// <see cref="JwtBearerEvents.MessageReceived"/>, <see cref="JwtBearerEvents.TokenValidated"/>, 
    /// and <see cref="JwtBearerEvents.AuthenticationFailed"/> events as appropriate.
    /// </summary>
    public bool EnableJwtEvents { get; set; }
}
