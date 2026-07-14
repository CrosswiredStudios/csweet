namespace CSweet.Application.Setup;

public sealed class AgentBuildException : Exception
{
    public AgentBuildException(string message)
        : base(message)
    {
    }

    public AgentBuildException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
