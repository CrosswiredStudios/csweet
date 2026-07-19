using System.ComponentModel.DataAnnotations;

namespace CSweet.AgentHost.Broker;

public sealed class AgentOnboardingDeliveryOptions
{
    public const string SectionName = "CSweet:AgentOnboardingDelivery";

    [Range(1, 100)]
    public int MaximumAttempts { get; set; } = 12;
}
