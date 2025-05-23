namespace GraphQL.AspNetCore3;

/// <summary>
/// Configuration options for <see cref="GraphQLHttpMiddleware"/>.
/// </summary>
public class GraphQLHttpMiddlewareOptions : IAuthorizationOptions
{
    /// <summary>
    /// Enables handling of GET requests.
    /// </summary>
    public bool HandleGet { get; set; } = true;

    /// <summary>
    /// Enables handling of POST requests, including form submissions, JSON-formatted requests and raw query requests.
    /// </summary>
    public bool HandlePost { get; set; } = true;

    /// <summary>
    /// Enables handling of WebSockets requests.
    /// <br/><br/>
    /// Requires calling <see cref="WebSocketMiddlewareExtensions.UseWebSockets(IApplicationBuilder)"/>
    /// to initialize the WebSocket pipeline within the ASP.NET Core framework.
    /// </summary>
    public bool HandleWebSockets { get; set; } = true;

    /// <summary>
    /// Enables handling of batched GraphQL requests for POST requests when formatted as JSON.
    /// </summary>
    public bool EnableBatchedRequests { get; set; } = true;

    /// <summary>
    /// Enables parallel execution of batched GraphQL requests.
    /// </summary>
    public bool ExecuteBatchedRequestsInParallel { get; set; } = true;

    /// <summary>
    /// When enabled, GraphQL requests with validation errors
    /// have the HTTP status code set to 400 Bad Request.
    /// GraphQL requests with execution errors are unaffected.
    /// <br/><br/>
    /// Does not apply to batched or WebSocket requests.
    /// </summary>
    public bool ValidationErrorsReturnBadRequest { get; set; } = true;

    /// <summary>
    /// Enables parsing the query string on POST requests.
    /// If enabled, the query string properties override those in the body of the request.
    /// </summary>
    public bool ReadQueryStringOnPost { get; set; } = true;

    /// <summary>
    /// Enables parsing POST requests with the form content types such as <c>multipart-form/data</c>.
    /// </summary>
    /// <remarks>
    /// There is a potential security vulnerability when employing cookie authentication
    /// with the <c>multipart-form/data</c> content type because sending cookies
    /// alongside the request does not initiate a pre-flight CORS request.
    /// As a result, GraphQL.NET carries out the request and potentially modifies data,
    /// even if the CORS policy forbids it, irrespective of the sender's ability to access
    /// the response.  With <see cref="CsrfProtectionEnabled"/> enabled, these requests
    /// are blocked the request contains a non-empty header from the
    /// <see cref="CsrfProtectionHeaders"/> list, providing a measure of protection.
    /// </remarks>
    public bool ReadFormOnPost { get; set; }

    /// <summary>
    /// Enables cross-site request forgery (CSRF) protection for both GET and POST requests.
    /// Requires a non-empty header from the <see cref="CsrfProtectionHeaders"/> list to be
    /// present, or a POST request with a Content-Type header that is not <c>text/plain</c>,
    /// <c>application/x-www-form-urlencoded</c>, or <c>multipart/form-data</c>.
    /// </summary>
    public bool CsrfProtectionEnabled { get; set; } = true;

    /// <summary>
    /// When <see cref="CsrfProtectionEnabled"/> is enabled, requests require a non-empty
    /// header from this list or a POST request with a Content-Type header that is not
    /// <c>text/plain</c>, <c>application/x-www-form-urlencoded</c>, or <c>multipart/form-data</c>.
    /// Defaults to <c>GraphQL-Require-Preflight</c>.
    /// </summary>
    public List<string> CsrfProtectionHeaders { get; set; } = ["GraphQL-Require-Preflight"]; // see https://github.com/graphql/graphql-over-http/pull/303

    /// <summary>
    /// Enables reading variables from the query string.
    /// Variables are interpreted as JSON and deserialized before being
    /// provided to the <see cref="IDocumentExecuter"/>.
    /// </summary>
    public bool ReadVariablesFromQueryString { get; set; } = true;

    /// <summary>
    /// Enables reading extensions from the query string.
    /// Extensions are interpreted as JSON and deserialized before being
    /// provided to the <see cref="IDocumentExecuter"/>.
    /// </summary>
    public bool ReadExtensionsFromQueryString { get; set; } = true;

    /// <summary>
    /// Gets or sets a list of the authentication schemes the authentication requirements are evaluated against.
    /// When no schemes are specified, the default authentication scheme is used.
    /// </summary>
    public List<string> AuthenticationSchemes { get; set; } = new();

    IEnumerable<string> IAuthorizationOptions.AuthenticationSchemes => AuthenticationSchemes;

    /// <inheritdoc/>
    /// <remarks>
    /// HTTP requests return <c>401 Forbidden</c> when the request is not authenticated.
    /// </remarks>
    public bool AuthorizationRequired { get; set; }

    /// <inheritdoc cref="IAuthorizationOptions.AuthorizedRoles"/>
    /// <remarks>
    /// HTTP requests return <c>403 Forbidden</c> when the user fails the role check.
    /// </remarks>
    public List<string> AuthorizedRoles { get; set; } = new();

    IEnumerable<string> IAuthorizationOptions.AuthorizedRoles => AuthorizedRoles;

    /// <inheritdoc/>
    /// <remarks>
    /// HTTP requests return <c>403 Forbidden</c> when the user fails the policy check.
    /// </remarks>
    public string? AuthorizedPolicy { get; set; }

    /// <summary>
    /// The maximum allowed file size in bytes for each file uploaded pursuant to the
    /// specification at <see href="https://github.com/jaydenseric/graphql-multipart-request-spec"/>.
    /// Null indicates no limit.
    /// </summary>
    public long? MaximumFileSize { get; set; }

    /// <summary>
    /// The maximum allowed number of files uploaded pursuant to the specification at
    /// <see href="https://github.com/jaydenseric/graphql-multipart-request-spec"/>.
    /// Null indicates no limit.
    /// </summary>
    public int? MaximumFileCount { get; set; }

    /// <summary>
    /// Returns an options class for WebSocket connections.
    /// </summary>
    public GraphQLWebSocketOptions WebSockets { get; set; } = new();
}
