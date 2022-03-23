namespace Shane32.GraphQL.AspNetCore.WebSockets;

/// <summary>
/// Represents a WebSocket connection, dispatching received messages over
/// the connection to the specified <see cref="IOperationMessageReceiveStream"/>,
/// and sending requested messages out the connection when requested
/// through the <see cref="IOperationMessageSendStream"/>.
/// </summary>
public interface IWebSocketConnection : IOperationMessageSendStream
{
    /// <summary>
    /// Listens to incoming messages over the WebSocket connection,
    /// dispatching the messages to the specified <paramref name="operationMessageReceiveStream"/>.
    /// Returns or throws <see cref="OperationCanceledException"/> when the WebSocket connection is closed.
    /// </summary>
    Task ExecuteAsync(IOperationMessageReceiveStream operationMessageReceiveStream);
}
