namespace CSweet.Domain.Setup;

public sealed class ModelCapabilityTest
{
    public Guid Id { get; set; }
    public Guid ProviderProfileId { get; set; }
    public LlmProviderProfile? ProviderProfile { get; set; }
    public bool ConnectionSucceeded { get; set; }
    public bool ChatSucceeded { get; set; }
    public bool StreamingSucceeded { get; set; }
    public bool ToolCallingSucceeded { get; set; }
    public bool StructuredOutputSucceeded { get; set; }
    public string? FailureMessage { get; set; }
    public string? RawResult { get; set; }
    public DateTimeOffset TestedAt { get; set; }
}
