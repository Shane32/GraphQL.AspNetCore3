namespace GraphQL.AspNetCore3.WebSockets.GraphQLWs;

/// <summary>
/// The payload of the ping message.
/// </summary>
public class PingPayload
{
    /// <summary>
    /// The unique identifier of the ping message.
    /// </summary>
    public string? id { get; set; }
}
