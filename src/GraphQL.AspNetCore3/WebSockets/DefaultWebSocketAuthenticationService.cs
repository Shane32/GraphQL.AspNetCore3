using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace GraphQL.AspNetCore3.WebSockets;

/// <summary>
/// A default implementation of <see cref="IWebSocketAuthenticationService"/> which examines the payload
/// for an "Authorization" key, and passes it to <see cref="IAuthenticationService"/> for authentication
/// within an "Authorization" header value.
/// </summary>
public class DefaultWebSocketAuthenticationService : IWebSocketAuthenticationService
{
    private readonly IGraphQLSerializer _serializer;
    private readonly AuthenticationMiddleware _middleware;

    /// <summary>
    /// Initializes a new instance with the specified properties.
    /// </summary>
    public DefaultWebSocketAuthenticationService(IAuthenticationSchemeProvider schemes, IGraphQLSerializer serializer)
    {
        _serializer = serializer;
        _middleware = new(_ => Task.CompletedTask, schemes);
    }

    /// <inheritdoc/>
    public virtual async Task AuthenticateAsync(IWebSocketConnection connection, string subProtocol, OperationMessage operationMessage)
    {
        if (connection.HttpContext.User.Identity?.IsAuthenticated ?? false)
            return;
        var inputs = _serializer.ReadNode<Inputs>(operationMessage.Payload);
        if (inputs != null && inputs.TryGetValue("Authorization", out var authValue) && authValue is string authValueString) {
            var dic = new Dictionary<string, StringValues>(connection.HttpContext.Request.Headers);
            var headers = new HeaderDictionary(dic) {
                ["Authorization"] = authValueString
            };
            var fakeContext = new FakeHttpContext(connection.HttpContext, headers);
            await _middleware.Invoke(fakeContext);
        }
    }

    private class FakeHttpContext : HttpContext
    {
        private readonly HttpContext _baseContext;
        private readonly HttpRequest _request;

        public FakeHttpContext(HttpContext baseContext, IHeaderDictionary headerDictionary)
        {
            _baseContext = baseContext;
            _request = new FakeRequest(this, baseContext.Request, headerDictionary);
        }

        public override ConnectionInfo Connection => _baseContext.Connection;

        public override IFeatureCollection Features => _baseContext.Features;

        public override IDictionary<object, object?> Items { get => _baseContext.Items; set => throw new NotImplementedException(); }

        public override HttpRequest Request => _request;

        public override CancellationToken RequestAborted { get => _baseContext.RequestAborted; set => throw new NotImplementedException(); }
        public override IServiceProvider RequestServices { get => _baseContext.RequestServices; set => throw new NotImplementedException(); }

        public override HttpResponse Response => throw new NotImplementedException();

        public override ISession Session { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string TraceIdentifier { get => _baseContext.TraceIdentifier; set => throw new NotImplementedException(); }
        public override ClaimsPrincipal User { get => _baseContext.User; set => _baseContext.User = value; }

        public override WebSocketManager WebSockets => throw new NotImplementedException();

        public override void Abort() => throw new NotImplementedException();
    }

    private class FakeRequest : HttpRequest
    {
        private readonly HttpContext _baseContext;
        private readonly HttpRequest _baseRequest;
        private readonly IHeaderDictionary _headerDictionary;

        public FakeRequest(HttpContext baseContext, HttpRequest baseRequest, IHeaderDictionary headerDictionary)
        {
            _baseContext = baseContext;
            _baseRequest = baseRequest;
            _headerDictionary = headerDictionary;
        }

        public override Stream Body { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override long? ContentLength { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string ContentType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override IRequestCookieCollection Cookies { get => _baseRequest.Cookies; set => throw new NotImplementedException(); }
        public override IFormCollection Form { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override bool HasFormContentType => throw new NotImplementedException();

        public override IHeaderDictionary Headers => _headerDictionary;

        public override HostString Host { get => _baseRequest.Host; set => throw new NotImplementedException(); }

        public override HttpContext HttpContext => _baseContext;

        public override bool IsHttps { get => _baseRequest.IsHttps; set => throw new NotImplementedException(); }
        public override string Method { get => _baseRequest.Method; set => throw new NotImplementedException(); }
        public override PathString Path { get => _baseRequest.Path; set => throw new NotImplementedException(); }
        public override PathString PathBase { get => _baseRequest.PathBase; set => throw new NotImplementedException(); }
        public override string Protocol { get => _baseRequest.Protocol; set => throw new NotImplementedException(); }
        public override IQueryCollection Query { get => _baseRequest.Query; set => throw new NotImplementedException(); }
        public override QueryString QueryString { get => _baseRequest.QueryString; set => throw new NotImplementedException(); }
        public override string Scheme { get => _baseRequest.Scheme; set => throw new NotImplementedException(); }

        public override Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
