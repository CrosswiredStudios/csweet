using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.AgentHost.Broker;

public sealed record AgentMemoryIdentity(string TenantId, string EmployeeId);

public interface IAgentMemoryIdentityResolver
{
    Task<AgentMemoryIdentity?> ResolveAsync(AgentSession session, CancellationToken cancellationToken);
}

public sealed class AgentMemoryIdentityResolver(CSweetDbContext db) : IAgentMemoryIdentityResolver
{
    public async Task<AgentMemoryIdentity?> ResolveAsync(AgentSession session, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(session.InstallationId, out var installationId)) return null;
        var matches = await db.CoreOrganizationUsers
            .Where(x => x.AgentInstallationId == installationId && x.EmployeeType == EmployeeType.Agent)
            .Select(x => new AgentMemoryIdentity(x.OrganizationId.ToString(), x.Id.ToString()))
            .Take(2)
            .ToListAsync(cancellationToken);
        return matches.Count == 1 ? matches[0] : null;
    }
}
