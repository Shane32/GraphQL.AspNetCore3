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

#### Project file:

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

#### Program.cs file:

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
app.UseGraphQL("/graphql");
await app.RunAsync();

// schema
public class Query
{
    public static string Hero() => "Luke Skywalker";
}
```

