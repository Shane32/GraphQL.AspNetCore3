namespace Shane32.GraphQL.AspNetCore.WebSockets;

// Copyright (c) 2022 Shane Krueger
// source: https://github.com/Shane32/AsyncResetEvents/blob/4e0e50226bef91747ccc4c62950c52eb7fe740fd/src/AsyncResetEvents/AsyncMessagePump.cs
// license: https://github.com/Shane32/AsyncResetEvents/blob/4e0e50226bef91747ccc4c62950c52eb7fe740fd/LICENSE

/// <summary>
/// <para>
/// An asynchronous message pump, where messages can be posted
/// on multiple threads and executed sequentially.
/// </para>
/// <para>
/// The callback functions are guaranteed to be executed in the
/// same order that <see cref="Post(T)"/> was called.
/// </para>
/// <para>
/// Callback functions may be synchronous or asynchronous.
/// Since callback functions may execute on the same thread that
/// <see cref="Post(T)"/> was called on, it is not suggested
/// to use long-running synchronous tasks within the callback
/// delegate without the use of <see cref="Task.Yield"/>.
/// </para>
/// </summary>
internal class AsyncMessagePump<T>
{
    private readonly Func<T, Task> _callback;
    private readonly Queue<ValueTask<T>> _queue = new();

    /// <summary>
    /// Initializes a new instances with the specified asynchronous callback delegate.
    /// </summary>
    public AsyncMessagePump(Func<T, Task> callback)
    {
        _callback = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    /// <summary>
    /// Initializes a new instances with the specified synchronous callback delegate.
    /// </summary>
    public AsyncMessagePump(Action<T> callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));
        _callback = message => {
            callback(message);
            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// Posts the specified message to the message queue.
    /// </summary>
    public void Post(T message)
        => Post(new ValueTask<T>(message));

    /// <summary>
    /// Posts the result of an asynchronous operation to the message queue.
    /// </summary>
    public void Post(ValueTask<T> messageTask)
    {
        bool attach = false;
        lock (_queue) {
            _queue.Enqueue(messageTask);
            attach = _queue.Count == 1;
        }

        if (attach) {
            CompleteAsync();
        }
    }

    /// <summary>
    /// Processes message in the queue until it is empty.
    /// </summary>
    private async void CompleteAsync()
    {
        // grab the message at the start of the queue, but don't remove it from the queue
        ValueTask<T> messageTask;
        lock (_queue) {
            // should always successfully peek from the queue here
#pragma warning disable CA2012 // Use ValueTasks correctly
            messageTask = _queue.Peek();
#pragma warning restore CA2012 // Use ValueTasks correctly
        }
        while (true) {
            // process the message
            try {
                var message = await messageTask.ConfigureAwait(false);
                await _callback(message).ConfigureAwait(false);
            } catch (Exception ex) {
                try {
                    await HandleErrorAsync(ex);
                } catch { }
            }

            // once the message has been passed along, dequeue it
            lock (_queue) {
#pragma warning disable CA2012 // Use ValueTasks correctly
                _ = _queue.Dequeue();
#pragma warning restore CA2012 // Use ValueTasks correctly
                // if the queue is empty, immedately quit the loop, as any new
                // events queued will start CompleteAsync
                if (!_queue.TryPeek(out messageTask!))
                    return;
            }
        }
    }

    /// <summary>
    /// Handles exceptions that occur within the asynchronous message delegate or the callback.
    /// </summary>
    protected virtual Task HandleErrorAsync(Exception exception)
        => Task.CompletedTask;
}
