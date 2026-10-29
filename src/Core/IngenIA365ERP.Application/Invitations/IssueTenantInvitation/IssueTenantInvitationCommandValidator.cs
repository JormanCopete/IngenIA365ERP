using FluentValidation;
using IngenIA365ERP.Application.Invitations.Services;

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

        // Feature 017 (decisión del dueño del 2026-10-28): ninguna invitación nueva sale sin documento.
        // La regla del documento (dígitos, longitud, tipos de la cooperativa) la aplica EmisionDeInvitacion.
        InvitacionConDocumento.Reglas(this, x => x.DocumentType, x => x.DocumentNumber, x => x.DocumentNumberConfirmation,
            x => x.FirstName, x => x.LastName);
        RuleFor(x => x.OtherNames).MaximumLength(60);
        RuleFor(x => x.SecondLastName).MaximumLength(60);
        RuleFor(x => x.MobilePhone).MaximumLength(30);
    }
}
