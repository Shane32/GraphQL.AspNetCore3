using System.Net;
using GraphQL.MicrosoftDI;
using GraphQL.SystemTextJson;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Shane32.GraphQL.AspNetCore;

namespace Tests;

public class GraphQLHttpMiddlewareTests : IDisposable
{
    private GraphQLHttpMiddlewareOptions _options = null!;
    private GraphQLHttpMiddlewareOptions _options2 = null!;
    private readonly TestServer _server;

    public GraphQLHttpMiddlewareTests()
    {
        var hostBuilder = new WebHostBuilder();
        hostBuilder.ConfigureServices(services => {
            services.AddSingleton<Chat.Services.ChatService>();
            services.AddGraphQL(b => b
                .AddAutoSchema<Chat.Schema.Query>(s => s
                    .WithMutation<Chat.Schema.Mutation>()
                    .WithSubscription<Chat.Schema.Subscription>())
                .AddSchema<Schema2>()
                .AddSystemTextJson()
                .AddServer());
        });
        hostBuilder.Configure(app => {
            app.UseWebSockets();
            app.UseGraphQL("/graphql", opts => {
                _options = opts;
            });
            app.UseGraphQL<Schema2>("/graphql2", opts => {
                _options2 = opts;
            });
        });
        _server = new TestServer(hostBuilder);
    }

    private class Schema2 : Schema
    {
        public Schema2()
        {
            Query = new AutoRegisteringObjectGraphType<Query2>();
        }
    }

    private class Query2
    {
        public static string? Ext(IResolveFieldContext context)
            => context.InputExtensions.TryGetValue("test", out var value) ? value?.ToString() : null;
    }

    public void Dispose() => _server.Dispose();

