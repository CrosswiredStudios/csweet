using CSweet.Contracts.Auth;

namespace CSweet.UI.Services;

public sealed class AuthSessionStore
{
    public AuthStatusResponse? Current { get; private set; }
    public string? AntiforgeryToken => Current?.AntiforgeryToken;

    public void Set(AuthStatusResponse status) => Current = status;
    public void Clear() => Current = null;
}
