using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RichardSzalay.MockHttp;

namespace Tests.JwtBearer;

public class JwtWebSocketAuthenticationServiceTests
{
    private string _issuer = "https://demo.identityserver.io";
    private string _audience = "testAudience";
    private readonly string _subject = "user123";
    private RSAParameters _rsaParameters;
    private string? _jwtAccessToken;
    private readonly MockHttpMessageHandler _oidcHttpMessageHandler = new();
    private readonly ISchema _schema;

    public JwtWebSocketAuthenticationServiceTests()
    {
        var query = new ObjectGraphType() { Name = "Query" };
        query.Field<StringGraphType>("test").Resolve(ctx => ctx.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        _schema = new Schema { Query = query };
    }

    [Fact]
    public async Task SuccessfulAuthentication()
    {
        CreateSignedToken();
        SetupOidcDiscovery();
        using var testServer = CreateTestServer();
        await TestGetAsync(testServer, isAuthenticated: true);
        await TestWebSocketAsync(testServer, isAuthenticated: true);
    }

    [Fact]
    public async Task WrongKeys()
    {
        CreateSignedToken();
        SetupOidcDiscovery();
        using var testServer = CreateTestServer();
        CreateSignedToken(); // create new token with different keys
        await TestGetAsync(testServer, isAuthenticated: false);
        await TestWebSocketAsync(testServer, isAuthenticated: false);
    }

    [Fact]
    public async Task WrongIssuer()
    {
        CreateSignedToken();
        _issuer = "https://wrong.issuer";
        SetupOidcDiscovery();
        using var testServer = CreateTestServer();
        await TestGetAsync(testServer, isAuthenticated: false);
        await TestWebSocketAsync(testServer, isAuthenticated: false);
    }

    [Fact]
    public async Task WrongAudience()
    {
        CreateSignedToken();
        _audience = "wrongAudience";
        SetupOidcDiscovery();
        using var testServer = CreateTestServer();
        await TestGetAsync(testServer, isAuthenticated: false);
        await TestWebSocketAsync(testServer, isAuthenticated: false);
    }

    [Fact]
    public async Task Expired()
    {
        CreateSignedToken(expired: true);
        SetupOidcDiscovery();
        using var testServer = CreateTestServer();
        await TestGetAsync(testServer, isAuthenticated: false);
        await TestWebSocketAsync(testServer, isAuthenticated: false);
    }

    [Fact]
    public async Task NoDefaultScheme()
    {
        CreateSignedToken();
        SetupOidcDiscovery();
        using var testServer = CreateTestServer(defaultScheme: false);
        await TestGetAsync(testServer, isAuthenticated: false);
        await TestWebSocketAsync(testServer, isAuthenticated: false);
    }

    [Fact]
    public async Task NoDefaultSchemeSpecified()
    {
        CreateSignedToken();
        SetupOidcDiscovery();
        using var testServer = CreateTestServer(defaultScheme: false, specifyScheme: true);
        await TestGetAsync(testServer, isAuthenticated: true);
        await TestWebSocketAsync(testServer, isAuthenticated: true);
    }

    [Fact]
    public async Task CustomScheme()
    {
        CreateSignedToken();
        SetupOidcDiscovery();
        using var testServer = CreateTestServer(customScheme: true);
        await TestGetAsync(testServer, isAuthenticated: true);
        await TestWebSocketAsync(testServer, isAuthenticated: true);
    }

    [Fact]
    public async Task CustomNoDefaultScheme()
    {
        CreateSignedToken();
        SetupOidcDiscovery();
        using var testServer = CreateTestServer(customScheme: true, defaultScheme: false);
        await TestGetAsync(testServer, isAuthenticated: false);
        await TestWebSocketAsync(testServer, isAuthenticated: false);
    }

    [Fact]
    public async Task CustomNoDefaultSchemeSpecified()
    {
        CreateSignedToken();
        SetupOidcDiscovery();
        using var testServer = CreateTestServer(customScheme: true, defaultScheme: false, specifyScheme: true);
        await TestGetAsync(testServer, isAuthenticated: true);
        await TestWebSocketAsync(testServer, isAuthenticated: true);
    }

    [Fact]
    public async Task WrongScheme()
    {
        CreateSignedToken();
        SetupOidcDiscovery();
        using var testServer = CreateTestServer(specifyInvalidScheme: true);
        await TestGetAsync(testServer, isAuthenticated: false);
        await TestWebSocketAsync(testServer, isAuthenticated: false);
    }

    [Fact]
    public async Task MultipleSchemes()
    {
        CreateSignedToken();
        SetupOidcDiscovery();
        using var testServer = CreateTestServer(specifyInvalidScheme: true, specifyScheme: true, defaultScheme: false);
        await TestGetAsync(testServer, isAuthenticated: true);
        await TestWebSocketAsync(testServer, isAuthenticated: true);
    }

    [Fact]
    public async Task NoToken()
    {
        CreateSignedToken();
        SetupOidcDiscovery();
        using var testServer = CreateTestServer();
        _jwtAccessToken = null;
        await TestGetAsync(testServer, isAuthenticated: false);
        await TestWebSocketAsync(testServer, isAuthenticated: false);
    }

    private async Task TestGetAsync(TestServer testServer, bool isAuthenticated)
    {
        // test an authenticated request
        using var client = testServer.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/graphql?query={test}");
        if (_jwtAccessToken != null)
            request.Headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, _jwtAccessToken);
        using var response = await client.SendAsync(request);
        if (isAuthenticated) {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.ShouldBe($$$"""
            {"data":{"test":"{{{_subject}}}"}}
            """);
        } else {
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    private async Task TestWebSocketAsync(TestServer testServer, bool isAuthenticated)
    {
        // test an authenticated request
        var webSocketClient = testServer.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => {
            request.Headers["Sec-WebSocket-Protocol"] = "graphql-ws";
        };
        webSocketClient.SubProtocols.Add("graphql-ws");
        using var webSocket = await webSocketClient.ConnectAsync(new Uri(testServer.BaseAddress, "/graphql"), default);

        // send CONNECTION_INIT
        await webSocket.SendMessageAsync(new OperationMessage {
            Type = "connection_init",
            Payload = _jwtAccessToken != null ? new {
                Authorization = "Bearer " + _jwtAccessToken,
            } : null,
        });

        if (!isAuthenticated) {
            // wait for CONNECTION_ERROR
            var message1 = await webSocket.ReceiveMessageAsync();
            message1.Type.ShouldBe("connection_error");
            message1.Payload.ShouldBeOfType<string>().ShouldBe("\"Access denied\""); // for the purposes of testing, this contains the raw JSON received for this JSON element.

            // wait for websocket closure
            (await webSocket.ReceiveCloseAsync()).ShouldBe((WebSocketCloseStatus)4401);
            return;
        }

        // wait for CONNECTION_ACK
        var message = await webSocket.ReceiveMessageAsync();
        message.Type.ShouldBe("connection_ack");

        // send start
        await webSocket.SendMessageAsync(new OperationMessage {
            Id = "1",
            Type = "start",
            Payload = new GraphQLRequest {
                Query = "{test}",
            },
        });

        // wait for data
        message = await webSocket.ReceiveMessageAsync();
        message.Type.ShouldBe("data");
        message.Id.ShouldBe("1");
        message.Payload.ShouldBe($$$"""
        {"data":{"test":"{{{_subject}}}"}}
        """);
    }

    /// <summary>
    /// Creates a test server with JWT bearer authentication.
    /// Uses the currently configured <see cref="_issuer"/> and <see cref="_audience"/>.
    /// </summary>
    private TestServer CreateTestServer(bool defaultScheme = true, bool customScheme = false, bool specifyScheme = false, bool specifyInvalidScheme = false)
    {
        return new TestServer(new WebHostBuilder()
            .ConfigureServices(services => {
                var authBuilder = services.AddAuthentication(defaultScheme ? customScheme ? "Custom" : JwtBearerDefaults.AuthenticationScheme : "");
                if (specifyInvalidScheme) {
                    authBuilder.AddCookie();
                }
                authBuilder.AddJwtBearer(customScheme ? "Custom" : JwtBearerDefaults.AuthenticationScheme, o => {
                    o.Authority = _issuer;
                    o.Audience = _audience;
                    o.BackchannelHttpHandler = _oidcHttpMessageHandler;
                });
                services.AddGraphQL(b => b
                    .AddSchema(_schema)
                    .AddSystemTextJson()
                    .AddJwtBearerAuthentication()
                );
#if NET48
                services.AddHostApplicationLifetime();
#endif
            })
            .Configure(app => {
                app.UseWebSockets();
                app.UseAuthentication();
                app.UseGraphQL(configureMiddleware: o => {
                    o.AuthorizationRequired = true;
                    o.CsrfProtectionEnabled = false;
                    if (specifyInvalidScheme) {
                        o.AuthenticationSchemes.Add(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                    if (specifyScheme) {
                        o.AuthenticationSchemes.Add(customScheme ? "Custom" : JwtBearerDefaults.AuthenticationScheme);
                    }
                });
            }));
    }

    /// <summary>
    /// Configures the mock HTTP message handler to respond to OIDC discovery requests.
    /// Uses the currently configured <see cref="_issuer"/> and <see cref="_rsaParameters"/>.
    /// </summary>
    private void SetupOidcDiscovery()
    {
        // Comprehensive OIDC discovery document
        var discoveryDocument = new {
            issuer = _issuer,
            authorization_endpoint = $"{_issuer}/connect/authorize",
            token_endpoint = $"{_issuer}/connect/token",
            userinfo_endpoint = $"{_issuer}/connect/userinfo",
            jwks_uri = $"{_issuer}/.well-known/jwks.json",
            response_types_supported = new[] { "code", "token", "id_token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" }
        };

        // mock the discovery endpoint
        _oidcHttpMessageHandler
            .When($"{_issuer}/.well-known/openid-configuration")
            .Respond("application/json", JsonSerializer.Serialize(discoveryDocument));

        // Create JWKS based on the RSA public key
        var jwk = new {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    n = Base64UrlEncoder.Encode(_rsaParameters.Modulus),
                    e = Base64UrlEncoder.Encode(_rsaParameters.Exponent)
                }
            }
        };

        // mock the JWKS endpoint
        _oidcHttpMessageHandler
            .When($"{_issuer}/.well-known/jwks.json")
            .Respond("application/json", JsonSerializer.Serialize(jwk));

        // throw for all other requests
        _oidcHttpMessageHandler
            .When("*")
            .Respond(request => {
                throw new NotImplementedException($"No handler configured for {request.RequestUri}");
            });
    }

    /// <summary>
    /// Creates a new RSA key pair and a signed JWT token.
    /// Uses the currently configured <see cref="_issuer"/>, <see cref="_audience"/>, and <see cref="_subject"/>.
    /// Overwrites the <see cref="_rsaParameters"/> and <see cref="_jwtAccessToken"/> fields.
    /// </summary>
    private void CreateSignedToken(bool expired = false)
    {
        using var rsa = RSA.Create(2048);
        var rsaParameters = rsa.ExportParameters(true);
        var key = new RsaSecurityKey(rsaParameters);
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var now = DateTime.UtcNow;
        if (expired) {
            now = now.AddMinutes(-10);
        }
        var tokenDescriptor = new SecurityTokenDescriptor {
            Issuer = _issuer,
            Audience = _audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, _subject)]), // Subject (user ID)
            Expires = now.Add(TimeSpan.FromMinutes(5)),
            IssuedAt = now,
            NotBefore = now,
            SigningCredentials = signingCredentials
        };
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenStr = tokenHandler.WriteToken(token);
        _rsaParameters = rsaParameters;
        _jwtAccessToken = tokenStr;
    }
}

