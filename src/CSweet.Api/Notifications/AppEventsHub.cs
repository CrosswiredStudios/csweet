using CSweet.Api.Auth;
using CSweet.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CSweet.Api.Notifications;

public static class AppEventGroups
{
    public static string ApplicationUser(Guid id) => $"application-user:{id:D}";
    public static string OrganizationUser(Guid id) => $"organization-user:{id:D}";
}

public sealed class AppEventsHub(CSweetDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var applicationUserId = Context.User?.GetApplicationUserId();
        if (!applicationUserId.HasValue)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, AppEventGroups.ApplicationUser(applicationUserId.Value));
        var organizationUserIds = await db.CoreOrganizationUsers.AsNoTracking()
            .Where(x => x.ApplicationUserId == applicationUserId && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(Context.ConnectionAborted);
        foreach (var organizationUserId in organizationUserIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, AppEventGroups.OrganizationUser(organizationUserId));

        await base.OnConnectedAsync();
    }
}
