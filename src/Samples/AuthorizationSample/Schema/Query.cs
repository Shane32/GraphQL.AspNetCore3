using GraphQL;
using GraphQL.Types;

namespace AuthorizationSample.Schema;

public class Query
{
    public static string Hello => "Hello anybody.";

    [Authorize(Roles = "User")]
    public static string HelloUser => "Hello, User!";

    public static string HelloPerson([MyAuthorize(Roles = "User")] string? name) => name ?? "Unknown";

    public static Person GetPerson => new Person { Name = "User" };

    [Authorize("MyPolicy")]
    public static string HelloByPolicy => "Policy Passed!";
}

[Authorize(Roles = "User")]
public class Person
{
    public string Name { get; set; } = null!;
}

// due to an oversight in GraphQL 5.1.0, GraphQLAttribute does not work on query arguments.
// this class is a temporary replacement for [GraphQLAttribute(Roles = "User")]
public class MyAuthorizeAttribute : GraphQLAttribute
{
    public string? Roles { get; set; }
    public override void Modify(QueryArgument queryArgument)
    {
        if (Roles != null)
            queryArgument.AuthorizeWithRoles(Roles);
    }
}

[Authorize("MyPolicy")]
public class Mutation
{
    [Authorize(Roles = "User")]
    public static string Hello => "Hello authenticated user.";
}
