# Version history / migration notes

## 5.0.0

GraphQL.AspNetCore3 v5 requires GraphQL.NET v7 or newer.

`builder.AddAuthorization()` has been renamed to `builder.AddAuthorizationRule()`.
The old method has been marked as deprecated.

The authorization validation rule and supporting methods have been changed to be
asynchronous, to match the new asynchronous signatures of `IValidationRule` in
GraphQL.NET v7.  If you override any methods, they will need to be updated with
the new signature.

The authorization rule now pulls `ClaimsPrincipal` indirectly from
`ExecutionOptions.User`.  This value must be set properly from the ASP.NET middleware.
While the default implementation has this update in place, if you override
`GraphQLHttpMiddleware.ExecuteRequestAsync` or do not use the provided ASP.NET
middleware, you must set the value in your code.  Another consequence of this
change is that the constructor of `AuthorizationValidationRule` does not require
`IHttpContextAccessor`, and `IHttpContextAccessor` is not required to be registered
within the dependency injection framework (previously provided automatically by
`builder.AddAuthorization()`).

## 4.0.0

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

`WebSocketConnection`, `BaseSubscriptionServer` and both of its implementations
now requires `GraphQLWebSocketOptions` and `IAuthorizationOptions` rather than
`GraphQLHttpMiddlewareOptions` in their constructors.

`IWebSocketHandler` and `WebSocketHandler` have been removed and their protected
members moved to `GraphQLHttpMiddleware`.  Override the new members within the
middleware to add or change WebSocket subprotocol implementations.  The constructor
of `GraphQLHttpMiddleware` has changed slightly to correspond.

`GraphQLHttpMiddleware` has been refactored slightly so the abstract members now
have default implementations in the base class.  Additional arguments were added
to the constructor to accomodate this change.

`AuthorizationVisitor.Authorize` has been renamed to `IsAuthenticated`.

`AuthorizationVisitor.AuthorizeRole` has been renamed to `IsInRole`.

`AuthorizationVisitor.AuthorizePolicy` has been renamed to `Authorize`.

Rename `AuthorizationVisitorBase.SkipField` to `SkipNode` and add support for
skipping fragment spreads or inline fragments via @skip and @include directives.

Added `AuthorizationVisitorBase.GetRecursivelyReferencedFragments` which will skip
fragment definitions not referenced due to a @skip or @include directive.

Returns 405 Method Not Allowed rather than 400 Bad Request when attempting a mutation
over a HTTP GET connection, or a subscription over a HTTP GET/POST connection.

Specifying an authorization policy or required role(s) will implicitly require
the user to be authenticated.

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