    [Fact]
    public async Task Get_Works()
    {
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql?query={count}");
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBeNull();
        var str = await response.Content.ReadAsStringAsync();
        str.ShouldBe(@"{""data"":{""count"":0}}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_WithError(bool validationErrorsReturnBadRequest)
    {
        _options.ValidationErrorsReturnBadRequest = validationErrorsReturnBadRequest;
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql?query={invalid}");
        response.StatusCode.ShouldBe(validationErrorsReturnBadRequest ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBeNull();
        var str = await response.Content.ReadAsStringAsync();
        str.ShouldBe(@"{""errors"":[{""message"":""Cannot query field \u0027invalid\u0027 on type \u0027Query\u0027."",""locations"":[{""line"":1,""column"":2}],""extensions"":{""code"":""FIELDS_ON_CORRECT_TYPE"",""codes"":[""FIELDS_ON_CORRECT_TYPE""],""number"":""5.3.1""}}]}");
    }

    [Fact]
    public async Task Get_Disabled()
    {
        _options.HandleGet = false;
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql?query={count}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_QueryParseError(bool validationErrorsReturnBadRequest)
    {
        _options.ValidationErrorsReturnBadRequest = validationErrorsReturnBadRequest;
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql?query={");
        response.StatusCode.ShouldBe(validationErrorsReturnBadRequest ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBeNull();
        var str = await response.Content.ReadAsStringAsync();
        str.ShouldBe(@"{""errors"":[{""message"":""Error parsing query: Expected Name, found EOF; for more information see http://spec.graphql.org/October2021/#Field"",""locations"":[{""line"":1,""column"":2}],""extensions"":{""code"":""SYNTAX_ERROR"",""codes"":[""SYNTAX_ERROR""]}}]}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_NoQuery(bool validationErrorsReturnBadRequest)
    {
        _options.ValidationErrorsReturnBadRequest = validationErrorsReturnBadRequest;
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql?query=");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBeNull();
        var str = await response.Content.ReadAsStringAsync();
        str.ShouldBe(@"{""errors"":[{""message"":""GraphQL query is missing.""}]}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_Mutation(bool validationErrorsReturnBadRequest)
    {
        _options.ValidationErrorsReturnBadRequest = validationErrorsReturnBadRequest;
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql?query=mutation{clearMessages}");
        response.StatusCode.ShouldBe(validationErrorsReturnBadRequest ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBeNull();
        var str = await response.Content.ReadAsStringAsync();
        str.ShouldBe(@"{""errors"":[{""message"":""Only query operations allowed for GET requests."",""locations"":[{""line"":1,""column"":1}],""extensions"":{""code"":""OPERATION_TYPE_VALIDATION"",""codes"":[""OPERATION_TYPE_VALIDATION""]}}]}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_Subscription(bool validationErrorsReturnBadRequest)
    {
        _options.ValidationErrorsReturnBadRequest = validationErrorsReturnBadRequest;
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql?query=subscription{newMessages{id}}");
        response.StatusCode.ShouldBe(validationErrorsReturnBadRequest ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBeNull();
        var str = await response.Content.ReadAsStringAsync();
        str.ShouldBe(@"{""errors"":[{""message"":""Only query operations allowed for GET requests."",""locations"":[{""line"":1,""column"":1}],""extensions"":{""code"":""OPERATION_TYPE_VALIDATION"",""codes"":[""OPERATION_TYPE_VALIDATION""]}}]}");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_WithVariables(bool readVariablesFromQueryString)
    {
        _options.ReadVariablesFromQueryString = readVariablesFromQueryString;
        _options.ValidationErrorsReturnBadRequest = false;
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql?query=query($from:String!){allMessages(from:$from){id}}&variables={%22from%22:%22abc%22}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBeNull();
        var str = await response.Content.ReadAsStringAsync();
        if (readVariablesFromQueryString) {
            str.ShouldBe(@"{""data"":{""allMessages"":[]}}");
        } else {
            str.ShouldBe(@"{""errors"":[{""message"":""Variable \u0027$from\u0027 is invalid. No value provided for a non-null variable."",""locations"":[{""line"":1,""column"":7}],""extensions"":{""code"":""INVALID_VALUE"",""codes"":[""INVALID_VALUE""],""number"":""5.8""}}]}");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Get_VariableParseError(bool validationErrorsReturnBadRequest)
    {
        _options.ValidationErrorsReturnBadRequest = validationErrorsReturnBadRequest;
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql?query=query($from:String!){allMessages(from:$from){id}}&variables={");
        response.StatusCode.ShouldBe(validationErrorsReturnBadRequest ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBeNull();
        var str = await response.Content.ReadAsStringAsync();
        str.ShouldBe(@"{""errors"":[{""message"":""Variable \u0027$from\u0027 is invalid. No value provided for a non-null variable."",""locations"":[{""line"":1,""column"":7}],""extensions"":{""code"":""INVALID_VALUE"",""codes"":[""INVALID_VALUE""],""number"":""5.8""}}]}");
    }

    [Theory]
    [InlineData("test1", @"{""data"":{""count"":0}}")]
    [InlineData("test2", @"{""data"":{""allMessages"":[]}}")]
    public async Task Get_OperationName(string opName, string expected)
    {
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql?query=query test1{count} query test2{allMessages{id}}&operationName=" + opName);
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBeNull();
        var str = await response.Content.ReadAsStringAsync();
        str.ShouldBe(expected);
    }

    [Theory]
    [InlineData(true, @"{""data"":{""ext"":""abc""}}")]
    [InlineData(false, @"{""data"":{""ext"":null}}")]
    public async Task Get_Extensions(bool readExtensions, string expected)
    {
        _options2.ReadExtensionsFromQueryString = readExtensions;
        var client = _server.CreateClient();
        using var response = await client.GetAsync("/graphql2?query={ext}&extensions={%22test%22:%22abc%22}");
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        response.Content.Headers.ContentType.CharSet.ShouldBeNull();
        var str = await response.Content.ReadAsStringAsync();
        str.ShouldBe(expected);
    }
}
