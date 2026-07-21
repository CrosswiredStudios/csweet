using CSweet.Api.Auth;
using CSweet.Application.Setup;
using CSweet.Domain.Core;
using CSweet.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Api.Security;

public sealed class AuditExecutionContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext http,
        CSweetDbContext db,
        IAuditExecutionContextAccessor accessor)
    {
        var applicationUserId = http.User.GetApplicationUserId();
        if (!applicationUserId.HasValue)
        {
            await next(http);
            return;
        }

        Guid? organizationId = Guid.TryParse(
            http.Request.RouteValues["organizationId"]?.ToString(), out var parsed) ? parsed : null;
        OrganizationUser? member = null;
        if (organizationId.HasValue)
            member = await db.CoreOrganizationUsers.AsNoTracking().SingleOrDefaultAsync(x =>
                x.OrganizationId == organizationId && x.ApplicationUserId == applicationUserId &&
                x.EmployeeType == EmployeeType.Human && x.IsActive, http.RequestAborted);

        using (accessor.Push(new AuditExecutionContext(
            organizationId,
            new AuditActor("Human", true, applicationUserId, member?.Id,
                member?.DisplayName ?? http.User.Identity?.Name ?? applicationUserId.Value.ToString("D")),
            TraceId: Guid.TryParse(http.TraceIdentifier, out var traceId) ? traceId : Guid.NewGuid())))
        {
            await next(http);
        }
    }
}
