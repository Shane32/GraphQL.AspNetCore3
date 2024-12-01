namespace GraphQL.AspNetCore3;

#if NETSTANDARD2_0 || NETCOREAPP2_1

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Performs no operation.
    /// </summary>
    [Obsolete("This method has no functionality and will be removed in future versions of this library.")]
    public static void AddHostApplicationLifetime(this IServiceCollection services)
    {
    }
}

#endif
