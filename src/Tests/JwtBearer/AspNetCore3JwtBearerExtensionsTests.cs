using GraphQL.AspNetCore3.JwtBearer;
using ServiceLifetime = GraphQL.DI.ServiceLifetime;

namespace Tests.JwtBearer;

public class AspNetCore3JwtBearerExtensionsTests
{
    [Fact]
    public void AddJwtBearerAuthentication_ShouldAddJwtWebSocketAuthenticationService()
    {
        // Arrange
        var serviceRegisterMock = new Mock<IServiceRegister>(MockBehavior.Strict);
        var graphQLBuilderMock = new Mock<IGraphQLBuilder>(MockBehavior.Strict);

        // Setup the Services property to return the mocked IServiceRegister
        graphQLBuilderMock
            .SetupGet(x => x.Services)
            .Returns(serviceRegisterMock.Object);

        // Setup the Register method to accept specific parameters
        serviceRegisterMock
            .Setup(x => x.Register(
                typeof(IWebSocketAuthenticationService),
                typeof(JwtWebSocketAuthenticationService),
                ServiceLifetime.Singleton,
                false))
            .Returns(serviceRegisterMock.Object);
            
        // Setup the Configure method to accept any Action<JwtBearerAuthenticationOptions, IServiceProvider>
        serviceRegisterMock
            .Setup(x => x.Configure<JwtBearerAuthenticationOptions>(It.IsAny<Action<JwtBearerAuthenticationOptions, IServiceProvider>>()))
            .Returns(serviceRegisterMock.Object);

        // Act
        var result = graphQLBuilderMock.Object.AddJwtBearerAuthentication();

        // Assert
        result.ShouldBe(graphQLBuilderMock.Object);

        // Verify that Register was called with the correct parameters
        serviceRegisterMock.Verify(
            x => x.Register(
                typeof(IWebSocketAuthenticationService),
                typeof(JwtWebSocketAuthenticationService),
                ServiceLifetime.Singleton,
                false),
            Times.Once);
    }
}
