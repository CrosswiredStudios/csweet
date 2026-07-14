using System.Net;
using System.Text;
using CSweet.Application.Setup;
using CSweet.Infrastructure.Setup;

namespace CSweet.UnitTests;

public class GitHubAgentRepositoryClientTests
{
    [Fact]
    public async Task Client_ResolvesDefaultBranchCommitAndRootManifest()
    {
        var handler = new RecordingHandler(request => request.RequestUri?.AbsolutePath switch
        {
            "/repos/example/research-agent" => Json("{\"default_branch\":\"main\"}"),
            "/repos/example/research-agent/commits/main" =>
                Json("{\"sha\":\"0123456789abcdef0123456789abcdef01234567\"}"),
            "/repos/example/research-agent/contents/csweet-agent.json" =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"manifestVersion\":\"1.0\"}")
                },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var client = CreateClient(handler);

        var branch = await client.GetDefaultBranchAsync("example", "research-agent", CancellationToken.None);
        var sha = await client.ResolveCommitShaAsync("example", "research-agent", branch, CancellationToken.None);
        var manifest = await client.GetRootManifestAsync("example", "research-agent", sha, CancellationToken.None);

        Assert.Equal("main", branch);
        Assert.Equal("0123456789abcdef0123456789abcdef01234567", sha);
        Assert.Contains("manifestVersion", Encoding.UTF8.GetString(manifest));
        Assert.Equal(
            "/repos/example/research-agent/contents/csweet-agent.json?ref=0123456789abcdef0123456789abcdef01234567",
            handler.Requests[2]);
    }

    [Fact]
    public async Task GetRootManifestAsync_RejectsManifestOverOneMegabyte()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[(1024 * 1024) + 1])
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<AgentImportPreviewException>(() =>
            client.GetRootManifestAsync(
                "example",
                "research-agent",
                "0123456789abcdef0123456789abcdef01234567",
                CancellationToken.None));

        Assert.Contains("1 MB", exception.Message);
    }

    private static GitHubAgentRepositoryClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") });

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri?.PathAndQuery ?? string.Empty);
            return Task.FromResult(_responseFactory(request));
        }
    }
}