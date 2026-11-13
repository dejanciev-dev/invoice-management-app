namespace ManagementApp.Application.Common.Interfaces;

public sealed record UserSummary(
    string Id,
    string Email,
    string? DisplayName,
    bool IsActive,
    bool IsPlatformAdmin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>User accounts. They are global: a user can be a member of several tenants.</summary>
public interface IIdentityService
{
    Task<UserSummary?> FindByIdAsync(string userId, CancellationToken cancellationToken);

    Task<UserSummary?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, UserSummary>> GetUsersAsync(IEnumerable<string> userIds, CancellationToken cancellationToken);

    Task<(IReadOnlyList<UserSummary> Users, int Total)> SearchUsersAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountUsersAsync(CancellationToken cancellationToken);

    /// <summary>Creates a user; throws a validation exception when Identity rejects the email or password.</summary>
    Task<string> CreateUserAsync(string email, string? displayName, string password, CancellationToken cancellationToken);

    Task UpdateDisplayNameAsync(string userId, string? displayName, CancellationToken cancellationToken);

    /// <summary>Sets a new password and signs the user out everywhere.</summary>
    Task ResetPasswordAsync(string userId, string newPassword, CancellationToken cancellationToken);

    /// <summary>A single-use, time-limited code that lets the account's owner choose a new password; safe in a URL.</summary>
    Task<string> GeneratePasswordResetCodeAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Sets a new password with a code from <see cref="GeneratePasswordResetCodeAsync"/> and signs the user out
    /// everywhere. False when the code is wrong, used or expired; throws a validation exception for a weak password.
    /// </summary>
    Task<bool> ResetPasswordWithCodeAsync(string userId, string code, string newPassword, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the password after checking the current one, and signs the user out everywhere else. Throws a
    /// validation exception when the current password is wrong or the new one is too weak.
    /// </summary>
    Task ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken);

    /// <summary>Ends every session and token of the account, on every device.</summary>
    Task SignOutEverywhereAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Enables or disables the account on the whole platform and signs the user out everywhere.</summary>
    Task SetActiveAsync(string userId, bool isActive, CancellationToken cancellationToken);
}
