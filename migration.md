# Version history / migration notes

## 4.0.0 (in progress)

Remove `AllowEmptyQuery` option, as this error condition is now handled by the
GraphQL.NET 5.3.0 `DocumentExecuter`, as well as the error classes that relate.

Move `GraphQL.AspNetCore3.AuthorizationRule.AuthorizationVisitor` class to
`GraphQL.AspNetCore3.AuthorizationVisitor` and add `AuthorizationVisitorBase`
class to easier override functionality when validating nodes.

Drastically reduce allocations within AuthorizationVisitor.

Change `IWebSocketConnection` to inherit `IDisposable`.

Rename option `BatchedRequestsExecuteInParallel` to `ExecuteBatchedRequestsInParallel`.

Rename `IWebSocketConnection.CloseConnectionAsync` to `CloseAsync`.

Split POST request parsing code into `ReadPostContentAsync`, to allow overriding
in derived implementations -- for example, to read form files into variables.

Rename `BaseSubscriptionServer.Client` to `Connection`.

`BaseSubscriptionServer` now requires `GraphQLWebSocketOptions` and `IAuthorizationOptions`
rather than `GraphQLHttpMiddlewareOptions`.  Corresponding changes were made to the two
implementations of this class.

## 3.0.0

Supports building user contexts after authentication has occurred for WebSocket
connections; supports and returns media type of `application/graphql+json` by default.

Support for ASP.NET Core 2.1 added, tested with .NET Core 2.1 and .NET Framework 4.8.

Removed `HandleNoQueryErrorAsync` method; validation for this scenario already
exists within `ExecuteRequestAsync`.

Added `AllowEmptyQuery` option to allow for Automatic Persisted Queries if configured
through a custom `IDocumentExecuter`.

## 2.1.0

Authentication validation rule and support

New features:

- Added codes to middleware errors
- Added validation rule for schema authorization; supports authorization rules set on
  the schema, output graphs, fields of output graphs, and query arguments.  Skips fields
  appropriately if marked with `@skip` or `@include`.  Does not check authorization rules
  set on input graphs or input fields.
- Added authorization options to middleware to authenticate connection prior to execution
  of request.

## 1.0.0

Initial release

Added features over GraphQL.Server:

- Single `UseGraphQL` call to enable middleware including WebSocket support
- Options to enable/disable GET, POST and/or WebSocket requests
- Options to enable/disable batched requests and/or executed batched requests in parallel
- Options to enable/disable reading variables and extensions from the query string
- Option to enable/disable reading query string for POST requests
- Option to return OK or BadRequest when a validation error occurs
- More virtual methods allowing to override specific behavior
- WebSocket support of new graphql-ws protocol (see https://github.com/enisdenjo/graphql-ws)
- Compatible with GraphQL v5
