using FluentValidation;

namespace IngenIA365ERP.Application.Invitations.IssueTenantInvitation;

public sealed class IssueTenantInvitationCommandValidator : AbstractValidator<IssueTenantInvitationCommand>
{
    public IssueTenantInvitationCommandValidator()
    {
        RuleFor(x => x.TenantPublicId)
            .NotEmpty().WithMessage("El identificador de la empresa es obligatorio.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo del destinatario es obligatorio.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.")
            .MaximumLength(256);
    }
}
