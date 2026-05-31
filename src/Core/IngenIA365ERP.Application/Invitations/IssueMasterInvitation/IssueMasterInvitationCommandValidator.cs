using FluentValidation;

namespace IngenIA365ERP.Application.Invitations.IssueMasterInvitation;

public sealed class IssueMasterInvitationCommandValidator : AbstractValidator<IssueMasterInvitationCommand>
{
    public IssueMasterInvitationCommandValidator()
    {
        RuleFor(x => x.TenantPublicId)
            .NotEmpty().WithMessage("El identificador de la empresa es obligatorio.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo del destinatario es obligatorio.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.")
            .MaximumLength(256);

        // InviteAsTenantAdmin no requiere validación intrínseca — la
        // autorización (solo master puede emitir) la verifica el handler.
    }
}
