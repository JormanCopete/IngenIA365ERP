using System.Text.RegularExpressions;
using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Lending;

namespace IngenIA365ERP.Application.Lending.Integration.Contrato;

// Feature 013, US5 (T0538; interfaces §3.2, §3.3 paso 2, §3.7–§3.10; T21, C35): la forma de cada solicitud del contrato de obligaciones. El contrato los
// ejecuta explícitamente (no pasa por MediatR, T101): un error es Rejected con Validation.Invalid y data.errors[] { field, code }. Las reglas de negocio
// (fechas, clases de medio, registro, saldos) no van aquí. (nuevo)

/// <summary>La gramática de las claves de cada recaudador (interfaces §3.10). (nuevo)</summary>
public static partial class GramaticaDeClaves
{
    /// <summary>Largo máximo de una clave (<c>LND_LoanOperations.IdempotencyKey</c>).</summary>
    public const int LargoMaximo = 150;

    [GeneratedRegex("^NOM:[0-9a-f]{32}:[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex Nomina();

    /// <summary>
    /// La recogida <c>CAR:{desembolso:N}:{obligación:N}</c> y, desde la feature 014 (US4, T119; R16), el débito automático
    /// <c>CAR:debito-automatico:{obligación:N}:{aaaammdd}</c>.
    /// </summary>
    [GeneratedRegex("^CAR:([0-9a-f]{32}:[0-9a-f]{32}|debito-automatico:[0-9a-f]{32}:[0-9]{8})$", RegexOptions.CultureInvariant)]
    private static partial Regex Recogida();

    [GeneratedRegex("^TST:[0-9a-f]{32}(:[0-9a-f]{32})?$", RegexOptions.CultureInvariant)]
    private static partial Regex Prueba();

    [GeneratedRegex("^REC:[^\\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Recaudo();

    /// <summary>Feature 014 (T042; contrato-de-cuentas §3.12): Tesorería, <c>TES:{documento:N}:{cuenta u obligación:N}</c>.</summary>
    [GeneratedRegex("^TES:[0-9a-f]{32}:[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex Tesoreria();

    /// <summary>¿La clave cumple la gramática de la clase del recaudador?</summary>
    public static bool Cumple(ClaseDeRecaudador clase, string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave) || clave.Length > LargoMaximo) return false;
        return clase switch
        {
            ClaseDeRecaudador.Payroll => Nomina().IsMatch(clave),
            ClaseDeRecaudador.OwnLoanPayoff => Recogida().IsMatch(clave),
            ClaseDeRecaudador.Test => Prueba().IsMatch(clave),
            ClaseDeRecaudador.Receipts => Recaudo().IsMatch(clave),
            ClaseDeRecaudador.Contributions => !clave.Any(char.IsWhiteSpace),
            ClaseDeRecaudador.Treasury => Tesoreria().IsMatch(clave),
            _ => false,
        };
    }

    /// <summary>Lo que dice el error de una clave que no cumple.</summary>
    public static string Mensaje(ClaseDeRecaudador clase) => clase switch
    {
        ClaseDeRecaudador.Payroll => "La clave de Nómina es NOM:{corrida:N}:{obligación:N}.",
        ClaseDeRecaudador.OwnLoanPayoff => "La clave de la recogida es CAR:{desembolso:N}:{obligación:N}.",
        ClaseDeRecaudador.Test => "La clave del recaudador de prueba es TST:{guid:N}.",
        ClaseDeRecaudador.Receipts => "La clave de Recaudo empieza con REC: y no lleva espacios.",
        ClaseDeRecaudador.Treasury => "La clave de Tesorería es TES:{documento:N}:{cuenta:N}.",
        _ => $"La clave no cumple la gramática del recaudador (hasta {LargoMaximo} caracteres, sin espacios).",
    };
}

/// <summary>La forma del recaudador. (nuevo)</summary>
public sealed class RecaudadorValidator : AbstractValidator<Recaudador>
{
    public RecaudadorValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10).WithErrorCode("CollectorCode");
        RuleFor(x => x.Class).IsInEnum().WithErrorCode("CollectorClass");
    }
}

/// <summary>La forma de un lote de pagos y no descontados (§3.7). (nuevo)</summary>
public sealed class SolicitudDeAplicacionValidator : AbstractValidator<SolicitudDeAplicacion>
{
    public SolicitudDeAplicacionValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.Form).IsInEnum().WithErrorCode("Form");
        RuleFor(x => x.Payments).NotNull();
        RuleFor(x => x.NotDeducted).NotNull();
        RuleFor(x => x).Must(x => (x.Payments?.Count ?? 0) + (x.NotDeducted?.Count ?? 0) > 0)
            .WithName("payments").WithErrorCode("Empty").WithMessage("El lote no trae pagos ni no descontados.");
        RuleFor(x => x.Form).Equal(FormaDeAplicacion.NonInteractive)
            .When(x => x.Collector?.Class is ClaseDeRecaudador.Payroll or ClaseDeRecaudador.OwnLoanPayoff)
            .WithErrorCode("FormNotAllowed").WithMessage("Nómina y la recogida de créditos propios aplican sin interacción.");
        RuleFor(x => x.Payments).Must(p => p.Select(i => i.IdempotencyKey).Distinct(StringComparer.Ordinal).Count() == p.Count)
            .When(x => x.Payments is not null).WithErrorCode("DuplicateKey").WithMessage("El lote repite una clave.");
        RuleForEach(x => x.Payments).ChildRules(p =>
        {
            p.RuleFor(i => i.ObligationPublicId).NotEmpty().WithErrorCode("ObligationPublicId");
            p.RuleFor(i => i.Modality).IsInEnum().WithErrorCode("Modality");
            p.RuleFor(i => i.Amount).GreaterThan(0m).WithErrorCode("Amount").WithMessage("El valor del pago es mayor que cero.");
            p.RuleFor(i => i.Amount).Must(a => decimal.Round(a, 2) == a).WithErrorCode("AmountPrecision").WithMessage("El valor va en pesos con centavos.");
            p.RuleFor(i => i.ValueDate).NotEqual(default(DateOnly)).WithErrorCode("ValueDate");
            p.RuleFor(i => i.PayerPublicId).NotEmpty().WithErrorCode("PayerPublicId");
            p.RuleFor(i => i.Receipt).NotNull().WithErrorCode("Receipt");
            p.RuleFor(i => i.Receipt.Number).NotEmpty().MaximumLength(40).When(i => i.Receipt is not null).WithErrorCode("ReceiptNumber");
            p.RuleFor(i => i.InstallmentNumber).NotNull().GreaterThan(0).When(i => i.Modality == ModalidadDePago.SpecificInstallment)
                .WithErrorCode("InstallmentNumber").WithMessage("El pago a una cuota específica indica la cuota.");
            p.RuleFor(i => i.Choice).Null().When(i => i.Modality != ModalidadDePago.ExtraordinaryPrincipal)
                .WithErrorCode("Choice").WithMessage("La elección sólo va en un abono extraordinario.");
            p.RuleFor(i => i.Choice).IsInEnum().When(i => i.Choice is not null).WithErrorCode("Choice");
            // K2 (T0840): la referencia a la línea pendiente de giro sólo la trae el pago de una pagaduría externa.
            p.RuleFor(i => i.DeductionFileLinePublicId).Null().When(i => i.MeansClass != ClaseDeMedioDeRecaudo.ExternalPaymasterDeduction)
                .WithErrorCode("DeductionFileLinePublicId")
                .WithMessage("La referencia a una línea del archivo de descuentos sólo va en el pago de una pagaduría.");
        });
        RuleForEach(x => x.Payments).Must((s, p) => GramaticaDeClaves.Cumple(s.Collector.Class, p.IdempotencyKey))
            .When(x => x.Collector is not null).WithErrorCode("IdempotencyKey").WithMessage((s, _) => GramaticaDeClaves.Mensaje(s.Collector.Class));
        RuleForEach(x => x.Payments).Must(p => !string.IsNullOrWhiteSpace(p.SimulationId))
            .When(x => x.Form == FormaDeAplicacion.Interactive).WithErrorCode("SimulationIdRequired")
            .WithMessage("En la forma interactiva cada pago lleva la simulación que vio el deudor.");
        RuleForEach(x => x.Payments).Must(p => p.SimulationId is null)
            .When(x => x.Form == FormaDeAplicacion.NonInteractive).WithErrorCode("SimulationIdNotAllowed")
            .WithMessage("En la forma no interactiva el pago no lleva simulación.");

        RuleFor(x => x.NotDeducted).Must(n => n.Count == 0)
            .When(x => x.NotDeducted is not null && (x.Collector?.Class != ClaseDeRecaudador.Payroll || x.Form != FormaDeAplicacion.NonInteractive))
            .WithErrorCode("NotDeductedNotAllowed").WithMessage("Sólo la nómina propia, sin interacción, informa cuotas no descontadas.");
        RuleForEach(x => x.NotDeducted).ChildRules(n =>
        {
            n.RuleFor(i => i.ObligationPublicId).NotEmpty().WithErrorCode("ObligationPublicId");
            n.RuleFor(i => i.ExpectedAmount).GreaterThanOrEqualTo(0m).WithErrorCode("ExpectedAmount");
            n.RuleFor(i => i.NotDeductedAmount).GreaterThan(0m).WithErrorCode("NotDeductedAmount");
            n.RuleFor(i => i).Must(i => i.NotDeductedAmount <= i.ExpectedAmount).WithName("notDeductedAmount").WithErrorCode("NotDeductedAmount")
                .WithMessage("Lo no descontado no supera lo esperado.");
            n.RuleFor(i => i.Reason).IsInEnum().WithErrorCode("Reason");
            n.RuleFor(i => i.Detail).MaximumLength(300).WithErrorCode("Detail");
        });
        RuleForEach(x => x.NotDeducted).Must((s, n) => GramaticaDeClaves.Cumple(s.Collector.Class, n.IdempotencyKey))
            .When(x => x.Collector is not null && x.NotDeducted is not null).WithErrorCode("IdempotencyKey")
            .WithMessage((s, _) => GramaticaDeClaves.Mensaje(s.Collector.Class));
    }
}

