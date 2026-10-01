using FluentValidation;

namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Validador base de los comandos <see cref="IConMotivo"/> (feature 012, T053; contracts/api.md §2.8): el
/// motivo no puede venir vacío ni en blanco y tiene hasta <see cref="LargoMaximo"/> caracteres. Sin él, el
/// <c>ValidationBehavior</c> responde 400 <c>Validation.Invalid</c>. El validador de cada comando hereda de
/// éste y agrega sus reglas en su constructor; al ser abstracto, el registro automático de FluentValidation
/// no lo toma por sí solo.
/// </summary>
public abstract class ValidadorConMotivo<T> : AbstractValidator<T> where T : IConMotivo
{
    public const int LargoMaximo = 500;

    protected ValidadorConMotivo()
    {
        RuleFor(x => x.Reason)
            .Must(m => !string.IsNullOrWhiteSpace(m))
            .WithMessage("Indicá el motivo.")
            .MaximumLength(LargoMaximo)
            .WithMessage($"El motivo admite hasta {LargoMaximo} caracteres.");
    }
}
