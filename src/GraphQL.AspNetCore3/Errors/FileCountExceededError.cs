namespace GraphQL.AspNetCore3.Errors;

/// <summary>
/// Represents an error when too many files are uploaded in a GraphQL request.
/// </summary>
public class FileCountExceededError : RequestError, IHasPreferredStatusCode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileCountExceededError"/> class.
    /// </summary>
    public FileCountExceededError()
        : base("File uploads exceeded.")
    {
    }

    /// <inheritdoc/>
    public HttpStatusCode PreferredStatusCode { get; set; } = HttpStatusCode.RequestEntityTooLarge;
}
