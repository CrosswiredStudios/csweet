namespace CSweet.Application.Setup;

public sealed class AgentBuildException : Exception
{
    public string? StepKey { get; }

    public AgentBuildException(string message)
        : base(message)
    {
    }

    public AgentBuildException(string message, string stepKey)
        : base(message)
    {
        StepKey = stepKey;
    }

    public AgentBuildException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public AgentBuildException(string message, string stepKey, Exception innerException)
        : base(message, innerException)
    {
        StepKey = stepKey;
    }
}
