# Version history / migration notes

## 7.0.0

GraphQL.AspNetCore3 v7 requires GraphQL.NET v8 or newer.

### New features

- Supports JWT WebSocket Authentication using the separately-provided `GraphQL.AspNetCore3.JwtBearer` package.
  - Inherits all options configured by the `Microsoft.AspNetCore.Authentication.JwtBearer` package.
  - Supports multiple authentication schemes, configurable via the `GraphQLHttpMiddlewareOptions.AuthenticationSchemes` property.
  - Defaults to attempting the `AuthenticationOptions.DefaultAuthenticateScheme` scheme if not specified.

### Breaking changes

- `AuthenticationSchemes` property added to `IAuthorizationOptions` interface.
- `IWebSocketAuthenticationService.AuthenticateAsync` parameters refactored into an `AuthenticationRequest` class.

## 6.0.0

GraphQL.AspNetCore3 v6 requires GraphQL.NET v8 or newer.

### New features

- When using `FormFileGraphType` with type-first schemas, you may specify the allowed media
  types for the file by using the new `[MediaType]` attribute on the argument or input object field.
- Cross-site request forgery (CSRF) protection has been added for both GET and POST requests,
  enabled by default.
- Status codes for validation errors are now, by default, determined by the response content type,
  and for authentication errors may return a 401 or 403 status code.  These changes are purusant
  to the [GraphQL over HTTP specification](https://github.com/graphql/graphql-over-http/blob/main/spec/GraphQLOverHTTP.md).
  See the breaking changes section below for more information.

### Breaking changes

- `GraphQLHttpMiddlewareOptions.ValidationErrorsReturnBadRequest` is now a nullable boolean where
   `null` means "use the default behavior".  The default behavior is to return a 200 status code
  when the response content type is `application/json` and a 400 status code otherwise.  The
  default value for this in v7 was `true`; set this option to retain the v7 behavior.
- The validation rules' signatures have changed slightly due to the underlying changes to the
  GraphQL.NET library.  Please see the GraphQL.NET v8 migration document for more information.
- Cross-site request forgery (CSRF) protection has been enabled for all requests by default.
  This will require that the `GraphQL-Require-Preflight` header be sent with all GET requests and
  all form-POST requests.  To disable this feature, set the `CsrfProtectionEnabled` property on the
  `GraphQLMiddlewareOptions` class to `false`.  You may also configure the headers list by modifying
  the `CsrfProtectionHeaders` property on the same class.  See the readme for more details.
- Form POST requests are disabled by default; to enable them, set the `ReadFormOnPost` setting
  to `true`.
- Validation errors such as authentication errors may now be returned with a 'preferred' status
  code instead of a 400 status code.  This occurs when (1) the response would otherwise contain
  a 400 status code (e.g. the execution of the document has not yet begun), and (2) all errors
  in the response prefer the same status code.  For practical purposes, this means that the included
  errors triggered by the authorization validation rule will now return 401 or 403 when appropriate.
- The `SelectResponseContentType` method now returns a `MediaTypeHeaderValue` instead of a string.
- The `AuthorizationVisitorBase.GetRecursivelyReferencedUsedFragments` method has been removed as
  `ValidationContext` now provides an overload to `GetRecursivelyReferencedFragments` which will only
  return fragments in use by the specified operation.
- The `AuthorizationVisitorBase.SkipNode` method has been removed as `ValidationContext` now provides
  a `ShouldIncludeNode` method.

## Other changes

- GraphiQL has been bumped from 1.5.1 to 3.2.0.

## 5.0.0

GraphQL.AspNetCore3 v5 requires GraphQL.NET v7.

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
