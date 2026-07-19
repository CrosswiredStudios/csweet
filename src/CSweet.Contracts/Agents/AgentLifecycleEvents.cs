namespace CSweet.Contracts.Agents;

public static class AgentLifecycleEvents
{
    public const string Onboarded = "com.csweet.agent.onboarded.v1";
}

public static class AgentLifecycleCapabilities
{
    public const string CompleteOnboarding = "agent.onboarding.complete.v1";
}

public sealed record AgentOnboardedEvent(
    Guid OrganizationId,
    Guid AgentOrganizationUserId,
    Guid HiringOrganizationUserId,
    Guid ConversationId,
    DateTimeOffset OccurredAt);

public sealed record CompleteAgentOnboardingRequest(Guid EventId);

public sealed record CompleteAgentOnboardingResponse(bool Completed, DateTimeOffset CompletedAt);
