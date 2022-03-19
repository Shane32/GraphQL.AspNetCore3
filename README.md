# Shane32.GraphQL.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/Shane32.GraphQL.AspNetCore.svg)](https://www.nuget.org/packages/Shane32.GraphQL.AspNetCore) [![Coverage Status](https://coveralls.io/repos/github/Shane32/GraphQL.AspNetCore/badge.svg?branch=master)](https://coveralls.io/github/Shane32/GraphQL.AspNetCore?branch=master)

This package is designed for ASP.Net Core 3.1+ to facilitate easy set-up of GraphQL requests
over HTTP.  The code is designed to be used as middleware within the ASP.Net Core pipeline,
serving GET, POST or WebSocket requests.  GET requests process requests from the querystring.
POST requests can be in the form of JSON requests, form submissions, or raw GraphQL strings.
WebSocket requests can use the 'graphql-ws' or 'graphql-transport-ws' protocol.

The middleware can be configured through the `IApplicationBuilder` or `IEndpointRouteBuilder`
builder interfaces.

In addition, an `ExecutionResultActionResult` class is added for returning `ExecutionResult`
instances directly from a controller action.

You will need to register the middleware and the WebSockets handler in the dependency injection
framework in order to use them.

## Configuration

### Typical configuration with HTTP middleware

First add the `Shane32.GraphQL.AspNetCore` nuget package to your application.  It requires
`GraphQL` version 5.0 or later and will default to the newest available 5.x version if none
are installed in your application.

Second, install the `GraphQL.SystemTextJson` or `GraphQL.NewtonsoftJson` package within your
application if you have not already done so.  For best performance, please use the
`GraphQL.SystemTextJson` package.

Then update your `Program.cs` or `Startup.cs` to register the schema, the serialization engine,
the HTTP middleware and WebSocket services.  Also configure GraphQL in the HTTP pipeline by calling
`UseGraphQL` at the appropriate point.  Below is a complete sample of a .NET 6 console app that
hosts a GraphQL endpoint at `http://localhost:5000/graphql`:

#### Project file

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Shane32.GraphQL.AspNetCore" Version="1.0.0" />
    <PackageReference Include="GraphQL.SystemTextJson" Version="5.0.0" />
  </ItemGroup>

</Project>
```

#### Program.cs file

```csharp
using GraphQL;
using GraphQL.MicrosoftDI;
using GraphQL.SystemTextJson;
using Shane32.GraphQL.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGraphQL(b => b
    .AddAutoSchema<Query>()  // schema
    .AddSystemTextJson()     // serializer
    .AddServer());           // HTTP middleware and WebSocket services

var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseGraphQL("/graphql");  // url to host GraphQL endpoint
await app.RunAsync();
```

#### Schema

```csharp
public class Query
{
    public static string Hero() => "Luke Skywalker";
}
```

#### Sample request url

```
http://localhost:5000/graphql?query={hero}
```

#### Sample response

```json
{"data":{"hero":"Luke Skywalker"}}
```

### Configuration with endpoint routing

To use endpoint routing, call `MapGraphQL` from inside the endpoint configuration
builder rather than `UseGraphQL` on the application builder.  See below for the
sample of the application builder code:

```csharp
var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseRouting();
app.UseEndpoints(endpoints => {
    endpoints.MapGraphQL("graphql");
});
await app.RunAsync();
```

### Configuration with a MVC controller

Although not recommended, you may set up a controller action to execute GraphQL
requests.  You will not need `UseGraphQL` or `MapGraphQL` in the application
startup.  Below is the sample controller content.  It does not contain code
to handle WebSockets connections.

#### HomeController.cs

```csharp
[Route("Home")]
public class HomeController : Controller
{
    private readonly IDocumentExecuter<ISchema> _executer;
    private readonly IGraphQLTextSerializer _serializer;

    public HomeController(IDocumentExecuter<ISchema> executer, IGraphQLTextSerializer serializer)
    {
        _executer = executer;
        _serializer = serializer;
    }

    [HttpGet("graphql")]
    public Task<IActionResult> GraphQLGetAsync(string query, string? operationName)
        => ExecuteGraphQLRequestAsync(ParseRequest(query, operationName));

    [HttpPost("graphql")]
    public async Task<IActionResult> GraphQLPostAsync(string query, string? variables, string? operationName, string? extensions)
    {
        if (HttpContext.Request.HasFormContentType) {
            return await ExecuteGraphQLRequestAsync(ParseRequest(query, operationName, variables, extensions));
        } else if (HttpContext.Request.HasJsonContentType()) {
            var request = await _serializer.ReadAsync<GraphQLRequest>(HttpContext.Request.Body, HttpContext.RequestAborted);
            return await ExecuteGraphQLRequestAsync(request);
        }
        return BadRequest();
    }

    private GraphQLRequest ParseRequest(string query, string? operationName, string? variables = null, string? extensions = null)
        => new GraphQLRequest {
            Query = query,
            OperationName = operationName,
            Variables = _serializer.Deserialize<Inputs>(variables),
            Extensions = _serializer.Deserialize<Inputs>(extensions),
        };

