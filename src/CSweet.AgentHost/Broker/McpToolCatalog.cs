using System.Text.Json;
using CSweet.Agent.SDK;
using CSweet.Contracts.Communications;
using CSweet.Contracts.Core;

namespace CSweet.AgentHost.Broker;

public enum McpToolExecutionPolicy
{
    ReadOnly,
    AdvisoryWrite,
    ApprovalCreating,
    PlatformOnly
}

/// <summary>
/// Controls discovery separately from execution risk. Global tools are available to every
/// authenticated, active installation without a manifest-requested capability grant.
/// </summary>
public enum McpToolAvailability
{
    GrantRequired,
    Global,
    PlatformOnly
}

public sealed record McpToolDescriptor(
    string Capability,
    string Name,
    string Description,
    JsonElement InputSchema,
    JsonElement? OutputSchema,
    McpToolExecutionPolicy ExecutionPolicy,
    McpToolAvailability Availability = McpToolAvailability.GrantRequired,
    bool ModelVisible = true);

public sealed class McpToolCatalog
{
    private static readonly JsonElement EmptyInput = Schema("""
        { "type": "object", "properties": {}, "additionalProperties": false }
        """);

    private static readonly IReadOnlyList<McpToolDescriptor> Tools =
    [
        Read(PlatformCapabilities.BusinessProfileRead, "read_business_profile",
            "Read the authoritative business profile for this organization."),
        Write(PlatformCapabilities.BusinessProfileUpdateExplicit, "update_explicit_business_profile",
            "Save low-risk facts explicitly stated by the owner, with conversation and message provenance."),
        Approval(PlatformCapabilities.BusinessProfileProposeUpdate, "propose_business_profile_update",
            "Propose inferred or sensitive business-profile changes for owner approval."),
        Read(PlatformCapabilities.OrganizationSnapshotRead, "read_organization_snapshot",
            "Read current staff, roles, reporting lines, objectives, workstreams, workers, and operating signals."),
        Read(PlatformCapabilities.BusinessPatternSearch, "search_business_patterns",
            "Find stage-appropriate operating patterns from broker-approved sources."),
        Approval(PlatformCapabilities.WorkstreamPlanPropose, "propose_workstream_plan",
            "Propose a managed workstream with one accountable manager."),
        Read(PlatformCapabilities.WorkforceSearch, "search_workforce",
            "Search current staff, installed agents, and connected workforce catalogs in platform policy order."),
        Approval(PlatformCapabilities.WorkforcePlanPropose, "propose_workforce_plan",
            "Propose a workforce plan without installing, hiring, contacting, or spending."),
        Read(PlatformCapabilities.FinanceProfileRead, "read_finance_profile",
            "Read authoritative financial goals and workforce controls."),
        Approval(PlatformCapabilities.FinanceProfileProposeUpdate, "propose_finance_profile_update",
            "Propose changes to financial goals or controls for owner approval."),
        Write(PlatformCapabilities.BudgetEvaluate, "evaluate_budget",
            "Evaluate a proposed cost against enforceable budgets; reservations remain platform controlled."),
        Approval(PlatformCapabilities.ApprovalPropose, "propose_approval",
            "Create a durable, separately gated action proposal."),
        Read(PlatformCapabilities.ManagementCycleRead, "read_management_cycle",
            "Read management cadence, executive briefing schedule, and quiet hours."),
        GlobalWrite(CommunicationHubCapabilities.AskUser, "ask_user",
            "Ask the user one structured multiple-choice question with two to four mutually exclusive options. Put the recommended option first. The UI automatically adds Something else with a free-text response."),
        Write(HiringCapabilities.UpsertRecommendation, "upsert_hiring_recommendation",
            "Maintain the ranked HR hiring backlog using opaque candidate references returned by search_workforce."),
        Approval(HiringCapabilities.StageWorkflow, "stage_hiring_workflow",
            "Stage a combined install-and-hire proposal for explicit organization-owner approval. This does not install or hire directly.")
    ];

    public IReadOnlyList<McpToolDescriptor> List(IReadOnlySet<string> grantedCapabilities) =>
        Tools.Where(tool => tool.ModelVisible &&
                             tool.Availability != McpToolAvailability.PlatformOnly &&
                             (tool.Availability == McpToolAvailability.Global ||
                              grantedCapabilities.Contains(tool.Capability)))
            .OrderBy(tool => tool.Name, StringComparer.Ordinal)
            .ToList();

    public McpToolDescriptor? Find(string name, IReadOnlySet<string> grantedCapabilities) =>
        List(grantedCapabilities).SingleOrDefault(tool => string.Equals(tool.Name, name, StringComparison.Ordinal));

    public static IReadOnlySet<string> GlobalCapabilities { get; } = Tools
        .Where(tool => tool.Availability == McpToolAvailability.Global)
        .Select(tool => tool.Capability)
        .ToHashSet(StringComparer.Ordinal);

    public static bool IsGlobalCapability(string capability) => GlobalCapabilities.Contains(capability);

    private static McpToolDescriptor Read(string capability, string name, string description) =>
        new(capability, name, description, InputFor(capability), null, McpToolExecutionPolicy.ReadOnly);

