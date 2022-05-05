using System.Security.Cryptography;
using GraphQL;
using GraphQL.AspNetCore3;
using GraphQL.AspNetCore3.WebSockets;
using GraphQL.MicrosoftDI;
using GraphQL.SystemTextJson;
using IdentityServer4.Models;
using Microsoft.IdentityModel.Tokens;

// configure Identity Server
var apiScopes = new[] { new ApiScope("api1", "My API") };
var clients = new[] {
    new Client {
        ClientId = "client",
        // no interactive user, use the clientid/secret for authentication
        AllowedGrantTypes = GrantTypes.ClientCredentials,
        // secret for authentication
        ClientSecrets = { new Secret("secret".Sha256()) },
        // scopes that client has access to
        AllowedScopes = { "api1" },
        // lifetime of generated token
        AccessTokenLifetime = 300,
        RefreshTokenUsage = TokenUsage.ReUse,
        RefreshTokenExpiration = TokenExpiration.Sliding,
    },
};

// create key pair
ECDsaSecurityKey privateKey;
ECDsaSecurityKey publicKey;
{
    var keyPair = ECDsa.Create();
    privateKey = new ECDsaSecurityKey(keyPair);
    var publicKeyBytes = keyPair.ExportSubjectPublicKeyInfo();

    var tempKeyPair = ECDsa.Create();
    tempKeyPair.ImportSubjectPublicKeyInfo(publicKeyBytes, out int _);
    publicKey = new ECDsaSecurityKey(tempKeyPair);
}

// create web app
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityServer()
    .AddInMemoryApiScopes(apiScopes)
    .AddInMemoryClients(clients)
    .AddSigningCredential(privateKey, IdentityServer4.IdentityServerConstants.ECDsaSigningAlgorithm.ES256);

builder.Services.AddSingleton<Chat.Services.ChatService>();
builder.Services.AddGraphQL(b => b
    .AddAutoSchema<Chat.Schema.Query>(s => s
        .WithMutation<Chat.Schema.Mutation>()
        .WithSubscription<Chat.Schema.Subscription>())
    .AddSystemTextJson()
    .AddWebSocketAuthentication<DefaultWebSocketAuthenticationService>());

// accepts any access token issued by identity server
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha256 },
            IssuerSigningKey = publicKey,
        };
    });

// adds an authorization policy to make sure the token is for scope 'api1'
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ApiScope", policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("scope", "api1");
    });
});

var app = builder.Build();

app.UseDeveloperExceptionPage();
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();
// configure the graphql endpoint at "/graphql"
app.UseGraphQL("/graphql", m => m.AuthorizedPolicy = "ApiScope");
// configure Playground at "/"
app.UseGraphQLPlayground(
    new GraphQL.Server.Ui.Playground.PlaygroundOptions {
        GraphQLEndPoint = new PathString("/graphql"),
        SubscriptionsEndPoint = new PathString("/graphql"),
    },
    "/");

await app.RunAsync();
