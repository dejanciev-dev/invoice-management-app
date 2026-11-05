using ManagementApp.Application.Auditing;
using ManagementApp.Application.Reports;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;

namespace ManagementApp.Application.Platform;

public sealed record PlatformDashboardDto(
    int TotalTenants,
    int ActiveTenants,
    int InactiveTenants,
    int SuspendedTenants,
    int TotalUsers,
    int TotalInvoices,
    int InvoicesLast30Days,
    int TenantsCreatedLast30Days,
    IReadOnlyList<TenantListItemDto> RecentTenants,
    IReadOnlyList<AuditLogDto> RecentActivity);

public sealed record TenantListItemDto(
    Guid Id,
    string Name,
    string? Email,
    string? Country,
    TenantStatus Status,
    string PlanCode,
    SubscriptionStatus SubscriptionStatus,
    int UserCount,
    int InvoiceCount,
    DateTimeOffset Created);

public sealed record GetTenantsQuery(string? Search = null, TenantStatus? Status = null, int Page = 1, int PageSize = 25);

public sealed record TenantStatsDto(
    int UserCount,
    int ActiveUserCount,
    int InvoiceCount,
    int InvoicesThisMonth,
    IReadOnlyList<CurrencyTotalsDto> Totals,
    DateTimeOffset? LastActivityAt);

public sealed record TenantDetailsDto(
    Guid Id,
    TenantStatus Status,
    string? StatusReason,
    DateTimeOffset? StatusChangedAt,
    DateTimeOffset Created,
    bool HasLogo,
    TenantSettingsDto Settings,
    SubscriptionDto Subscription,
    TenantStatsDto Stats);

/// <summary>
/// Creates a tenant and its first admin. An existing account is added as admin; otherwise an account is created
/// with <see cref="AdminPassword"/>, or an invitation is issued when no password is given.
/// </summary>
public sealed record CreateTenantCommand(TenantProfileDto Profile, string PlanCode, string AdminEmail, string? AdminPassword);

public sealed record CreatedTenantDto(Guid Id, string? InvitationToken);

public sealed record ChangeTenantStatusCommand(TenantStatus Status, string? Reason);

public sealed record UpdateSubscriptionCommand(
    string PlanCode,
    SubscriptionStatus Status,
    BillingCycle BillingCycle,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? CurrentPeriodEndsAt,
    // Leave null to use the plan's limits.
    int? CustomMaxUsers,
    int? CustomMaxInvoicesPerMonth);

public sealed record PlatformUserDto(
    string Id,
    string Email,
    string? DisplayName,
    bool IsActive,
    bool IsPlatformAdmin,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    IReadOnlyList<PlatformUserTenantDto> Tenants);

public sealed record PlatformUserTenantDto(Guid TenantId, string TenantName, TenantRole Role, bool IsActive);

public sealed record SetUserStatusCommand(bool IsActive);

public sealed record StartImpersonationCommand(string UserId, Guid TenantId, string? Reason);
