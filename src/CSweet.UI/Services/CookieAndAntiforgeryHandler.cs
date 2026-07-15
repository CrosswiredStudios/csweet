using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace CSweet.UI.Services;

public sealed class CookieAndAntiforgeryHandler : DelegatingHandler
{
    private readonly AuthSessionStore _session;

    public CookieAndAntiforgeryHandler(AuthSessionStore session) => _session = session;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        if (request.Method != HttpMethod.Get &&
            request.Method != HttpMethod.Head &&
            request.Method != HttpMethod.Options &&
            !string.IsNullOrWhiteSpace(_session.AntiforgeryToken))
        {
            request.Headers.TryAddWithoutValidation("X-CSWEET-CSRF", _session.AntiforgeryToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
