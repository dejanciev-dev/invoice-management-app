using FluentValidation;
using FluentValidation.Results;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Application.Emails;
using ManagementApp.Domain.Auditing;
using Microsoft.EntityFrameworkCore;

namespace ManagementApp.Application.Accounts;

public sealed record RequestPasswordResetCommand(string Email);

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator() =>
        RuleFor(v => v.Email).NotEmpty().EmailAddress().MaximumLength(256);
}

/// <summary>
/// Emails a password reset link to the account's own address. The caller always gets the same answer, whether or not
/// the account exists, so the endpoint can't be used to find out who has an account.
/// </summary>
public sealed class RequestPasswordResetHandler(
    IApplicationDbContext context,
    IIdentityService identity,
    SystemEmailOutbox outbox,
    IAppUrls appUrls,
    IAuditLogger auditLogger,
    IValidator<RequestPasswordResetCommand> validator,
    TimeProvider timeProvider)
{
    /// <summary>At most this many reset emails per account per hour, however many addresses ask for them.</summary>
    public const int MaxEmailsPerHour = 3;

    /// <summary>How long a reset link works.</summary>
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(2);

    public async Task HandleAsync(RequestPasswordResetCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await identity.FindByEmailAsync(command.Email, cancellationToken);
        if (user is not { IsActive: true })
        {
            return;
        }

        var hourAgo = timeProvider.GetUtcNow().AddHours(-1);
        var recent = await context.SystemEmailMessages.CountAsync(
            e => e.UserId == user.Id && e.Purpose == SystemEmailPurposes.PasswordReset && e.CreatedAt > hourAgo,
            cancellationToken);
        if (recent >= MaxEmailsPerHour)
        {
            return; // protects the inbox; the earlier emails still work
        }

        var code = await identity.GeneratePasswordResetCodeAsync(user.Id, cancellationToken);
        outbox.Queue(user.Id, SystemEmailPurposes.PasswordReset, user.Email, "Reset your password",
            $"""
            Someone asked to reset the password of your account ({user.Email}).

            Choose a new password here within {LinkLifetime.TotalHours:0} hours:
            {appUrls.ResetPassword(user.Email, code)}

            If it wasn't you, ignore this email: your password stays the same.
            """);
        auditLogger.Add(new AuditEntry(AuditActions.PasswordResetRequested) { UserId = user.Id, EntityType = "User", EntityId = user.Id });
        await context.SaveChangesAsync(cancellationToken);
    }
}

public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword);

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(v => v.Email).NotEmpty().MaximumLength(256);
        RuleFor(v => v.Code).NotEmpty().MaximumLength(2000);
        RuleFor(v => v.NewPassword).NotEmpty();
    }
}

/// <summary>Sets a new password with the code from a reset email; every existing session of the account ends.</summary>
public sealed class ResetPasswordHandler(
    IApplicationDbContext context,
    IIdentityService identity,
    IAuditLogger auditLogger,
    IValidator<ResetPasswordCommand> validator)
{
    public const string InvalidLinkMessage = "This link is invalid or has expired. Ask for a new one.";

    public async Task HandleAsync(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        // An unknown or disabled account gets the same answer as a wrong code.
        var user = await identity.FindByEmailAsync(command.Email, cancellationToken);
        if (user is not { IsActive: true }
            || !await identity.ResetPasswordWithCodeAsync(user.Id, command.Code, command.NewPassword, cancellationToken))
        {
            throw new ValidationException([new ValidationFailure("code", InvalidLinkMessage)]);
        }

        auditLogger.Add(new AuditEntry(AuditActions.PasswordReset) { UserId = user.Id, EntityType = "User", EntityId = user.Id });
        await context.SaveChangesAsync(cancellationToken);
    }
}
