namespace ManagementApp.Application.Common.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
}

public static class CurrentUserServiceExtensions
{
    public static string RequireUserId(this ICurrentUserService currentUser) =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("The request has no authenticated user.");
}