/// <summary>La forma de un reverso (§3.8). (nuevo)</summary>
public sealed class SolicitudDeReversoValidator : AbstractValidator<SolicitudDeReverso>
{
    public SolicitudDeReversoValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.PaymentIdempotencyKey).NotEmpty().MaximumLength(GramaticaDeClaves.LargoMaximo).WithErrorCode("PaymentIdempotencyKey");
        RuleFor(x => x.Reason).NotEmpty().WithErrorCode("Reason").WithMessage("Diga por qué se reversa el pago.").MaximumLength(500);
        // Fase 9: el número del recibo cabe en LND_LoanOperations.ReceiptNumber (40); sin esta regla la base respondía un 500.
        RuleFor(x => x.Receipt!.Number).NotEmpty().MaximumLength(40).When(x => x.Receipt is not null).WithErrorCode("ReceiptNumber");
    }
}

/// <summary>La forma de una resolución de saldo a favor o de anticipo (§3.9). (nuevo)</summary>
public sealed class SolicitudDeResolucionValidator : AbstractValidator<SolicitudDeResolucion>
{
    public SolicitudDeResolucionValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.IdempotencyKey).Must((s, k) => s.Collector is null || GramaticaDeClaves.Cumple(s.Collector.Class, k))
            .WithErrorCode("IdempotencyKey").WithMessage((s, _) => GramaticaDeClaves.Mensaje(s.Collector.Class));
        RuleFor(x => x.ObligationPublicId).NotEmpty().WithErrorCode("ObligationPublicId");
        RuleFor(x => x.Concept).Must(c => c is ConceptoDeObligacion.CreditBalance or ConceptoDeObligacion.Advance)
            .WithErrorCode("Concept").WithMessage("Se resuelve un saldo a favor o un anticipo.");
        RuleFor(x => x.Amount).GreaterThan(0m).WithErrorCode("Amount");
        RuleFor(x => x.Amount).Must(a => decimal.Round(a, 2) == a).WithErrorCode("AmountPrecision");
        RuleFor(x => x.Form).IsInEnum().WithErrorCode("Form");
        RuleFor(x => x.Receipt).NotNull().WithErrorCode("Receipt");
        RuleFor(x => x.Receipt.Number).NotEmpty().MaximumLength(40).When(x => x.Receipt is not null).WithErrorCode("ReceiptNumber");
        RuleFor(x => x.TargetObligationPublicId).NotNull().NotEqual(Guid.Empty).When(x => x.Form == FormaDeResolucion.ApplyToOtherObligation)
            .WithErrorCode("TargetObligationPublicId").WithMessage("Diga a qué obligación se aplica el saldo.");
        RuleFor(x => x.TargetModality).NotNull().IsInEnum().When(x => x.Form == FormaDeResolucion.ApplyToOtherObligation)
            .WithErrorCode("TargetModality").WithMessage("Diga con qué modalidad se aplica en la obligación destino.");
        RuleFor(x => x.TargetObligationPublicId).Null().When(x => x.Form != FormaDeResolucion.ApplyToOtherObligation).WithErrorCode("TargetObligationPublicId");
        RuleFor(x => x.Choice).NotNull().IsInEnum().When(x => x.Form == FormaDeResolucion.ApplyAsExtraordinaryPrincipal)
            .WithErrorCode("Choice").WithMessage("El abono a capital necesita que el deudor elija si reduce el plazo o la cuota.");
        RuleFor(x => x.Concept).Equal(ConceptoDeObligacion.CreditBalance).When(x => x.Form == FormaDeResolucion.ApplyAsExtraordinaryPrincipal)
            .WithErrorCode("Concept").WithMessage("Sólo un saldo a favor se aplica como abono a capital.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

/// <summary>La forma de una simulación (§3.6). (nuevo)</summary>
public sealed class SolicitudDeSimulacionValidator : AbstractValidator<SolicitudDeSimulacion>
{
    public SolicitudDeSimulacionValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.ObligationPublicId).NotEmpty().When(x => x.PaymentIdempotencyKeyToReverse is null).WithErrorCode("ObligationPublicId");
        RuleFor(x => x.Modality).IsInEnum().WithErrorCode("Modality");
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0m).WithErrorCode("Amount");
        RuleFor(x => x.Amount).GreaterThan(0m).When(x => x.PaymentIdempotencyKeyToReverse is null
                                                         && x.Modality is not (ModalidadDePago.FullPayoff or ModalidadDePago.PastDue))
            .WithErrorCode("Amount").WithMessage("El valor del pago es mayor que cero.");
        RuleFor(x => x.ValueDate).NotEqual(default(DateOnly)).When(x => x.PaymentIdempotencyKeyToReverse is null).WithErrorCode("ValueDate");
        RuleFor(x => x.InstallmentNumber).NotNull().GreaterThan(0).When(x => x.Modality == ModalidadDePago.SpecificInstallment
                                                                             && x.PaymentIdempotencyKeyToReverse is null)
            .WithErrorCode("InstallmentNumber");
        RuleFor(x => x.PaymentIdempotencyKeyToReverse).MaximumLength(GramaticaDeClaves.LargoMaximo);
    }
}
