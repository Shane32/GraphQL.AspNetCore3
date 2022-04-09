using GraphQL;

namespace AuthorizationSample.Schema;

public class Query
{
    public static string Hello => "Hello anybody.";

    [GraphQLAuthorize(Roles = "User")]
    public static string HelloUser => "Hello, User!";
}
