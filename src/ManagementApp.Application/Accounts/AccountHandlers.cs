using FluentValidation;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Auditing;

namespace ManagementApp.Application.Accounts;

/// <summary>The signed-in user's own account: their name, their password, their sessions.</summary>
public sealed record UpdateProfileCommand(string? DisplayName);

public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator() => RuleFor(v => v.DisplayName).MaximumLength(100);
}

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword);

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(v => v.CurrentPassword).NotEmpty();
        RuleFor(v => v.NewPassword).NotEmpty();
    }
}

public sealed class AccountHandler(
    IApplicationDbContext context,
    IIdentityService identity,
    ICurrentUserService currentUser,
    IAuditLogger auditLogger,
    IValidator<UpdateProfileCommand> profileValidator,
    IValidator<ChangePasswordCommand> passwordValidator)
{
    private string UserId => currentUser.RequireUserId();

    public async Task UpdateProfileAsync(UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        await profileValidator.ValidateAndThrowAsync(command, cancellationToken);
        await identity.UpdateDisplayNameAsync(UserId, command.DisplayName, cancellationToken);
        await AuditAsync(AuditActions.ProfileUpdated, cancellationToken);
    }

    /// <summary>Changes the password; every other session of the account ends. The caller renews the current one.</summary>
    public async Task ChangePasswordAsync(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        await passwordValidator.ValidateAndThrowAsync(command, cancellationToken);
        await identity.ChangePasswordAsync(UserId, command.CurrentPassword, command.NewPassword, cancellationToken);
        await AuditAsync(AuditActions.PasswordChanged, cancellationToken);
    }

    /// <summary>Ends every session and token of the account, including the current one.</summary>
    public async Task SignOutEverywhereAsync(CancellationToken cancellationToken)
    {
        await identity.SignOutEverywhereAsync(UserId, cancellationToken);
        await AuditAsync(AuditActions.SignedOutEverywhere, cancellationToken);
    }

    private async Task AuditAsync(string action, CancellationToken cancellationToken)
    {
        auditLogger.Add(new AuditEntry(action) { EntityType = "User", EntityId = UserId });
        await context.SaveChangesAsync(cancellationToken);
    }
}
