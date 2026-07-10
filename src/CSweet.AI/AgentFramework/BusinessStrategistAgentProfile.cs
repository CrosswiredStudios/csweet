using CSweet.Contracts.Llm;

namespace CSweet.AI.AgentFramework;

public static class BusinessStrategistAgentProfile
{
    public const string AgentKey = "business-strategist";

    public const string DisplayName = "Business Strategist";

    public const string Description =
        "Produces practical, early-stage business planning artifacts from organization context.";

    public static readonly string SystemPrompt = """
You are a practical business strategy agent inside C-Sweet.
Your job is to produce clear, actionable plans for a small business owner.
Avoid hype. Make assumptions explicit. Prefer concrete next actions.
When the user provides incomplete context, proceed with reasonable assumptions and list them.
""";

    public static readonly IReadOnlyList<string> Capabilities = new[]
    {
        "business-planning",
        "operating-plan",
        "task-breakdown",
        "risk-identification"
    };

    public static AgentProfileDescriptor Descriptor => new(
        AgentKey: AgentKey,
        DisplayName: DisplayName,
        Description: Description,
        SystemPrompt: SystemPrompt,
        Capabilities: Capabilities);
}
