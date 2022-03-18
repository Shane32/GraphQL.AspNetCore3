namespace Shane32.GraphQL.AspNetCore;

/// <summary>
/// ASP.NET Core middleware for processing GraphQL requests. Can processes both single and batch requests.
/// See <see href="https://www.apollographql.com/blog/query-batching-in-apollo-63acfd859862/">Transport-level batching</see>
/// for more information. This middleware useful with and without ASP.NET Core routing.
/// <br/><br/>
/// GraphQL over HTTP <see href="https://github.com/APIs-guru/graphql-over-http">spec</see> says:
/// GET requests can be used for executing ONLY queries. If the values of query and operationName indicates that
/// a non-query operation is to be executed, the server should immediately respond with an error status code, and
/// halt execution.
/// <br/><br/>
/// Attention! The current implementation does not impose such a restriction and allows mutations in GET requests.
/// </summary>
/// <typeparam name="TSchema">Type of GraphQL schema that is used to validate and process requests.</typeparam>
public class GraphQLHttpMiddleware<TSchema> : IMiddleware
    where TSchema : ISchema
{
    private readonly IGraphQLTextSerializer _serializer;
    private readonly IDocumentExecuter _documentExecuter;
    private readonly IEnumerable<IValidationRule> _validationRules;
    private readonly IEnumerable<IValidationRule> _cachedDocumentValidationRules;
    private readonly IEnumerable<IWebSocketHandler<TSchema>> _webSocketHandlers;

    /// <summary>
    /// Gets the options configured for this instance.
    /// </summary>
    protected GraphQLHttpMiddlewareOptions Options { get; }

    /// <summary>
    /// Initializes a new instance.
    /// </summary>
    public GraphQLHttpMiddleware(
        IGraphQLTextSerializer serializer,
        IDocumentExecuter<TSchema> documentExecuter,
        IHttpContextAccessor httpContextAccessor,
        IEnumerable<IWebSocketHandler<TSchema>> webSocketHandlers,
        GraphQLHttpMiddlewareOptions options)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _documentExecuter = documentExecuter ?? throw new ArgumentNullException(nameof(documentExecuter));
        var rule = new OperationTypeValidationRule(httpContextAccessor);
        _validationRules = DocumentValidator.CoreRules.Append(rule).ToArray();
        _cachedDocumentValidationRules = new[] { rule };
        _webSocketHandlers = webSocketHandlers;
        Options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc/>
    public virtual async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.WebSockets.IsWebSocketRequest) {
            if (Options.HandleWebSockets)
                await InvokeWebSocketAsync(context, next);
            else
                await HandleInvalidHttpMethodErrorAsync(context, next);
            return;
        }

        // Handle requests as per recommendation at http://graphql.org/learn/serving-over-http/
        // Inspiration: https://github.com/graphql/express-graphql/blob/master/src/index.js
        var httpRequest = context.Request;
        var httpResponse = context.Response;

        // GraphQL HTTP only supports GET and POST methods
        bool isGet = HttpMethods.IsGet(httpRequest.Method);
        bool isPost = HttpMethods.IsPost(httpRequest.Method);
        if (isGet && !Options.HandleGet || isPost && !Options.HandlePost || !isGet && !isPost) {
            await HandleInvalidHttpMethodErrorAsync(context, next);
            return;
        }

        // Parse POST body
        GraphQLRequest? bodyGQLRequest = null;
        IList<GraphQLRequest>? bodyGQLBatchRequest = null;
        if (isPost) {
            if (!MediaTypeHeaderValue.TryParse(httpRequest.ContentType, out var mediaTypeHeader)) {
                await HandleContentTypeCouldNotBeParsedErrorAsync(context);
                return;
            }

            switch (mediaTypeHeader.MediaType) {
                case "application/json":
                    IList<GraphQLRequest>? deserializationResult;
                    try {
#if NET5_0_OR_GREATER
                        if (!TryGetEncoding(mediaTypeHeader.CharSet, out var sourceEncoding)) {
                            await HandleContentTypeCouldNotBeParsedErrorAsync(context);
                            return;
                        }
                        // Wrap content stream into a transcoding stream that buffers the data transcoded from the sourceEncoding to utf-8.
                        if (sourceEncoding != null && sourceEncoding != System.Text.Encoding.UTF8) {
                            using var tempStream = System.Text.Encoding.CreateTranscodingStream(httpRequest.Body, innerStreamEncoding: sourceEncoding, outerStreamEncoding: System.Text.Encoding.UTF8, leaveOpen: true);
                            deserializationResult = await _serializer.ReadAsync<IList<GraphQLRequest>>(tempStream, context.RequestAborted);
                        } else {
                            deserializationResult = await _serializer.ReadAsync<IList<GraphQLRequest>>(httpRequest.Body, context.RequestAborted);
                        }
#else
                        deserializationResult = await _serializer.ReadAsync<IList<GraphQLRequest>>(httpRequest.Body, context.RequestAborted);
#endif
                    } catch (Exception ex) {
                        if (!await HandleDeserializationErrorAsync(context, ex))
                            throw;
                        return;
                    }
                    // https://github.com/graphql-dotnet/server/issues/751
                    if (deserializationResult is GraphQLRequest[] array && array.Length == 1)
                        bodyGQLRequest = deserializationResult[0];
                    else
                        bodyGQLBatchRequest = deserializationResult;
                    break;

                case "application/graphql":
                    bodyGQLRequest = await DeserializeFromGraphBodyAsync(httpRequest.Body);
                    break;

                default:
                    if (httpRequest.HasFormContentType) {
                        var formCollection = await httpRequest.ReadFormAsync(context.RequestAborted);
                        try {
                            bodyGQLRequest = DeserializeFromFormBody(formCollection);
                        } catch (Exception ex) {
                            if (!await HandleDeserializationErrorAsync(context, ex))
                                throw;
                        }
                        break;
                    }
                    await HandleInvalidContentTypeErrorAsync(context);
                    return;
            }
        }

        // If we don't have a batch request, parse the query from URL too to determine the actual request to run.
        // Query string params take priority.
        GraphQLRequest? gqlRequest = null;
        var urlGQLRequest = isGet || Options.ReadQueryStringOnPost ? DeserializeFromQueryString(httpRequest.Query) : null;

        gqlRequest = new GraphQLRequest {
            Query = urlGQLRequest?.Query ?? bodyGQLRequest?.Query!,
            Variables = urlGQLRequest?.Variables ?? bodyGQLRequest?.Variables,
            Extensions = urlGQLRequest?.Extensions ?? bodyGQLRequest?.Extensions,
            OperationName = urlGQLRequest?.OperationName ?? bodyGQLRequest?.OperationName
        };

        if (string.IsNullOrWhiteSpace(gqlRequest.Query)) {
            await HandleNoQueryErrorAsync(context);
            return;
        }

        // Prepare context and execute
        await HandleRequestAsync(context, next, gqlRequest);
    }

    /// <summary>
    /// Executes the GraphQL request
    /// </summary>
    protected virtual async Task HandleRequestAsync(
        HttpContext context,
        RequestDelegate next,
        GraphQLRequest gqlRequest)
    {
        // Normal execution with single graphql request
        var result = await ExecuteRequestAsync(context, gqlRequest);
        await WriteResponseAsync(context.Response, result, context.RequestAborted);
    }

    /// <summary>
    /// Executes a GraphQL request.
    /// </summary>
    protected virtual async Task<ExecutionResult> ExecuteRequestAsync(HttpContext context, GraphQLRequest request)
        => await _documentExecuter.ExecuteAsync(new ExecutionOptions {
            Query = request.Query,
            Variables = request.Variables,
            Extensions = request.Extensions,
            CancellationToken = context.RequestAborted,
            OperationName = request.OperationName,
            RequestServices = context.RequestServices,
            UserContext = await BuildUserContextAsync(context),
            ValidationRules = _validationRules,
            CachedDocumentValidationRules = _cachedDocumentValidationRules,
        });

    /// <summary>
    /// Builds the user context.
    /// </summary>
    protected virtual async Task<IDictionary<string, object?>> BuildUserContextAsync(HttpContext context)
    {
        var userContextBuilder = context.RequestServices.GetService<IUserContextBuilder>();
        var userContext = userContextBuilder == null
            ? new Dictionary<string, object?>()
            : await userContextBuilder.BuildUserContextAsync(context);
        return userContext;
    }

    /// <summary>
    /// Writes the specified object (usually a GraphQL response) as JSON to the HTTP response stream.
    /// </summary>
    protected virtual Task WriteResponseAsync<TResult>(HttpResponse httpResponse, TResult result, CancellationToken cancellationToken)
    {
        httpResponse.ContentType = "application/json";
        httpResponse.StatusCode = 200; // OK

        return _serializer.WriteAsync(httpResponse.Body, result, cancellationToken);
    }

    /// <summary>
    /// Handles a WebSocket request.
    /// </summary>
    protected virtual async Task InvokeWebSocketAsync(HttpContext context, RequestDelegate next)
    {
        if (_webSocketHandlers == null || !_webSocketHandlers.Any()) {
            await next(context);
            return;
        }

        var cancellationToken = context.RequestAborted;

        string selectedProtocol;
        IWebSocketHandler selectedHandler;
        // select a sub-protocol, preferring the first sub-protocol requested by the client
        foreach (var protocol in context.WebSockets.WebSocketRequestedProtocols) {
            foreach (var handler in _webSocketHandlers) {
                if (handler.SupportedSubProtocols.Contains(protocol)) {
                    selectedProtocol = protocol;
                    selectedHandler = handler;
                    goto MatchedHandler;
                }
            }
        }

        await HandleWebSocketSubProtocolNotSupportedAsync(context);
        return;

    MatchedHandler:
        var socket = await context.WebSockets.AcceptWebSocketAsync(selectedProtocol);

        if (socket.SubProtocol != selectedProtocol) {
            await socket.CloseAsync(
                WebSocketCloseStatus.ProtocolError,
                $"Invalid sub-protocol; expected '{selectedProtocol}'",
                cancellationToken);
            return;
        }

        // Prepare context and execute
        var userContext = await BuildUserContextAsync(context);
        // Connect, then wait until the websocket has disconnected (and all subscriptions ended)
        await selectedHandler.ExecuteAsync(context, socket, selectedProtocol, userContext, cancellationToken);
    }

    /// <summary>
    /// Writes a '404 JSON body text could not be parsed.' message to the output
    /// </summary>
    protected virtual async ValueTask<bool> HandleDeserializationErrorAsync(HttpContext context, Exception ex)
    {
        await WriteErrorResponseAsync(context, $"JSON body text could not be parsed. {ex.Message}", HttpStatusCode.BadRequest);
        return true;
    }

    /// <summary>
    /// Writes a '404 Invalid WebSocket sub-protocol.' message to the output
    /// </summary>
    protected virtual Task HandleWebSocketSubProtocolNotSupportedAsync(HttpContext context)
        => WriteErrorResponseAsync(context, $"Invalid WebSocket sub-protocol(s): {string.Join(",", context.WebSockets.WebSocketRequestedProtocols.Select(x => $"'{x}'"))}", HttpStatusCode.BadRequest);

    /// <summary>
    /// Writes a '404 GraphQL query is missing.' message to the output
    /// </summary>
    protected virtual Task HandleNoQueryErrorAsync(HttpContext context)
        => WriteErrorResponseAsync(context, "GraphQL query is missing.", HttpStatusCode.BadRequest);

    /// <summary>
    /// Writes a '415 Invalid Content-Type header: could not be parsed.' message to the output
    /// </summary>
    protected virtual Task HandleContentTypeCouldNotBeParsedErrorAsync(HttpContext context)
        => WriteErrorResponseAsync(context, $"Invalid 'Content-Type' header: value '{context.Request.ContentType}' could not be parsed.", HttpStatusCode.UnsupportedMediaType);

    /// <summary>
    /// Writes a '415 Invalid Content-Type header: non-supported type.' message to the output
    /// </summary>
    protected virtual Task HandleInvalidContentTypeErrorAsync(HttpContext context)
        => WriteErrorResponseAsync(context, $"Invalid 'Content-Type' header: non-supported media type '{context.Request.ContentType}'. Must be 'application/json', 'application/graphql' or a form body.", HttpStatusCode.UnsupportedMediaType);

    /// <summary>
    /// Indicates that an unsupported HTTP method was requested.
    /// Executes the next delegate in the chain by default.
    /// </summary>
    protected virtual Task HandleInvalidHttpMethodErrorAsync(HttpContext context, RequestDelegate next)
    {
        //context.Response.Headers["Allow"] = Options.HandleGet && Options.HandlePost ? "GET, POST" : Options.HandleGet ? "GET" : Options.HandlePost ? "POST" : "";
        //return WriteErrorResponseAsync(context, $"Invalid HTTP method.{(Options.HandleGet || Options.HandlePost ? $" Only {(Options.HandleGet && Options.HandlePost ? "GET and POST are" : Options.HandleGet ? "GET is" : "POST is")} supported." : "")}", HttpStatusCode.MethodNotAllowed);
        return next(context);
    }

    /// <summary>
    /// Writes the specified error message as a JSON-formatted GraphQL response, with the specified HTTP status code.
    /// </summary>
    protected virtual Task WriteErrorResponseAsync(HttpContext context, string errorMessage, HttpStatusCode httpStatusCode)
    {
        var result = new ExecutionResult {
            Errors = new ExecutionErrors
            {
                new ExecutionError(errorMessage)
            }
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)httpStatusCode;

        return _serializer.WriteAsync(context.Response.Body, result, context.RequestAborted);
    }

    private const string QUERY_KEY = "query";
    private const string VARIABLES_KEY = "variables";
    private const string EXTENSIONS_KEY = "extensions";
    private const string OPERATION_NAME_KEY = "operationName";

    private GraphQLRequest DeserializeFromQueryString(IQueryCollection queryCollection) => new GraphQLRequest {
        Query = queryCollection.TryGetValue(QUERY_KEY, out var queryValues) ? queryValues[0] : null!,
        Variables = Options.ReadVariablesFromQueryString && queryCollection.TryGetValue(VARIABLES_KEY, out var variablesValues) ? _serializer.Deserialize<Inputs>(variablesValues[0]) : null,
        Extensions = Options.ReadExtensionsFromQueryString && queryCollection.TryGetValue(EXTENSIONS_KEY, out var extensionsValues) ? _serializer.Deserialize<Inputs>(extensionsValues[0]) : null,
        OperationName = queryCollection.TryGetValue(OPERATION_NAME_KEY, out var operationNameValues) ? operationNameValues[0] : null,
    };

    private GraphQLRequest DeserializeFromFormBody(IFormCollection formCollection) => new GraphQLRequest {
        Query = formCollection.TryGetValue(QUERY_KEY, out var queryValues) ? queryValues[0] : null!,
        Variables = formCollection.TryGetValue(VARIABLES_KEY, out var variablesValues) ? _serializer.Deserialize<Inputs>(variablesValues[0]) : null,
        Extensions = formCollection.TryGetValue(EXTENSIONS_KEY, out var extensionsValues) ? _serializer.Deserialize<Inputs>(extensionsValues[0]) : null,
        OperationName = formCollection.TryGetValue(OPERATION_NAME_KEY, out var operationNameValues) ? operationNameValues[0] : null,
    };

    private async Task<GraphQLRequest> DeserializeFromGraphBodyAsync(Stream bodyStream)
    {
        // In this case, the query is the raw value in the POST body

        // Do not explicitly or implicitly (via using, etc.) call dispose because StreamReader will dispose inner stream.
        // This leads to the inability to use the stream further by other consumers/middlewares of the request processing
        // pipeline. In fact, it is absolutely not dangerous not to dispose StreamReader as it does not perform any useful
        // work except for the disposing inner stream.
        string query = await new StreamReader(bodyStream).ReadToEndAsync();

        return new GraphQLRequest { Query = query }; // application/graphql MediaType supports only query text
    }

#if NET5_0_OR_GREATER
    private static bool TryGetEncoding(string? charset, out System.Text.Encoding encoding)
    {
        encoding = null!;

        if (string.IsNullOrEmpty(charset))
            return true;

        try {
            // Remove at most a single set of quotes.
            if (charset.Length > 2 && charset[0] == '\"' && charset[^1] == '\"') {
                encoding = System.Text.Encoding.GetEncoding(charset[1..^1]);
            } else {
                encoding = System.Text.Encoding.GetEncoding(charset);
            }
        } catch (ArgumentException) {
            return false;
        }

        return true;
    }
#endif
}
