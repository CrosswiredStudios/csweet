namespace CSweet.Agents.PersonalAssistant;

public sealed record UserMessageReceived(
    Guid ProviderProfileId,
    string ConversationId,
    string UserId,
    string Message,
    IReadOnlyDictionary<string, string>? Context);

public sealed record AssistantCapabilityInput(
    Guid ProviderProfileId,
    string ConversationId,
    string Prompt,
    IReadOnlyDictionary<string, string>? Context);

public sealed record AssistantResponseCreated(
    string ConversationId,
    string Response,
    IReadOnlyList<ProposedAction> ProposedActions,
    DateTimeOffset CreatedAt);

public sealed record ProposedAction(
    string ActionType,
    string Summary,
    string ParametersJson,
    bool RequiresApproval);

public sealed record AssistantResponseChunk(
    string ConversationId,
    int Sequence,
    string Delta,
    bool IsFinal);
