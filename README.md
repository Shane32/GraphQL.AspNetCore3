# Shane32.GraphQL.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/Shane32.GraphQL.AspNetCore.svg)](https://www.nuget.org/packages/Shane32.GraphQL.AspNetCore) [![Coverage Status](https://coveralls.io/repos/github/Shane32/GraphQL.AspNetCore/badge.svg?branch=master)](https://coveralls.io/github/Shane32/GraphQL.AspNetCore?branch=master)

This package is designed for ASP.Net Core 3.1+ to facilitate easy set-up of GraphQL requests
over HTTP.  The code is designed to be used as middleware within the ASP.Net Core pipeline,
serving GET, POST or WebSocket requests.  GET requests process requests from the querystring.
POST requests can be in the form of JSON requests, form submissions, or raw GraphQL strings.
WebSocket requests can use the `graphql-ws` or `graphql-transport-ws` WebSocket sub-protocol,
as defined in the [apollographql/subscriptions-transport-ws](https://github.com/apollographql/subscriptions-transport-ws)
and [enisdenjo/graphql-ws](https://github.com/enisdenjo/graphql-ws) respoitories, respectively.  The
`graphql-subscriptions` sub-protocol is not supported.

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

### Configuration options

Below are descriptions of the options available when registering the HTTP middleware
or WebSocket handler.

#### GraphQLHttpMiddlewareOptions

| Property | Description | Default value |
|----------|-------------|---------------|
| `BatchedRequestsExecuteInParallel` | Enables parallel execution of batched GraphQL requests. | True |
| `EnableBatchedRequests` | Enables handling of batched GraphQL requests for POST requests when formatted as JSON. | True |
| `HandleGet` | Enables handling of GET requests. | True |
| `HandlePost` | Enables handling of POST requests. | True |
| `HandleWebSockets` | Enables handling of WebSockets requests. | True |
| `ReadExtensionsFromQueryString` | Enables reading extensions from the query string. | False |
| `ReadQueryStringOnPost` | Enables parsing the query string on POST requests. | False |
| `ReadVariablesFromQueryString` | Enables reading variables from the query string. | False |
| `ValidationErrorsReturnBadRequest` | When enabled, GraphQL requests with validation errors have the HTTP status code set to 400 Bad Request. | True |

#### WebSocketHandlerOptions

| Property | Description | Default value |
|----------|-------------|---------------|
| `ConnectionInitWaitTimeout` | The amount of time to wait for a GraphQL initialization packet before the connection is closed. | 10 seconds |
| `KeepAliveTimeout`          | The amount of time to wait between sending keep-alive packets. | 30 seconds |
| `DisconnectionTimeout`      | The amount of time to wait to attempt a graceful teardown of the WebSockets protocol. | 10 seconds |

### Multi-schema configuration

You may use the generic versions of the various builder methods to map a URL to a particular schema.

```csharp
var app = builder.Build();
app.UseDeveloperExceptionPage();
app.UseGraphQL<DogSchema>("/graphql/dogs");
app.UseGraphQL<CatSchema>("/graphql/cats");
await app.RunAsync();
```

### Customizing middleware behavior

GET/POST requests are handled directly by the `GraphQLHttpMiddleware`.
WebSocket requests are passed from the middleware to an `IWebSocketHandler` instance
configured for the requested WebSocket sub-protocol.

#### GraphQLHttpMiddleware

The base middleware functionality is contained within `GraphQLHttpMiddleware`, with code
to perform execution of GraphQL requests in the derived class `GraphQLHttpMiddleware<TSchema>`.
The classes are organized as follows:

- `InvokeAsync` is the entry point to the middleware.  For WebSocket connection requests,
  execution is immediately passed to `HandleWebSocketsAsync`.
- Methods that start with `Handle` are passed the `HttpContext` and `RequestDelegate`
  instance, and may handle the request or pass execution to the `RequestDelegate` thereby
  skipping this execution handler.  This includes methods to handle execution of single or
  batch queries or returning error conditions.
- Methods that start with `Write` are for writing responses to the output stream.
- Methods that start with `Execute` are for executing GraphQL requests.

A list of methods are as follows:

| Method                      | Description |
|-----------------------------|-------------|
| `InvokeAsync`               | Entry point of the middleware |
| `HandleRequestAsync`        | Handles a single GraphQL request. |
| `HandleBatchRequestAsync`   | Handles a batched GraphQL request. |
| `HandleWebSocketAsync`      | Handles a WebSocket connection request. |
| `BuildUserContextAsync`     | Builds the user context based on a `HttpContext`. |
| `ExecuteRequestAsync`       | Executes a GraphQL request. |
| `ExecuteScopedRequestAsync` | Executes a GraphQL request with a scoped service provider. |
| `WriteErrorResponseAsync`   | Writes the specified error message as a JSON-formatted GraphQL response, with the specified HTTP status code. |
| `WriteJsonResponseAsync`    | Writes the specified object (usually a GraphQL response) as JSON to the HTTP response stream. |

| Error handling method                         | Description |
|-----------------------------------------------|-------------|
| `HandleBatchedRequestsNotSupportedAsync`      | Writes a '400 Batched requests are not supported.' message to the output. |
| `HandleContentTypeCouldNotBeParsedErrorAsync` | Writes a '415 Invalid Content-Type header: could not be parsed.' message to the output. |
| `HandleDeserializationErrorAsync`             | Writes a '400 JSON body text could not be parsed.' message to the output. |
| `HandleInvalidContentTypeErrorAsync`          | Writes a '415 Invalid Content-Type header: non-supported type.' message to the output. |
| `HandleInvalidHttpMethodErrorAsync`           | Indicates that an unsupported HTTP method was requested. Executes the next delegate in the chain by default. |
| `HandleNoQueryErrorAsync`                     | Writes a '400 GraphQL query is missing.' message to the output. |
| `HandleWebSocketSubProtocolNotSupportedAsync` | Writes a '400 Invalid WebSocket sub-protocol.' message to the output. |

#### WebSocket handler classes

The WebSocket handling code is organized as follows:

| Interface / Class                | Description |
|----------------------------------|-------------|
| `IWebSocketHandler`              | Handles one or more specific WebSocket sub-protocols, returning once the client has completely disconnected. |
| `IOperationMessageSendStream`    | Provides methods to a send a message to a client or close the connection. |
| `IOperationMessageReceiveStream` | Handles incoming messages from the client. |
| `WebSocketHandler`               | Handles `graphql-ws` and `graphql-transport-ws` sub-protocols, setting up a new `WebSocketConnection` with a `NewSubscriptionServer` or `OldSubscriptionServer` depending on the sub-protocol requested. |
| `WebSocketHandlerOptions`        | Provides configuration options to a `WebSocketHandler` instance |
| `WebSocketConnection`            | Standard implementation of a message pump for `OperationMessage` messages across a WebSockets connection.  Implements `IOperationMessageSendStream` and delivers messages to a specified `IOperationMessageReceiveStream`. |
| `BaseSubscriptionServer`         | Abstract implementation of `IOperationMessageReceiveStream`, a message handler for `OperationMessage` messages.  Provides base functionality for managing subscriptions and requests. |
| `OldSubscriptionServer`          | Implementation of `IOperationMessageReceiveStream` for the `graphql-ws` sub-protocol. |
| `NewSubscriptionServer`          | Implementation of `IOperationMessageReceiveStream` for the `graphql-transport-ws` sub-protocol. |

Typically if you wish to change functionality or support another sub-protocol
you will need to perform the following:

1. Derive from `NewSubscriptionServer`and/or `OldSubscriptionServer`, modifying functionality as needed.
2. Write a handler class that implements `IWebSocketHandler`, or derive from `WebSocketHandler`
   and perform the following:
   1. If necessary, override the `SupportedSubProtocols` property to return the list of
      sub-protocols your handler supports.
   2. Override the `CreateSendStream` method to return your new subscription server class when a
      request arrives.  If your handler supports multiple sub-protocols, it should return the
      proper subscription server for the requested sub-protocol.
3. Register the new WebSocket hander in the DI framework via `.AddWebSocketHandler<T>()`.
4. If desired, disable the existing WebSocket handler by replacing the call to `.AddServer()`
   with `.AddHttpMiddleware()`, or by removing the call to `.AddWebSocketHandler()`.
   Alternatively, register the WebSocket handlers in the order of priority, for instance adding
   the call to `.AddWebSocketHandler<T>()` prior to the call of `.AddServer()`.

There exists a few additional classes to support the above.  Please refer to the source code
of `NewSubscriptionServer` if you are attempting to add support for another protocol.

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
