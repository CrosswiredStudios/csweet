using CSweet.Contracts.Llm;

namespace CSweet.AI.AgentFramework;

/// <summary>
/// Factory for resolving agent profiles by key.
/// Currently hard-coded — will evolve into a dynamic registry when marketplace agents arrive.
/// </summary>
public sealed class AgentFrameworkAgentFactory
{
    private readonly Dictionary<string, AgentProfileDescriptor> _profiles = new();

    public AgentFrameworkAgentFactory()
    {
        Register(BusinessStrategistAgentProfile.Descriptor);
    }

    /// <summary>
    /// Register a custom agent profile.
    /// </summary>
    public void Register(AgentProfileDescriptor descriptor)
    {
        _profiles[descriptor.AgentKey] = descriptor;
    }

    /// <summary>
    /// Resolve an agent profile by its key. Returns null if not found.
    /// </summary>
    public AgentProfileDescriptor? Resolve(string agentKey)
    {
        return _profiles.TryGetValue(agentKey, out var descriptor) ? descriptor : null;
    }

    /// <summary>
    /// Get all registered agent profiles.
    /// </summary>
    public IReadOnlyList<AgentProfileDescriptor> AllProfiles => _profiles.Values.ToList();
}

