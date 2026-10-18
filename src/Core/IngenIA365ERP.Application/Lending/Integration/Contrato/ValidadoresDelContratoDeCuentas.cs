using FluentValidation;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;

namespace IngenIA365ERP.Application.Lending.Integration.Contrato;

// Feature 014, US3 (T083; contrato-de-cuentas §3.4 paso 2, §3.6–§3.11; T101 de la 013): la forma de cada solicitud del contrato de cuentas. El contrato los
// ejecuta explícitamente (no pasa por MediatR): un error es Rejected con Validation.Invalid y data.errors[] { field, code }. Las reglas de negocio
// (cuenta, cuotas, fechas, clases de medio, registro, saldos) no van aquí. (nuevo)

/// <summary>La forma de una consulta de exigibles (§3.6). (nuevo)</summary>
public sealed class SolicitudDeExigiblesValidator : AbstractValidator<SolicitudDeExigibles>
{
    /// <summary>Cuántas personas se piden a la vez (Nómina pide sus empleados en bloque).</summary>
    public const int MaximoDePersonas = 5000;

    public SolicitudDeExigiblesValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.PersonPublicIds).NotNull().Must(p => p.Count is > 0 and <= MaximoDePersonas).WithErrorCode("PersonPublicIds")
            .WithMessage($"Pida las cuotas de 1 a {MaximoDePersonas} personas.");
        RuleFor(x => x).Must(x => x.PeriodStart is null == x.PeriodEnd is null && (x.PeriodStart is null || x.PeriodStart <= x.PeriodEnd))
            .WithErrorCode("Period").WithMessage("El período lleva inicio y fin, y el inicio no pasa del fin.");
    }
}

/// <summary>La forma de un lote de consignaciones y no descontados (§3.8). (nuevo)</summary>
public sealed class SolicitudDeConsignacionValidator : AbstractValidator<SolicitudDeConsignacion>
{
    public SolicitudDeConsignacionValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.Form).IsInEnum().WithErrorCode("Form");
        RuleFor(x => x.Deposits).NotNull();
        RuleFor(x => x.NotDeducted).NotNull();
        RuleFor(x => x).Must(x => (x.Deposits?.Count ?? 0) + (x.NotDeducted?.Count ?? 0) > 0).WithErrorCode("Items")
            .WithMessage("El lote trae al menos una consignación o un no descontado.");
        RuleFor(x => x).Must(x => (x.Deposits ?? []).Select(d => d.IdempotencyKey).Distinct().Count() == (x.Deposits?.Count ?? 0))
            .WithErrorCode("IdempotencyKey").WithMessage("Dos consignaciones del lote no comparten clave.");
        RuleForEach(x => x.Deposits).ChildRules(d =>
        {
            d.RuleFor(i => i.AccountPublicId).NotEmpty().WithErrorCode("AccountPublicId");
            d.RuleFor(i => i.Modality).IsInEnum().WithErrorCode("Modality");
            d.RuleFor(i => i.MeansClass).IsInEnum().WithErrorCode("MeansClass");
            d.RuleFor(i => i.Amount).GreaterThan(0m).WithErrorCode("Amount")
                .Must(m => Deposits.Ledger.RegistroDeCuentas.Decimales(m) <= MemberAccountTransaction.DecimalesDeOperacion).WithErrorCode("AmountTooPrecise")
                .WithMessage("El valor lleva a lo sumo dos decimales.");
            d.RuleFor(i => i.DepositorPublicId).NotEmpty().WithErrorCode("DepositorPublicId");
            d.RuleFor(i => i.Receipt).NotNull().WithErrorCode("Receipt");
            d.RuleFor(i => i.Receipt.Number).NotEmpty().MaximumLength(40).When(i => i.Receipt is not null).WithErrorCode("ReceiptNumber");
            d.RuleFor(i => i.InstallmentPublicIds).NotEmpty().When(i => i.Modality == ModalidadDeConsignacion.SpecificInstallments)
                .WithErrorCode("InstallmentPublicIds").WithMessage("Nombre las cuotas que paga.");
        });
        RuleForEach(x => x.Deposits).Must((s, d) => GramaticaDeClaves.Cumple(s.Collector.Class, d.IdempotencyKey)).When(x => x.Collector is not null)
            .WithErrorCode("IdempotencyKey").WithMessage((s, _) => GramaticaDeClaves.Mensaje(s.Collector.Class));
        RuleForEach(x => x.NotDeducted).ChildRules(n =>
        {
            n.RuleFor(i => i.AccountPublicId).NotEmpty().WithErrorCode("AccountPublicId");
            n.RuleFor(i => i.InstallmentPublicId).NotEmpty().WithErrorCode("InstallmentPublicId");
            n.RuleFor(i => i.ExpectedAmount).GreaterThan(0m).WithErrorCode("ExpectedAmount");
            n.RuleFor(i => i.NotDeductedAmount).GreaterThan(0m).WithErrorCode("NotDeductedAmount");
            n.RuleFor(i => i).Must(i => i.NotDeductedAmount <= i.ExpectedAmount).WithErrorCode("NotDeductedAmount")
                .WithMessage("Lo no descontado no pasa de lo esperado.");
            n.RuleFor(i => i.Reason).IsInEnum().WithErrorCode("Reason");
            n.RuleFor(i => i.Detail).MaximumLength(300);
        });
        RuleForEach(x => x.NotDeducted).Must((s, n) => GramaticaDeClaves.Cumple(s.Collector.Class, n.IdempotencyKey)).When(x => x.Collector is not null)
            .WithErrorCode("IdempotencyKey").WithMessage((s, _) => GramaticaDeClaves.Mensaje(s.Collector.Class));
    }
}

