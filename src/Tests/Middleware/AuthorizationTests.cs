using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using GraphQL.AspNetCore3.Errors;
using GraphQL.Execution;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting;
#if NET48 || NETCOREAPP2_1
using IHostApplicationLifetime = Microsoft.Extensions.Hosting.IApplicationLifetime;
#endif

namespace Tests.Middleware;

public class AuthorizationTests
{
    private GraphQLHttpMiddlewareOptions _options = null!;
    private bool _enableCustomErrorInfoProvider;
    private TestServer _server;

    public AuthorizationTests()
    {
        _server = CreateServer();
    }

    private TestServer CreateServer(Action<IServiceCollection>? configureServices = null)
    {
        var hostBuilder = new WebHostBuilder();
        hostBuilder.ConfigureServices(services => {
            services.AddSingleton<Chat.Services.ChatService>();
            services.AddGraphQL(b => b
                .AddAutoSchema<Chat.Schema.Query>()
                .AddErrorInfoProvider(new CustomErrorInfoProvider(this))
                .AddSystemTextJson());
            services.AddRouting();
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options => {
                    options.TokenValidationParameters.ValidateIssuerSigningKey = false;
                    options.TokenValidationParameters.ValidateLifetime = false;
                    options.TokenValidationParameters.ValidateAudience = false;
                    options.TokenValidationParameters.ValidIssuer = "test";
                    options.TokenValidationParameters.RequireSignedTokens = false;
                });
            services.AddAuthorization(config => {
                config.AddPolicy("MyPolicy", policyConfig => {
                    policyConfig.RequireAuthenticatedUser();
                });
                config.AddPolicy("FailingPolicy", policyConfig => {
                    policyConfig.RequireRole("FailingRole");
                });
            });
            configureServices?.Invoke(services);
        });
        hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseAuthentication();
#if !NETCOREAPP2_1 && !NET48
            app.UseAuthorization();
#endif
            app.UseGraphQL("/graphql", opts => {
                _options = opts;
            });
        });
        return new TestServer(hostBuilder);
    }

    private string CreateJwtToken()
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(issuer: "test", claims: new Claim[] {
            new Claim("role", "MyRole")
        });
        return tokenHandler.WriteToken(token);
    }

    private Task<HttpResponseMessage> PostQueryAsync(string json, bool authenticated)
    {
        var client = _server.CreateClient();
        var content = new StringContent(json);
        content.Headers.ContentType = new("application/graphql");
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql") { Content = content };
        if (authenticated)
            request.Headers.Authorization = new("Bearer", CreateJwtToken());
        return client.SendAsync(request);
    }

    [Fact]
    public async Task NotAuthorized()
    {
        _options.AuthorizationRequired = true;
        using var response = await PostQueryAsync("{ __typename }", false);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""errors"":[{""message"":""Access denied for schema."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
    }

    [Fact]
    public async Task NotAuthorized_Get()
    {
        _options.AuthorizationRequired = true;
        var client = _server.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/graphql?query={ __typename }");
        request.Headers.Add("GraphQL-Require-Preflight", "true");
        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""errors"":[{""message"":""Access denied for schema."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
    }

    [Fact]
    public async Task WebSocket_IgnoreAuthenticationOnConnect()
    {
        _options.AuthorizationRequired = true;
        var webSocketClient = _server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => {
            request.Headers["Sec-WebSocket-Protocol"] = "graphql-ws";
        };
        webSocketClient.SubProtocols.Add("graphql-ws");
        using var webSocket = await webSocketClient.ConnectAsync(new Uri(_server.BaseAddress, "/graphql"), default);
    }

    [Fact]
    public async Task WebSocket_NotAuthorized()
    {
        var hostBuilder = new WebHostBuilder();
        hostBuilder.ConfigureServices(services => {
            services.AddGraphQL(b => b
                .AddAutoSchema<Chat.Schema.Query>()
                .AddSystemTextJson());
        });
        hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL<MyMiddleware>("/graphql");
        });
        var server = new TestServer(hostBuilder);

        var webSocketClient = server.CreateWebSocketClient();
        webSocketClient.ConfigureRequest = request => {
            request.Headers["Sec-WebSocket-Protocol"] = "graphql-ws";
        };
        webSocketClient.SubProtocols.Add("graphql-ws");
        var error = await Should.ThrowAsync<InvalidOperationException>(() => webSocketClient.ConnectAsync(new Uri(_server.BaseAddress, "/graphql"), default));
        error.Message.ShouldBe("Incomplete handshake, status code: 422");
    }

    private class MyMiddleware : GraphQLHttpMiddleware<ISchema>
    {
        public MyMiddleware(RequestDelegate next, IGraphQLTextSerializer serializer, IDocumentExecuter<ISchema> documentExecuter, IServiceScopeFactory serviceScopeFactory, IHostApplicationLifetime hostApplicationLifetime)
            : base(next, serializer, documentExecuter, serviceScopeFactory, new(), hostApplicationLifetime)
        {
        }

        protected override async ValueTask<bool> HandleAuthorizeWebSocketConnectionAsync(HttpContext context, RequestDelegate next)
        {
            await WriteErrorResponseAsync(context, (HttpStatusCode)422 /* HttpStatusCode.UnprocessableEntity */, "Access deined");
            return true;
        }
    }

    [Fact]
    public async Task NotAuthorized_2()
    {
        _options.AuthorizationRequired = true;
        _enableCustomErrorInfoProvider = true;
        using var response = await PostQueryAsync("{ __typename }", false);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""errors"":[{""message"":""Access denied; authorization required."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
    }

    [Fact]
    public async Task Authorized()
    {
        _options.AuthorizationRequired = true;
        using var response = await PostQueryAsync("{ __typename }", true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""data"":{""__typename"":""Query""}}");
    }


    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NotAuthorized_Roles(bool authenticated)
    {
        _options.AuthorizedRoles.Add("AnotherRole");
        _options.AuthorizedRoles.Add("FailingRole");
        using var response = await PostQueryAsync("{ __typename }", authenticated);
        response.StatusCode.ShouldBe(authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""errors"":[{""message"":""Access denied for schema."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NotAuthorized_Roles_2(bool authenticated)
    {
        _options.AuthorizedRoles.Add("AnotherRole");
        _options.AuthorizedRoles.Add("FailingRole");
        _enableCustomErrorInfoProvider = true;
        using var response = await PostQueryAsync("{ __typename }", authenticated);
        response.StatusCode.ShouldBe(authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
        var actual = await response.Content.ReadAsStringAsync();
        if (authenticated) {
            actual.ShouldBe(@"{""errors"":[{""message"":""Access denied; roles required \u0027AnotherRole\u0027/\u0027FailingRole\u0027."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
        } else {
            actual.ShouldBe(@"{""errors"":[{""message"":""Access denied; authorization required."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
        }
    }

    [Fact]
    public async Task Authorized_Roles()
    {
        _options.AuthorizedRoles.Add("AnotherRole");
        _options.AuthorizedRoles.Add("MyRole");
        using var response = await PostQueryAsync("{ __typename }", true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""data"":{""__typename"":""Query""}}");
    }


    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NotAuthorized_Policy(bool authenticated)
    {
        _options.AuthorizedPolicy = "FailingPolicy";
        using var response = await PostQueryAsync("{ __typename }", authenticated);
        response.StatusCode.ShouldBe(authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""errors"":[{""message"":""Access denied for schema."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task NotAuthorized_Policy_2(bool authenticated)
    {
        _options.AuthorizedPolicy = "FailingPolicy";
        _enableCustomErrorInfoProvider = true;
        using var response = await PostQueryAsync("{ __typename }", authenticated);
        response.StatusCode.ShouldBe(authenticated ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
        var actual = await response.Content.ReadAsStringAsync();
        if (authenticated) {
            actual.ShouldBe(@"{""errors"":[{""message"":""Access denied; policy required \u0027FailingPolicy\u0027."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
        } else {
            actual.ShouldBe(@"{""errors"":[{""message"":""Access denied; authorization required."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
        }
    }

    [Fact]
    public async Task Authorized_Policy()
    {
        _options.AuthorizedPolicy = "MyPolicy";
        using var response = await PostQueryAsync("{ __typename }", true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""data"":{""__typename"":""Query""}}");
    }

    [Fact]
    public async Task NotAuthorized_WrongScheme()
    {
        _server = CreateServer(services => {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme); // change default scheme to Cookie authentication
        });
        _options.AuthorizationRequired = true;
        using var response = await PostQueryAsync("{ __typename }", true); // send an authenticated request (with JWT bearer scheme)
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""errors"":[{""message"":""Access denied for schema."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
    }

    [Fact]
    public async Task NotAuthorized_WrongScheme_2()
    {
        _server.Dispose();
        _server = CreateServer(services => {
            services.AddAuthentication().AddCookie(); // add Cookie authentication
        });
        _options.AuthorizationRequired = true;
        _options.AuthenticationSchemes.Add(CookieAuthenticationDefaults.AuthenticationScheme); // change authentication scheme for GraphQL requests to Cookie (which is not used by the test client)
        using var response = await PostQueryAsync("{ __typename }", true); // send an authenticated request (with JWT bearer scheme)
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""errors"":[{""message"":""Access denied for schema."",""extensions"":{""code"":""ACCESS_DENIED"",""codes"":[""ACCESS_DENIED""]}}]}");
    }

    [Fact]
    public async Task NotAuthorized_WrongScheme_VerifyUser()
    {
        bool validatedUser = false;
        _server = CreateServer(services => {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme); // change default scheme to Cookie authentication
            services.AddGraphQL(b => b
                .ConfigureExecutionOptions(opts => {
                    opts.User.ShouldNotBeNull().Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeFalse();
                    validatedUser = true;
                }));
        });
        _options.AuthorizationRequired = false; // disable authorization requirements; we just want to verify that an anonymous user is passed to the execution options
        using var response = await PostQueryAsync("{ __typename }", true); // send an authenticated request (with JWT bearer scheme)
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""data"":{""__typename"":""Query""}}");
        validatedUser.ShouldBeTrue();
    }

    [Fact]
    public async Task Authorized_DifferentScheme()
    {
        bool validatedUser = false;
        _server = CreateServer(services => {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme); // change default scheme to Cookie authentication
            services.AddGraphQL(b => b.ConfigureExecutionOptions(opts => {
                opts.User.ShouldNotBeNull().Identity.ShouldNotBeNull().IsAuthenticated.ShouldBeTrue();
                validatedUser = true;
            }));
        });
        _options.AuthorizationRequired = true;
        _options.AuthenticationSchemes.Add(JwtBearerDefaults.AuthenticationScheme);
        using var response = await PostQueryAsync("{ __typename }", true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var actual = await response.Content.ReadAsStringAsync();
        actual.ShouldBe(@"{""data"":{""__typename"":""Query""}}");
        validatedUser.ShouldBeTrue();
    }

    [Fact]
    public void SecurityHelperTests()
    {
        SecurityHelper.MergeUserPrincipal(null, null).ShouldNotBeNull().Identity.ShouldBeNull(); // Note that ASP.NET Core does not return null for anonymous user
        var principal1 = new ClaimsPrincipal(new ClaimsIdentity()); // empty identity for primary identity (default for ASP.NET Core)
        SecurityHelper.MergeUserPrincipal(null, principal1).ShouldBe(principal1);
        var principal2 = new ClaimsPrincipal(new ClaimsIdentity("test1")); // non-empty identity for secondary identity
        SecurityHelper.MergeUserPrincipal(principal1, principal2).Identities.ShouldHaveSingleItem().AuthenticationType.ShouldBe("test1");
        var principal3 = new ClaimsPrincipal(new ClaimsIdentity("test2")); // merge two non-empty identities together
        SecurityHelper.MergeUserPrincipal(principal2, principal3).Identities.Select(x => x.AuthenticationType).ShouldBe(new[] { "test2", "test1" }); // last one wins
    }

    private class CustomErrorInfoProvider : ErrorInfoProvider
    {
        private readonly AuthorizationTests _authorizationTests;

        public CustomErrorInfoProvider(AuthorizationTests authorizationTests)
            => _authorizationTests = authorizationTests;

        public override ErrorInfo GetInfo(ExecutionError executionError)
        {
            var info = base.GetInfo(executionError);
            if (!_authorizationTests._enableCustomErrorInfoProvider)
                return info;
            if (executionError is AccessDeniedError accessDeniedError) {
                if (accessDeniedError.RolesRequired != null) {
                    info.Message = $"Access denied; roles required {string.Join("/", accessDeniedError.RolesRequired.Select(x => $"'{x}'"))}.";
                } else if (accessDeniedError.PolicyRequired != null) {
                    info.Message = $"Access denied; policy required '{accessDeniedError.PolicyRequired}'.";
                    accessDeniedError.PolicyAuthorizationResult.ShouldNotBeNull();
                    accessDeniedError.PolicyAuthorizationResult.Succeeded.ShouldBeFalse();
                } else {
                    info.Message = $"Access denied; authorization required.";
                }
            }
            return info;
        }
    }
}
