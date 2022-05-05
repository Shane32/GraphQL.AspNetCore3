#if NETSTANDARD2_0 || NETCOREAPP2_1

namespace GraphQL.AspNetCore3;

internal interface IAsyncDisposable
{
    ValueTask DisposeAsync();
}

#endif
