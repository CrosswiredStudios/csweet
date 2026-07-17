namespace CSweet.Contracts.Agents;

public sealed record SetPluginSecretRequest(string Value);
public sealed record GrantPluginOrganizationRequest(Guid OrganizationId);
