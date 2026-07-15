using Microsoft.AspNetCore.Identity;

namespace CSweet.Infrastructure.Auth;

public sealed class RootUserConfirmation : IUserConfirmation<ApplicationUser>
{
    public Task<bool> IsConfirmedAsync(UserManager<ApplicationUser> manager, ApplicationUser user) =>
        Task.FromResult(user.IsInitialAdministrator || user.EmailConfirmed);
}
