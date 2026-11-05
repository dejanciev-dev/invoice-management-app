using FluentValidation;
using FluentValidation.Results;
using ManagementApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Infrastructure.Identity;

public sealed class IdentityService(UserManager<ApplicationUser> userManager, TimeProvider timeProvider) : IIdentityService
{
    public async Task<UserSummary?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        ToSummary(await userManager.FindByIdAsync(userId));

    public async Task<UserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        ToSummary(await userManager.FindByEmailAsync(email.Trim()));

    public async Task<IReadOnlyDictionary<string, UserSummary>> GetUsersAsync(
        IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, UserSummary>();
        }

        var users = await userManager.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToListAsync(cancellationToken);
        return users.ToDictionary(u => u.Id, u => ToSummary(u)!);
    }

    public async Task<(IReadOnlyList<UserSummary> Users, int Total)> SearchUsersAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var users = userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            users = users.Where(u => (u.Email != null && u.Email.Contains(term)) || (u.DisplayName != null && u.DisplayName.Contains(term)));
        }

        var total = await users.CountAsync(cancellationToken);
        var items = await users.OrderBy(u => u.Email).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items.Select(u => ToSummary(u)!).ToList(), total);
    }

    public Task<int> CountUsersAsync(CancellationToken cancellationToken) => userManager.Users.CountAsync(cancellationToken);

    public async Task<string> CreateUserAsync(string email, string? displayName, string password, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            CreatedAt = timeProvider.GetUtcNow()
        };

        ThrowIfFailed(await userManager.CreateAsync(user, password));
        return user.Id;
    }

    public async Task UpdateDisplayNameAsync(string userId, string? displayName, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);
        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        ThrowIfFailed(await userManager.UpdateAsync(user));
    }

    public async Task ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        ThrowIfFailed(await userManager.ResetPasswordAsync(user, token, newPassword), "newPassword");
    }

    public async Task SetActiveAsync(string userId, bool isActive, CancellationToken cancellationToken)
    {
        var user = await RequireAsync(userId);
        user.IsActive = isActive;
        ThrowIfFailed(await userManager.UpdateAsync(user));
        ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));
    }

    private async Task<ApplicationUser> RequireAsync(string userId) =>
        await userManager.FindByIdAsync(userId)
            ?? throw new Application.Common.Exceptions.NotFoundException("User", userId);

    private static void ThrowIfFailed(IdentityResult result, string passwordProperty = "password")
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new ValidationException(result.Errors.Select(e => new ValidationFailure(
            e.Code.Contains("Password", StringComparison.Ordinal) ? passwordProperty : "email",
            e.Description)));
    }

    private static UserSummary? ToSummary(ApplicationUser? user) => user is null
        ? null
        : new UserSummary(user.Id, user.Email ?? user.UserName ?? user.Id, user.DisplayName, user.IsActive,
            user.IsPlatformAdmin, user.CreatedAt, user.LastLoginAt);
}
