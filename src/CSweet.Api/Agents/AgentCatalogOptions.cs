namespace CSweet.Api.Agents;

public sealed class AgentCatalogOptions
{
    public const string SectionName = "CSweet:Agents";

    public List<string> ManifestSearchPaths { get; set; } = [];
}
