namespace GraphQL.AspNetCore3.WebSockets;

/// <summary>
/// Authorizes an incoming GraphQL over WebSockets request with the
/// connection initialization message.  A typical implementation will
/// set the <see cref="HttpContext.User"/> property after reading the
/// authorization token.  This service must be registered as a singleton
/// in the dependency injection framework.
/// </summary>
public interface IWebSocketAuthorizationService
{
    /// <summary>
    /// Authorizes an incoming GraphQL over WebSockets request with the
    /// connection initialization message.  A typical implementation will
    /// set the <see cref="HttpContext.User"/> property after reading the
    /// authorization token.
    /// <br/><br/>
    /// Return <see langword="true"/> if authorization is successful, or
    /// return <see langword="false"/> if not.  You may choose to call
    /// <see cref="IWebSocketConnection.CloseConnectionAsync(int, string?)"/>
    /// with an appropriate error number and message.
    /// </summary>
    ValueTask<bool> AuthorizeAsync(IWebSocketConnection connection, OperationMessage operationMessage);
}
