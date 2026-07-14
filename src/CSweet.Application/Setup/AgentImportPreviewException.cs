namespace CSweet.Application.Setup;

public sealed class AgentImportPreviewException : Exception
{
    public AgentImportPreviewException(string message)
        : base(message)
    {
    }

    public AgentImportPreviewException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}