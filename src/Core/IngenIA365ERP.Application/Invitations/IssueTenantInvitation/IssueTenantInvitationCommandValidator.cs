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

        // Feature 017: con documento, también tipo, confirmación y nombre; la regla del documento
        // (dígitos, longitud, tipos de la cooperativa) la aplica EmisionDeInvitacion.
        When(x => !string.IsNullOrWhiteSpace(x.DocumentType) || !string.IsNullOrWhiteSpace(x.DocumentNumber), () =>
        {
            RuleFor(x => x.DocumentType).NotEmpty().WithMessage("El tipo de documento es obligatorio.").MaximumLength(3);
            RuleFor(x => x.DocumentNumber).NotEmpty().WithMessage("El número de documento es obligatorio.").MaximumLength(30);
            RuleFor(x => x.DocumentNumberConfirmation).NotEmpty().WithMessage("Digite otra vez el número de documento.");
            RuleFor(x => x.FirstName).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(60);
            RuleFor(x => x.LastName).NotEmpty().WithMessage("El apellido es obligatorio.").MaximumLength(60);
        });
        RuleFor(x => x.OtherNames).MaximumLength(60);
        RuleFor(x => x.SecondLastName).MaximumLength(60);
        RuleFor(x => x.MobilePhone).MaximumLength(30);
    }
}
