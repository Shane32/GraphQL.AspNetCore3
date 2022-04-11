using GraphQL.AspNetCore3;
using PublicApiGenerator;
using Shouldly;
using Xunit;

namespace Tests.ApiTests;

/// <summary>
/// See more info about API approval tests here <see href="https://github.com/JakeGinnivan/ApiApprover"/>.
/// </summary>
public class ApiApprovalTests
{
    [Theory]
    [InlineData(typeof(GraphQLHttpMiddleware))]
    public void PublicApi(Type type)
    {
        string publicApi = type.Assembly.GeneratePublicApi(new ApiGeneratorOptions {
            IncludeAssemblyAttributes = false,
            //WhitelistedNamespacePrefixes = new[] { "Microsoft.Extensions.DependencyInjection" },
            ExcludeAttributes = new[] { "System.Diagnostics.DebuggerDisplayAttribute" }
        }) + Environment.NewLine;

        // See: https://shouldly.readthedocs.io/en/latest/assertions/shouldMatchApproved.html
        // Note: If the AssemblyName.approved.txt file doesn't match the latest publicApi value,
        // this call will try to launch a diff tool to help you out but that can fail on
        // your machine if a diff tool isn't configured/setup.
        publicApi.ShouldMatchApproved(options => {
            options.NoDiff();
            options.WithFilenameGenerator((testMethodInfo, discriminator, fileType, fileExtension) => $"{type.Assembly.GetName().Name}.{fileType}.{fileExtension}");
        });
    }
}
