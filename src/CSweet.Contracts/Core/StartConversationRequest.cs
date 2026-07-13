using System.ComponentModel.DataAnnotations;

namespace CSweet.Contracts.Core;

public sealed record StartConversationRequest(
    [property: Required] Guid AgentOrganizationUserId);
