namespace GraphQL.AspNetCore3.WebSockets;

/// <summary>
/// Represents an authentication request within the GraphQL ASP.NET Core WebSocket context.
/// </summary>
public class AuthenticationRequest
{
    /// <summary>
    /// Gets the WebSocket connection associated with the authentication request.
    /// </summary>
    /// <value>
    /// An instance of <see cref="IWebSocketConnection"/> representing the active WebSocket connection.
    /// </value>
    public IWebSocketConnection Connection { get; }

    /// <summary>
    /// Gets the subprotocol used for the WebSocket communication.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> specifying the subprotocol negotiated for the WebSocket connection.
    /// </value>
    public string SubProtocol { get; }

    /// <summary>
    /// Gets the operation message containing details of the authentication operation.
    /// </summary>
    /// <value>
    /// An instance of <see cref="OperationMessage"/> that encapsulates the specifics of the authentication request.
    /// </value>
    public OperationMessage OperationMessage { get; }

    /// <summary>
    /// Gets a list of the authentication schemes the authentication requirements are evaluated against.
    /// When no schemes are specified, the default authentication scheme is used.
    /// </summary>
    public IEnumerable<string> AuthenticationSchemes { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRequest"/> class.
    /// </summary>
    public AuthenticationRequest(IWebSocketConnection connection, string subProtocol, OperationMessage operationMessage, IEnumerable<string> authenticationSchemes)
    {
        Connection = connection;
        SubProtocol = subProtocol;
        OperationMessage = operationMessage;
        AuthenticationSchemes = authenticationSchemes;
    }
}