/// <summary>La forma de una simulación (§3.7). (nuevo)</summary>
public sealed class SolicitudDeSimulacionDeCuentaValidator : AbstractValidator<SolicitudDeSimulacionDeCuenta>
{
    public static readonly string[] Operaciones = ["Deposit", "Withdrawal", "Transfer"];

    public SolicitudDeSimulacionDeCuentaValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.Operation).Must(o => Operaciones.Contains(o)).WithErrorCode("Operation").WithMessage("La operación es Deposit, Withdrawal o Transfer.");
        RuleFor(x => x.AccountPublicId).NotEmpty().WithErrorCode("AccountPublicId");
        RuleFor(x => x.Amount).GreaterThan(0m).WithErrorCode("Amount");
        RuleFor(x => x.MeansClass).IsInEnum().WithErrorCode("MeansClass");
        RuleFor(x => x.SignerPersonPublicIds).NotNull();
    }
}

/// <summary>La forma de un reverso (§3.11). (nuevo)</summary>
public sealed class SolicitudDeReversoDeCuentaValidator : AbstractValidator<SolicitudDeReversoDeCuenta>
{
    public SolicitudDeReversoDeCuentaValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(GramaticaDeClaves.LargoMaximo).WithErrorCode("IdempotencyKey");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithErrorCode("Reason").WithMessage("Diga por qué se reversa.");
    }
}

/// <summary>La forma de un retiro (§3.9; US4, T111). (nuevo)</summary>
public sealed class SolicitudDeRetiroValidator : AbstractValidator<SolicitudDeRetiro>
{
    public SolicitudDeRetiroValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(GramaticaDeClaves.LargoMaximo).WithErrorCode("IdempotencyKey");
        RuleFor(x => x).Must(x => GramaticaDeClaves.Cumple(x.Collector.Class, x.IdempotencyKey)).When(x => x.Collector is not null)
            .WithErrorCode("IdempotencyKey").WithMessage(x => GramaticaDeClaves.Mensaje(x.Collector.Class));
        RuleFor(x => x.AccountPublicId).NotEmpty().WithErrorCode("AccountPublicId");
        RuleFor(x => x.Amount).GreaterThan(0m).WithErrorCode("Amount")
            .Must(m => Deposits.Ledger.RegistroDeCuentas.Decimales(m) <= MemberAccountTransaction.DecimalesDeOperacion).WithErrorCode("AmountTooPrecise")
            .WithMessage("El valor lleva a lo sumo dos decimales.");
        RuleFor(x => x.MeansClass).IsInEnum().WithErrorCode("MeansClass");
        RuleFor(x => x.SignerPersonPublicIds).NotNull().WithErrorCode("SignerPersonPublicIds");
        RuleFor(x => x.ReceiverPersonPublicId).NotEmpty().WithErrorCode("ReceiverPersonPublicId");
        RuleFor(x => x.Receipt).NotNull().WithErrorCode("Receipt");
        RuleFor(x => x.Receipt.Number).NotEmpty().MaximumLength(40).When(x => x.Receipt is not null).WithErrorCode("ReceiptNumber");
    }
}

/// <summary>La forma de un traslado (§3.10; US4, T111). (nuevo)</summary>
public sealed class SolicitudDeTrasladoValidator : AbstractValidator<SolicitudDeTraslado>
{
    public SolicitudDeTrasladoValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(GramaticaDeClaves.LargoMaximo).WithErrorCode("IdempotencyKey");
        RuleFor(x => x).Must(x => GramaticaDeClaves.Cumple(x.Collector.Class, x.IdempotencyKey)).When(x => x.Collector is not null)
            .WithErrorCode("IdempotencyKey").WithMessage(x => GramaticaDeClaves.Mensaje(x.Collector.Class));
        RuleFor(x => x.SourceAccountPublicId).NotEmpty().WithErrorCode("SourceAccountPublicId");
        RuleFor(x => x.TargetAccountPublicId).NotEmpty().NotEqual(x => x.SourceAccountPublicId).WithErrorCode("TargetAccountPublicId")
            .WithMessage("El traslado es entre dos cuentas distintas.");
        RuleFor(x => x.Amount).GreaterThan(0m).WithErrorCode("Amount")
            .Must(m => Deposits.Ledger.RegistroDeCuentas.Decimales(m) <= MemberAccountTransaction.DecimalesDeOperacion).WithErrorCode("AmountTooPrecise")
            .WithMessage("El valor lleva a lo sumo dos decimales.");
        RuleFor(x => x.SignerPersonPublicIds).NotNull().WithErrorCode("SignerPersonPublicIds");
        RuleFor(x => x.Receipt).NotNull().WithErrorCode("Receipt");
        RuleFor(x => x.TargetModality).IsInEnum().NotEqual(ModalidadDeConsignacion.TermDepositFunding).WithErrorCode("TargetModality");
    }
}

/// <summary>La forma de una consulta de cuentas (§3.5). (nuevo)</summary>
public sealed class SolicitudDeConsultaDeCuentasValidator : AbstractValidator<SolicitudDeConsultaDeCuentas>
{
    public SolicitudDeConsultaDeCuentasValidator()
    {
        RuleFor(x => x.Collector).NotNull().SetValidator(new RecaudadorValidator());
        RuleFor(x => x.PersonPublicId).NotEmpty().WithErrorCode("PersonPublicId");
    }
}

