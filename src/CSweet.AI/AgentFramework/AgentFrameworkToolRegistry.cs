using Microsoft.Extensions.AI;

namespace CSweet.AI.AgentFramework;

/// <summary>
/// Registry for tools that agents can invoke during a run.
/// Currently empty — will be populated when MCP or custom tool support is added.
/// </summary>
public sealed class AgentFrameworkToolRegistry
{
    private readonly List<AITool> _tools = new();

    public IReadOnlyList<AITool> Tools => _tools.AsReadOnly();

    /// <summary>
    /// Register a tool so agents can discover and invoke it.
    /// </summary>
    public void Register(AITool tool)
    {
        if (tool is not null)
            _tools.Add(tool);
    }

    /// <summary>
    /// Clear all registered tools. Useful for per-run isolation.
    /// </summary>
    public void Clear() => _tools.Clear();
}
