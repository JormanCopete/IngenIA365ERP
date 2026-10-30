using FluentValidation;
using IngenIA365ERP.Application.Invitations.Services;

namespace IngenIA365ERP.Application.Saas.RegisterTenantWithAdmin;

public sealed class RegisterTenantWithAdminCommandValidator : AbstractValidator<RegisterTenantWithAdminCommand>
{
    public RegisterTenantWithAdminCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        // La misma regla que el alta sin administrador: un guion u otro signo pasaba aquí y el alta fallaba después,
        // al aceptar la invitación, con un 500 (encontrado por las e2e de la feature 013, 2026-10-08).
        RuleFor(x => x.SchemaName)
            .NotEmpty().MaximumLength(50)
            .Matches("^[a-z][a-z0-9_]*$")
                .WithMessage("El schema debe estar en minúsculas y solo aceptar letras, dígitos y guion bajo.");
        RuleFor(x => x.Nit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.LegalName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.FirstAdminEmail)
            .NotEmpty().WithMessage("El correo del primer admin es obligatorio.")
            .EmailAddress().WithMessage("Correo del admin no válido.")
            .MaximumLength(256);
        // Feature 017 (decisión del dueño del 2026-10-29): el primer administrador también llega con su documento, como toda invitación nueva.
        InvitacionConDocumento.Reglas(this, x => x.FirstAdminDocumentType, x => x.FirstAdminDocumentNumber,
            x => x.FirstAdminDocumentNumberConfirmation, x => x.FirstAdminFirstName, x => x.FirstAdminLastName);
        RuleFor(x => x.MaxUsers).GreaterThan(0);
        RuleFor(x => x.StorageLimitMb).GreaterThan(0);
    }
}