    private static McpToolDescriptor Write(string capability, string name, string description) =>
        new(capability, name, description, InputFor(capability), null, McpToolExecutionPolicy.AdvisoryWrite);

    private static McpToolDescriptor GlobalWrite(string capability, string name, string description) =>
        new(capability, name, description, InputFor(capability), null, McpToolExecutionPolicy.AdvisoryWrite,
            McpToolAvailability.Global);

    private static McpToolDescriptor Approval(string capability, string name, string description) =>
        new(capability, name, description, InputFor(capability), null, McpToolExecutionPolicy.ApprovalCreating);

    private static JsonElement InputFor(string capability) => capability switch
    {
        PlatformCapabilities.BusinessProfileRead or
        PlatformCapabilities.OrganizationSnapshotRead or
        PlatformCapabilities.FinanceProfileRead or
        PlatformCapabilities.ManagementCycleRead => EmptyInput,
        PlatformCapabilities.BusinessPatternSearch => Schema("""
            {"type":"object","properties":{"businessType":{"type":["string","null"]},"lifecycleStage":{"type":["string","null"]},"jurisdictions":{"type":["array","null"],"items":{"type":"string"}},"maximumResults":{"type":"integer","minimum":1,"maximum":10}},"additionalProperties":false}
            """),
        PlatformCapabilities.WorkforceSearch => Schema("""
            {"type":"object","required":["requiredCapabilities","humanRequired"],"properties":{"requiredCapabilities":{"type":"array","items":{"type":"string"},"minItems":1},"requiredCredentials":{"type":["array","null"],"items":{"type":"string"}},"neededBy":{"type":["string","null"],"format":"date-time"},"maximumBudget":{"type":["number","null"],"minimum":0},"currency":{"type":["string","null"]},"humanRequired":{"type":"boolean"},"workstreamId":{"type":["string","null"]},"maximumResults":{"type":"integer","minimum":1,"maximum":25}},"additionalProperties":false}
            """),
        PlatformCapabilities.BusinessProfileUpdateExplicit => Schema("""
            {"type":"object","required":["expectedRevision","conversationId","messageId","userId","changes","idempotencyKey"],"properties":{"expectedRevision":{"type":"integer"},"conversationId":{"type":"string"},"messageId":{"type":"string"},"userId":{"type":"string"},"changes":{"type":"object"},"idempotencyKey":{"type":"string"}},"additionalProperties":false}
            """),
        PlatformCapabilities.BudgetEvaluate => Schema("""
            {"type":"object","required":["scopeType","amount","currency","purpose","reserve","idempotencyKey"],"properties":{"scopeType":{"type":"string"},"scopeId":{"type":["string","null"]},"amount":{"type":"number","minimum":0},"currency":{"type":"string"},"purpose":{"type":"string"},"reserve":{"type":"boolean"},"idempotencyKey":{"type":"string"}},"additionalProperties":false}
            """),
        CommunicationHubCapabilities.AskUser => Schema("""
            {"type":"object","required":["conversationId","chatTurnId","prompt","options","recommendedOptionId","idempotencyKey"],"properties":{"conversationId":{"type":"string","format":"uuid"},"chatTurnId":{"type":"string","format":"uuid"},"prompt":{"type":"string","minLength":1,"maxLength":2048},"options":{"type":"array","minItems":2,"maxItems":4,"items":{"type":"object","required":["id","label"],"properties":{"id":{"type":"string","minLength":1,"maxLength":80},"label":{"type":"string","minLength":1,"maxLength":160},"description":{"type":["string","null"],"maxLength":500}},"additionalProperties":false}},"recommendedOptionId":{"type":"string","minLength":1,"maxLength":80},"idempotencyKey":{"type":"string","minLength":1,"maxLength":160}},"additionalProperties":false}
            """),
        HiringCapabilities.UpsertRecommendation => Schema("""
            {"type":"object","required":["title","objective","candidateReferences","recommendedCandidateReference","idempotencyKey"],"properties":{"title":{"type":"string","minLength":1,"maxLength":256},"objective":{"type":"string","minLength":1,"maxLength":2048},"workstreamId":{"type":["string","null"],"format":"uuid"},"candidateReferences":{"type":"array","minItems":1,"maxItems":3,"items":{"type":"string"}},"recommendedCandidateReference":{"type":"string"},"idempotencyKey":{"type":"string","minLength":1,"maxLength":160}},"additionalProperties":false}
            """),
        HiringCapabilities.StageWorkflow => Schema("""
            {"type":"object","required":["recommendationId","candidateReference","roleTitle","idempotencyKey"],"properties":{"recommendationId":{"type":"string","format":"uuid"},"candidateReference":{"type":"string"},"roleTitle":{"type":"string","minLength":1,"maxLength":160},"reportsToOrganizationUserId":{"type":["string","null"],"format":"uuid"},"requiredGrants":{"type":["array","null"],"items":{"type":"string"}},"idempotencyKey":{"type":"string","minLength":1,"maxLength":160}},"additionalProperties":false}
            """),
        _ => Schema("""
            {"type":"object","description":"Arguments are validated by the broker capability handler."}
            """)
    };

    private static JsonElement Schema(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
