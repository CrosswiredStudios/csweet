using CSweet.Infrastructure.Setup;

namespace CSweet.UnitTests;

public sealed class DotNetAgentImageResolverTests
{
    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/sdk:9.0", "net10.0", "mcr.microsoft.com/dotnet/sdk:10.0")]
    [InlineData("mcr.microsoft.com/dotnet/runtime:9.0", "net10.0", "mcr.microsoft.com/dotnet/runtime:10.0")]
    public void OfficialImages_FollowTheAgentTargetFramework(
        string configuredImage,
        string targetFramework,
        string expectedImage)
    {
        var actual = configuredImage.Contains("/sdk:", StringComparison.Ordinal)
            ? DotNetAgentImageResolver.ResolveBuilderImage(configuredImage, targetFramework)
            : DotNetAgentImageResolver.ResolveRuntimeImage(configuredImage, targetFramework);

        Assert.Equal(expectedImage, actual);
    }

    [Fact]
    public void CustomImages_ArePreserved()
    {
        Assert.Equal(
            "registry.example/agent-builder:approved",
            DotNetAgentImageResolver.ResolveBuilderImage(
                "registry.example/agent-builder:approved",
                "net10.0"));
    }
}
