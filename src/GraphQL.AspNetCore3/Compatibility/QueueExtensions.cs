#if NETSTANDARD2_0

namespace GraphQL.AspNetCore3;

internal static class QueueExtensions
{
    public static bool TryPeek<T>(this Queue<T> queue, out T? value)
    {
        if (queue.Count == 0) {
            value = default;
            return false;
        } else {
            value = queue.Peek();
            return true;
        }
    }
}

#endif
