namespace CSweet.Application.Setup;

public sealed class AgentInstallationException : Exception
{
    public AgentInstallationException(string message)
        : base(message)
    {
    }
}