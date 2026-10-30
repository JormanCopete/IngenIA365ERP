using System.Linq.Expressions;
using FluentValidation;

namespace IngenIA365ERP.Application.Invitations.Services;

/// <summary>
/// Feature 017 (decisión del dueño del 2026-10-28): toda invitación nueva, de la cooperativa o del maestro, exige
/// tipo y número de documento digitado dos veces, nombre y apellido. Las invitaciones ya enviadas sin documento
/// siguen sirviendo: esto se mira al emitir, no al aceptar.
/// </summary>
public static class InvitacionConDocumento
{
    public static void Reglas<T>(
        AbstractValidator<T> v,
        Expression<Func<T, string?>> tipo,
        Expression<Func<T, string?>> numero,
        Expression<Func<T, string?>> confirmacion,
        Expression<Func<T, string?>> nombre,
        Expression<Func<T, string?>> apellido)
    {
        v.RuleFor(tipo).NotEmpty().WithErrorCode("Invitation.DocumentRequired")
            .WithMessage("El tipo de documento es obligatorio.").MaximumLength(3);
        v.RuleFor(numero).NotEmpty().WithErrorCode("Invitation.DocumentRequired")
            .WithMessage("El número de documento es obligatorio.").MaximumLength(30);
        v.RuleFor(confirmacion).NotEmpty().WithMessage("Digite otra vez el número de documento.");
        v.RuleFor(nombre).NotEmpty().WithMessage("El nombre es obligatorio.").MaximumLength(60);
        v.RuleFor(apellido).NotEmpty().WithMessage("El apellido es obligatorio.").MaximumLength(60);
    }
}
