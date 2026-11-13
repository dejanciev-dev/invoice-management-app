using FluentValidation;
using ManagementApp.Application.Tenants;
using ManagementApp.Domain.Tenancy;

namespace ManagementApp.Application.Platform;

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(v => v.Profile).NotNull().SetValidator(new TenantProfileValidator());
        RuleFor(v => v.PlanCode).Must(code => SubscriptionPlans.Find(code) is not null).WithMessage("Unknown plan.");
        RuleFor(v => v.AdminEmail).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public sealed class ChangeTenantStatusCommandValidator : AbstractValidator<ChangeTenantStatusCommand>
{
    public ChangeTenantStatusCommandValidator()
    {
        RuleFor(v => v.Status).IsInEnum();
        RuleFor(v => v.Reason).MaximumLength(500);
        RuleFor(v => v.Reason).NotEmpty().When(v => v.Status == TenantStatus.Suspended)
            .WithMessage("Give a reason for suspending the tenant.");
    }
}

public sealed class UpdateSubscriptionCommandValidator : AbstractValidator<UpdateSubscriptionCommand>
{
    public UpdateSubscriptionCommandValidator()
    {
        RuleFor(v => v.PlanCode).Must(code => SubscriptionPlans.Find(code) is not null).WithMessage("Unknown plan.");
        RuleFor(v => v.Status).IsInEnum();
        RuleFor(v => v.BillingCycle).IsInEnum();
        RuleFor(v => v.CustomMaxUsers).GreaterThan(0);
        RuleFor(v => v.CustomMaxInvoicesPerMonth).GreaterThan(0);
        RuleFor(v => v.Note).MaximumLength(SubscriptionEvent.NoteMaxLength);
    }
}

public sealed class StartImpersonationCommandValidator : AbstractValidator<StartImpersonationCommand>
{
    public StartImpersonationCommandValidator()
    {
        RuleFor(v => v.UserId).NotEmpty();
        RuleFor(v => v.TenantId).NotEmpty();
        RuleFor(v => v.Reason).MaximumLength(500);
    }
}
