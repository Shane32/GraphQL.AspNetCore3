namespace Shane32.GraphQL.AspNetCore
{
    /// <summary>
    /// Configuration options for <see cref="GraphQLHttpMiddleware{TSchema}"/>.
    /// </summary>
    public class GraphQLHttpMiddlewareOptions
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
        /// Requires registering one or more <see cref="IWebSocketHandler"/>
        /// interfaces with the dependency injection framework, typically by
        /// calling <see cref="GraphQLBuilderExtensions.AddWebSocketHandler{TWebSocketHandler}(IGraphQLBuilder)"/>
        /// during the dependency injection framework configuration.
        /// <br/><br/>
        /// If no <see cref="IWebSocketHandler"/> instances are registered,
        /// this property will effectively be <see langword="false"/>.
        /// </summary>
        public bool HandleWebSockets { get; set; } = true;

        /// <summary>
        /// Enables parsing the query string on POST requests.
        /// If enabled, the query string properties override those in the body of the request.
        /// </summary>
        public bool ReadQueryStringOnPost { get; set; } = false;

        /// <summary>
        /// Enables reading variables from the query string.
        /// Variables are interpreted as JSON and deserialized before being
        /// provided to the <see cref="IDocumentExecuter"/>.
        /// </summary>
        public bool ReadVariablesFromQueryString { get; set; } = false;

        /// <summary>
        /// Enables reading extensions from the query string.
        /// Extensions are interpreted as JSON and deserialized before being
        /// provided to the <see cref="IDocumentExecuter"/>.
        /// </summary>
        public bool ReadExtensionsFromQueryString { get; set; } = false;
    }
}