    private async Task<IActionResult> ExecuteGraphQLRequestAsync(GraphQLRequest? request)
    {
        if (string.IsNullOrWhiteSpace(request?.Query))
            return BadRequest();
        try {
            return new ExecutionResultActionResult(await _executer.ExecuteAsync(new ExecutionOptions {
                Query = request.Query,
                OperationName = request.OperationName,
                Variables = request.Variables,
                Extensions = request.Extensions,
                CancellationToken = HttpContext.RequestAborted,
                RequestServices = HttpContext.RequestServices,
            }));
        }
        catch {
            return BadRequest();
        }
    }
}
```

### User context configuration

To set the user context to be used during the execution of GraphQL requests,
call `AddUserContextBuilder` during the GraphQL service setup to set a delegate
which will be called when the user context is built.

#### Program.cs / Startup.cs

```csharp
builder.Services.AddGraphQL(b => b
    .AddAutoSchema<Query>()
    .AddSystemTextJson()
    .AddServer()
    .AddUserContextBuilder(httpContext => new MyUserContext(httpContext));
```

#### MyUserContext.cs

```csharp
public class MyUserContext : Dictionary<string, object?>
{
    public ClaimsPrincipal User { get; }

    public MyUserContext(HttpContext context) : base()
    {
        User = context.User;
    }
}
```

## Advanced configuration

For more advanced configurations, see the overloads and configuration options
available for the various builder methods, listed below.  Methods and properties
contain XML comments to provide assistance while coding with Visual Studio.

| Builder interface | Method | Description |
|-------------------|--------|-------------|
| `IGraphQLBuilder` | `AddServer`             | Registers the default HTTP middleware and WebSockets handler with the dependency injection framework. |
| `IGraphQLBuilder` | `AddHttpMiddleware`     | Registers the default HTTP middleware with the dependency injection framework. |
| `IGraphQLBuilder` | `AddWebSocketHandler`   | Registers the default WebSocket handler with the dependency injection framework. |
| `IGraphQLBuilder` | `AddUserContextBuilder` | Set up a delegate to create the UserContext for each GraphQL request. |
| `IApplicationBuilder`   | `UseGraphQL`      | Add the GraphQL middleware to the HTTP request pipeline. |
| `IEndpointRouteBuilder` | `MapGraphQL`      | Add the GraphQL middleware to the HTTP request pipeline. |

A number of the methods contain optional parameters or configuration delegates to
allow further customization.  Please review the overloads of each method to determine
which options are available.  In addition, many methods have more descriptive XML
comments than shown above.

Below are descriptions of the options available when registering the HTTP middleware
or WebSocket handler.

### GraphQLHttpMiddlewareOptions

| Property | Description | Default value |
|----------|-------------|---------------|
| `HandleGet` | Enables handling of GET requests. | True |
| `HandlePost` | Enables handling of POST requests. | True |
| `HandleWebSockets` | Enables handling of WebSockets requests. | True |
| `EnableBatchedRequests` | Enables handling of batched GraphQL requests for POST requests when formatted as JSON. | True |
| `BatchedRequestsExecuteInParallel` | Enables parallel execution of batched GraphQL requests. | True |
| `ReadQueryStringOnPost` | Enables parsing the query string on POST requests. | False |
| `ReadVariablesFromQueryString` | Enables reading variables from the query string. | False |
| `ReadExtensionsFromQueryString` | Enables reading extensions from the query string. | False |

### WebSocketHandlerOptions

| Property | Description | Default value |
|----------|-------------|---------------|
| `ConnectionInitWaitTimeout` | The amount of time to wait for a GraphQL initialization packet before the connection is closed. | 10 seconds |
| `KeepAliveTimeout`          | The amount of time to wait between sending keep-alive packets. | 30 seconds |
| `DisconnectionTimeout`      | The amount of time to wait to attempt a graceful teardown of the WebSockets protocol. | 10 seconds |

## Additional notes

### Service scope

By default, a dependency injection service scope is created for each GraphQL execution
in cases where it is possible that multiple GraphQL requests may be executing within the
same service scope:

1. A batched GraphQL request is executed.
2. A GraphQL request over a WebSocket connection is executed.

However, field resolvers for child fields of subscription nodes will not by default execute
with a service scope.  Rather, the `context.RequestServices` property will contain a reference
to a disposed service scope that cannot be used.

To solve this issue, please configure the scoped subscription execution strategy from the
GraphQL.MicrosoftDI package as follows:

> Unfortunately this class does not yet exist

For single GET / POST requests, the service scope from the underlying HTTP context is used.

### User context builder

The user context builder interface is executed only once, within the dependency injection
service scope of the original HTTP request.  For batched requests, the same user context
instance is passed to each GraphQL execution.  For WebSocket requests, the same user
context instance is passed to each GraphQL subscription and data event resolver execution.

As such, do not create objects within the user context that rely on having the same
dependency injection service scope as the field resolvers.  Since WebSocket connections
are long-lived, using scoped services within a user context builder will result in those
scoped services having a matching long lifetime.  You may wish to alleviate this by
creating a service scope temporarily within your user context builder.
