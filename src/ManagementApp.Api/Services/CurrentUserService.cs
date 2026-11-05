using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Infrastructure.Identity;
using System.Security.Claims;

namespace ManagementApp.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? ImpersonatorUserId => User?.FindFirstValue(AppClaimTypes.ImpersonatorId);

    public Guid? ImpersonationSessionId =>
        Guid.TryParse(User?.FindFirstValue(AppClaimTypes.ImpersonationSessionId), out var id) ? id : null;
}
