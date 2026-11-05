using FluentValidation;
using ManagementApp.Application.Common.Interfaces;
using ManagementApp.Domain.Auditing;
using ManagementApp.Domain.Tenancy;

namespace ManagementApp.Application.Accounts;

public sealed record SignupCommand(string CompanyName, string Email, string Password, string? DisplayName = null);

public sealed class SignupCommandValidator : AbstractValidator<SignupCommand>
{
    public SignupCommandValidator()
    {
        RuleFor(v => v.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(v => v.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(v => v.Password).NotEmpty();
        RuleFor(v => v.DisplayName).MaximumLength(100);
    }
}

/// <summary>Self-service sign-up: creates the account and a new company on a trial, with the user as its admin.</summary>
public sealed class SignupHandler(
    IApplicationDbContext context,
    IIdentityService identity,
    IAuditLogger auditLogger,
    IValidator<SignupCommand> validator,
    TimeProvider timeProvider)
{
    public async Task<Guid> HandleAsync(SignupCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var tenant = new Tenant
        {
            Name = command.CompanyName.Trim(),
            Email = command.Email.Trim(),
            Subscription = TenantSubscription.StartTrial(now)
        };

        await context.ExecuteInTransactionAsync(async () =>
        {
            var userId = await identity.CreateUserAsync(command.Email.Trim(), command.DisplayName, command.Password, cancellationToken);

            context.Tenants.Add(tenant);
            context.TenantMemberships.Add(TenantMembership.Create(tenant.Id, userId, TenantRole.TenantAdmin));
            auditLogger.Add(new AuditEntry(AuditActions.SignedUp)
            {
                TenantId = tenant.Id,
                UserId = userId,
                EntityType = nameof(Tenant),
                EntityId = tenant.Id.ToString(),
                Details = new { tenant.Name }
            });
            await context.SaveChangesAsync(cancellationToken);
        }, cancellationToken);

        return tenant.Id;
    }
}
