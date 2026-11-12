namespace ManagementApp.Domain.Auditing;

public static class AuditActions
{
    public const string LoginSucceeded = "auth.login";
    public const string LoginFailed = "auth.login_failed";
    public const string Logout = "auth.logout";
    public const string SignedUp = "auth.signup";

    public const string TenantCreated = "tenant.created";
    public const string TenantUpdated = "tenant.updated";
    public const string TenantSettingsUpdated = "tenant.settings_updated";
    public const string TenantStatusChanged = "tenant.status_changed";
    public const string TenantSubscriptionChanged = "tenant.subscription_changed";
    public const string TenantDeleted = "tenant.deleted";
    public const string TenantLogoChanged = "tenant.logo_changed";

    public const string UserCreated = "user.created";
    public const string UserInvited = "user.invited";
    public const string InvitationRevoked = "user.invitation_revoked";
    public const string InvitationAccepted = "user.invitation_accepted";
    public const string UserUpdated = "user.updated";
    public const string UserRoleChanged = "user.role_changed";
    public const string UserPermissionsChanged = "user.permissions_changed";
    public const string UserActivated = "user.activated";
    public const string UserDeactivated = "user.deactivated";
    public const string UserPasswordReset = "user.password_reset";
    public const string UserRemoved = "user.removed";
    public const string PlatformUserStatusChanged = "platform.user_status_changed";

    public const string InvoiceCreated = "invoice.created";
    public const string InvoiceUpdated = "invoice.updated";
    public const string InvoiceDeleted = "invoice.deleted";
    public const string InvoiceIssued = "invoice.issued";
    public const string InvoiceVoided = "invoice.voided";
    /// <summary>Older entries, from before payments were recorded one by one.</summary>
    public const string InvoicePaymentChanged = "invoice.payment_changed";
    public const string PaymentRecorded = "payment.recorded";
    public const string PaymentDeleted = "payment.deleted";
    public const string CreditNoteIssued = "credit_note.issued";
    public const string EmailRetried = "email.retried";
    public const string InvoicesExported = "invoice.exported";

    public const string CustomerCreated = "customer.created";
    public const string CustomerUpdated = "customer.updated";
    public const string CustomerDeleted = "customer.deleted";

    public const string ProductCreated = "product.created";
    public const string ProductUpdated = "product.updated";
    public const string ProductDeleted = "product.deleted";

    public const string ImpersonationStarted = "impersonation.started";
    public const string ImpersonationEnded = "impersonation.ended";
    public const string ImpersonatedRequest = "impersonation.request";

    public const string OverdueReminderSent = "notification.overdue_reminder";
    public const string SubscriptionExpired = "subscription.expired";
}
