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

    /// <summary>Enables or disables the account on the whole platform and signs the user out everywhere.</summary>
    Task SetActiveAsync(string userId, bool isActive, CancellationToken cancellationToken);
}
