namespace ManagementApp.Application.Common.Interfaces;

public interface ICurrentUserService
{
    /// <summary>The signed-in user. While impersonating, this is the impersonated user.</summary>
    string? UserId { get; }

    /// <summary>The platform admin behind the request when a user is being impersonated; otherwise null.</summary>
    string? ImpersonatorUserId { get; }

    Guid? ImpersonationSessionId { get; }
}

public static class CurrentUserServiceExtensions
{
    public static string RequireUserId(this ICurrentUserService currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("The request has no authenticated user.");

    public static bool IsImpersonating(this ICurrentUserService currentUser) => currentUser.ImpersonatorUserId is not null;
}
