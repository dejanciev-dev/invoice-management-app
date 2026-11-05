namespace ManagementApp.Infrastructure.Identity;

/// <summary>Claims added to the session cookie while a platform admin impersonates a user.</summary>
public static class AppClaimTypes
{
    public const string ImpersonatorId = "mgmt:impersonator_id";
    public const string ImpersonationSessionId = "mgmt:impersonation_session_id";

    public static readonly IReadOnlyList<string> Impersonation = [ImpersonatorId, ImpersonationSessionId];
}
