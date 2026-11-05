using Microsoft.AspNetCore.Identity;

namespace ManagementApp.Infrastructure.Identity;

/// <summary>
/// Identity asks this before every sign-in (with <c>SignIn.RequireConfirmedAccount</c> on). Disabled accounts are
/// refused, so a platform admin can lock someone out without touching their password.
/// </summary>
public sealed class ActiveUserConfirmation : IUserConfirmation<ApplicationUser>
{
    public Task<bool> IsConfirmedAsync(UserManager<ApplicationUser> manager, ApplicationUser user) => Task.FromResult(user.IsActive);
}
