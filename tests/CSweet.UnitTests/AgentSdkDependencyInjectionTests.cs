using CSweet.Agent.SDK;

namespace CSweet.UnitTests;

public sealed class AgentSdkDependencyInjectionTests
{
    [Fact]
    public void CreateGrpcAddress_ConvertsAspireCompositeScheme()
    {
        var address = DependencyInjection.CreateGrpcAddress("https+http://agenthost");

        Assert.Equal("https://agenthost/", address.ToString());
    }

    [Fact]
    public void CreateGrpcAddress_PreservesConcreteHttpEndpoint()
    {
        var address = DependencyInjection.CreateGrpcAddress("http://localhost:58452");

        Assert.Equal("http://localhost:58452/", address.ToString());
    }
}
