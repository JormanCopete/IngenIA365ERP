using FluentValidation;
using IngenIA365ERP.Application.Invitations.Services;

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

        // Feature 017 (decisión del dueño del 2026-10-28): también la del maestro lleva documento.
        InvitacionConDocumento.Reglas(this, x => x.DocumentType, x => x.DocumentNumber, x => x.DocumentNumberConfirmation,
            x => x.FirstName, x => x.LastName);
        RuleFor(x => x.OtherNames).MaximumLength(60);
        RuleFor(x => x.SecondLastName).MaximumLength(60);
        RuleFor(x => x.MobilePhone).MaximumLength(30);

        // InviteAsTenantAdmin no requiere validación intrínseca — la
        // autorización (solo master puede emitir) la verifica el handler.
    }
}
