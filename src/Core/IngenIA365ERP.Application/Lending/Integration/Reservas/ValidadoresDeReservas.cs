using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Lending.Integration.Reservas;

/// <summary>
/// El validador explícito de <see cref="IReservasDeCartera.ReservarAsync"/> (feature 013, US6, T0584; T101: el puerto corre fuera de MediatR y valida
/// su forma por su cuenta, <c>LosPuertosDeCarteraValidanYAuditanSusEscrituras</c>). La clave es <c>INV:{paymentPublicId:N}:{monto}</c> con el monto
/// de la petición (dos decimales, punto, cultura invariante). (nuevo)
/// </summary>
public sealed class SolicitudDeReservaValidator : AbstractValidator<SolicitudDeReserva>
{
    public const int LargoDeLaClave = 120;

    public SolicitudDeReservaValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(LargoDeLaClave)
            .Must((s, clave) => string.Equals(clave, ClaveDeReserva(s.PaymentPublicId, s.Amount), StringComparison.Ordinal))
            .WithMessage("La clave de la reserva es INV:{pago}:{monto}, con el pago y el monto de la petición.");
        RuleFor(x => x.PaymentPublicId).NotEmpty();
        RuleFor(x => x.DocumentPublicId).NotEmpty();
        RuleFor(x => x.PersonPublicId).NotEmpty();
        RuleFor(x => x.MeansClass).Must(c => c is PaymentMeansClass.AssociateCredit or PaymentMeansClass.CustomerCredit)
            .WithMessage("Sólo se reserva el número de un pago a crédito de asociado o de cliente.");
        RuleFor(x => x.Amount).GreaterThan(0m).WithMessage("El valor a crédito es mayor que cero.");
        RuleFor(x => x.OperationDate).NotEqual(default(DateOnly));
        RuleFor(x => x.CreditLineCode).MaximumLength(20);
        RuleFor(x => x.ConsultationEvidence).MaximumLength(64);
    }

    /// <summary>La clave de una reserva (T21, G9): <c>INV:{paymentPublicId:N}:{monto}</c> con dos decimales y punto.</summary>
    public static string ClaveDeReserva(Guid pago, decimal monto) =>
        $"INV:{pago:N}:{monto.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}";
}

/// <summary>El validador explícito de <see cref="IReservasDeCartera.LiberarAsync"/> (feature 013, US6, T0584; T101). (nuevo)</summary>
public sealed class SolicitudDeLiberacionValidator : AbstractValidator<SolicitudDeLiberacion>
{
    public SolicitudDeLiberacionValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(SolicitudDeReservaValidator.LargoDeLaClave);
        RuleFor(x => x.PaymentPublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300).WithMessage("El motivo de la liberación va de 1 a 300 caracteres.");
    }
}
