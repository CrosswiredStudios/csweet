using System.Text;
using CSweet.Application.Setup;
using CSweet.Contracts.Agents;
using CSweet.Infrastructure.Persistence;
using CSweet.Infrastructure.Setup;
using Microsoft.EntityFrameworkCore;

namespace CSweet.UnitTests;

public class AgentImportPreviewServiceTests
{
    [Theory]
    [InlineData("https://github.com/example/research-agent", "https://github.com/example/research-agent")]
    [InlineData("https://github.com/example/research-agent.git/", "https://github.com/example/research-agent")]
    public void Normalize_AcceptsRepositoryUrls(string input, string expected)
    {
        var repository = GitHubRepositoryUrlNormalizer.Normalize(input);

        Assert.Equal("example", repository.Owner);
        Assert.Equal("research-agent", repository.Name);
        Assert.Equal(expected, repository.RepositoryUrl);
    }

    [Theory]
    [InlineData("http://github.com/example/research-agent")]
    [InlineData("https://gitlab.com/example/research-agent")]
    [InlineData("https://github.com/example/research-agent/tree/main")]
    [InlineData("https://github.com/example/research-agent?tab=readme")]
    public void Normalize_RejectsUnsupportedUrls(string input)
    {
        Assert.Throws<AgentImportPreviewException>(() =>
            GitHubRepositoryUrlNormalizer.Normalize(input));
    }

    [Fact]
    public async Task PreviewAsync_PersistsImmutablePreviewAndWarnings()
    {
        await using var dbContext = CreateDbContext();
        var repositoryClient = new FakeGitHubAgentRepositoryClient(ValidManifest());
        var service = new AgentImportPreviewService(
            dbContext,
            repositoryClient,
            new TestAuditEventWriter());

        var result = await service.PreviewAsync(new PreviewAgentImportRequest(
            "https://github.com/example/research-agent"));

        Assert.Equal("Previewed", result.Status);
        Assert.Equal(FakeGitHubAgentRepositoryClient.CommitSha, result.CommitSha);
        Assert.Equal(64, result.ManifestDigest.Length);
        Assert.Equal("dotnet-project", result.RuntimeType);
        Assert.Contains(result.Warnings, warning => warning.Code == "network_access_requested");
        Assert.Single(await dbContext.AgentPackageSources.ToListAsync());
        var version = Assert.Single(await dbContext.AgentPackageVersions.ToListAsync());
        Assert.Equal(result.ImportId, version.Id);
        Assert.Equal(result.ManifestDigest, version.ManifestDigest);
    }

    [Fact]
    public async Task PreviewAsync_RejectsProjectPathTraversalWithoutPersisting()
    {
        await using var dbContext = CreateDbContext();
        var invalidManifest = ValidManifest().Replace(
            "src/ResearchAgent/ResearchAgent.csproj",
            "../ResearchAgent.csproj",
            StringComparison.Ordinal);
        var service = new AgentImportPreviewService(
            dbContext,
            new FakeGitHubAgentRepositoryClient(invalidManifest),
            new TestAuditEventWriter());

        var exception = await Assert.ThrowsAsync<AgentImportPreviewException>(() =>
            service.PreviewAsync(new PreviewAgentImportRequest(
                "https://github.com/example/research-agent")));

        Assert.Contains("without parent traversal", exception.Message);
        Assert.Empty(await dbContext.AgentPackageSources.ToListAsync());
        Assert.Empty(await dbContext.AgentPackageVersions.ToListAsync());
    }

    [Fact]
    public async Task PreviewAsync_ReusesExistingImmutablePreview()
    {
        await using var dbContext = CreateDbContext();
        var service = new AgentImportPreviewService(
            dbContext,
            new FakeGitHubAgentRepositoryClient(ValidManifest()),
            new TestAuditEventWriter());
        var request = new PreviewAgentImportRequest("https://github.com/example/research-agent");

        var first = await service.PreviewAsync(request);
        var second = await service.PreviewAsync(request);

        Assert.Equal(first.ImportId, second.ImportId);
        Assert.Single(await dbContext.AgentPackageSources.ToListAsync());
        Assert.Single(await dbContext.AgentPackageVersions.ToListAsync());
    }

    private static CSweetDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CSweetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CSweetDbContext(options);
    }

    private static string ValidManifest() => """
        {
          "manifestVersion": "1.0",
          "kind": "agent",
          "id": "com.example.research-agent",
          "name": "Research Agent",
          "version": "1.2.3",
          "publisher": { "id": "com.example", "name": "Example" },
          "runtime": {
            "type": "dotnet-project",
            "projectPath": "src/ResearchAgent/ResearchAgent.csproj",
            "targetFramework": "net10.0",
            "defaultActivationMode": "Periodic"
          },
          "protocol": { "minimumVersion": "1.0", "maximumVersion": "1.x" },
          "provides": [{"name":"research.execute.v1"}],
          "requires": [{"name":"documents.read.v1","scope":"organization"}],
          "events": {
            "subscribes": ["research.requested.v1"],
            "publishes": ["research.completed.v1"]
          },
          "webAccess": {
            "mode": "Allowlist",
            "rules": [{"scheme":"https","host":"api.example.com","pathPrefix":"/","methods":["GET"],"protocol":"http","purpose":"Research"}]
          }
        }
        """;

    private sealed class FakeGitHubAgentRepositoryClient : IGitHubAgentRepositoryClient
    {
        public const string CommitSha = "0123456789abcdef0123456789abcdef01234567";
        private readonly byte[] _manifest;

        public FakeGitHubAgentRepositoryClient(string manifest)
        {
            _manifest = Encoding.UTF8.GetBytes(manifest);
        }

        public Task<string> GetDefaultBranchAsync(
            string repositoryOwner,
            string repositoryName,
            CancellationToken cancellationToken) => Task.FromResult("main");

        public Task<string> ResolveCommitShaAsync(
            string repositoryOwner,
            string repositoryName,
            string reference,
            CancellationToken cancellationToken) => Task.FromResult(CommitSha);

        public Task<byte[]> GetRootManifestAsync(
            string repositoryOwner,
            string repositoryName,
            string commitSha,
            CancellationToken cancellationToken) => Task.FromResult(_manifest);
    }
}
