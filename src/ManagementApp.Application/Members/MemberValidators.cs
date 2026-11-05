using FluentValidation;

namespace ManagementApp.Application.Members;

public sealed class CreateMemberCommandValidator : AbstractValidator<CreateMemberCommand>
{
    public CreateMemberCommandValidator()
    {
        RuleFor(v => v.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(v => v.DisplayName).MaximumLength(100);
        RuleFor(v => v.Role).IsInEnum();
        RuleFor(v => v.Password).NotEmpty();
    }
}

public sealed class UpdateMemberCommandValidator : AbstractValidator<UpdateMemberCommand>
{
    public UpdateMemberCommandValidator()
    {
        RuleFor(v => v.DisplayName).MaximumLength(100);
        RuleFor(v => v.Role).IsInEnum();
        RuleForEach(v => v.Permissions).IsInEnum();
    }
}

public sealed class InviteMemberCommandValidator : AbstractValidator<InviteMemberCommand>
{
    public InviteMemberCommandValidator()
    {
        RuleFor(v => v.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(v => v.Role).IsInEnum();
    }
}

public sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(v => v.Token).NotEmpty().MaximumLength(200);
        RuleFor(v => v.DisplayName).MaximumLength(100);
    }
}
