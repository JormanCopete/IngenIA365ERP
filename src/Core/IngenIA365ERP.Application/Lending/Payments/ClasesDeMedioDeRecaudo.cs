using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>
/// La matriz clase de recaudador × clase de medio de recaudo (feature 013, US5, T0540; FR-082, D23, C30; contracts/interfaces.md §3.2 y §3.14):
/// <list type="bullet">
/// <item>una clase que el enum no conoce: <c>Lending.Contract.UnknownMeansClass</c>;</item>
/// <item><c>AssociateCredit</c> y <c>CustomerCredit</c> se declaran y se rechazan siempre (<c>Lending.Payment.MeansClassNotAllowed</c>): pagar un
/// crédito con crédito es recogida o novación (C30);</item>
/// <item><c>Payroll</c>: sólo <c>OwnPayrollDeduction</c>; <c>Contributions</c> y <c>OwnLoanPayoff</c>: sólo <c>InternalOffset</c>;
/// <c>Receipts</c>: las de la 012 y <c>ExternalPaymasterDeduction</c>; <c>Test</c>: todas salvo las de crédito; feature 014: <c>Treasury</c>,
/// <c>Transfer</c> y <c>BankDeposit</c>;</item>
/// <item>la aplicación de una resolución de saldo a favor a otra obligación es siempre <c>InternalOffset</c>, sea cual sea el recaudador.</item>
/// </list>
/// Pura. (nuevo)
/// </summary>
public static class ClasesDeMedioDeRecaudo
{
    /// <summary>Las clases de la 012 (<c>PaymentMeansClass</c>) que no son de crédito.</summary>
    public static IReadOnlySet<ClaseDeMedioDeRecaudo> DeCaja { get; } = new HashSet<ClaseDeMedioDeRecaudo>
    {
        ClaseDeMedioDeRecaudo.Cash, ClaseDeMedioDeRecaudo.CreditCard, ClaseDeMedioDeRecaudo.DebitCard, ClaseDeMedioDeRecaudo.BankDeposit,
        ClaseDeMedioDeRecaudo.Transfer, ClaseDeMedioDeRecaudo.Voucher, ClaseDeMedioDeRecaudo.Check, ClaseDeMedioDeRecaudo.Other,
    };

    /// <summary>Las de crédito: se declaran y el contrato las rechaza (C30).</summary>
    public static IReadOnlySet<ClaseDeMedioDeRecaudo> DeCredito { get; } = new HashSet<ClaseDeMedioDeRecaudo>
    {
        ClaseDeMedioDeRecaudo.AssociateCredit, ClaseDeMedioDeRecaudo.CustomerCredit,
    };

    /// <summary>¿Admite el recaudador esa clase de medio?</summary>
    public static bool Admite(ClaseDeRecaudador recaudador, ClaseDeMedioDeRecaudo medio, bool esResolucion = false)
    {
        if (!Enum.IsDefined(medio) || DeCredito.Contains(medio)) return false;
        if (esResolucion) return medio == ClaseDeMedioDeRecaudo.InternalOffset;
        return recaudador switch
        {
            ClaseDeRecaudador.Payroll => medio == ClaseDeMedioDeRecaudo.OwnPayrollDeduction,
            ClaseDeRecaudador.Contributions or ClaseDeRecaudador.OwnLoanPayoff => medio == ClaseDeMedioDeRecaudo.InternalOffset,
            ClaseDeRecaudador.Receipts => DeCaja.Contains(medio) || medio == ClaseDeMedioDeRecaudo.ExternalPaymasterDeduction,
            ClaseDeRecaudador.Test => true,
            // Feature 014 (T042; contrato-de-cuentas §3.3): Tesorería consigna las transferencias que recibe.
            ClaseDeRecaudador.Treasury => medio is ClaseDeMedioDeRecaudo.Transfer or ClaseDeMedioDeRecaudo.BankDeposit,
            _ => false,
        };
    }

    /// <summary>El error de la clase de medio, o nulo si se admite.</summary>
    public static Error? Validar(ClaseDeRecaudador recaudador, ClaseDeMedioDeRecaudo medio, bool esResolucion = false)
    {
        if (!Enum.IsDefined(medio)) return ErroresDePagos.ClaseDeMedioDesconocida((int)medio);
        if (DeCredito.Contains(medio)) return ErroresDePagos.ClaseDeMedioDeCredito(medio);
        return Admite(recaudador, medio, esResolucion) ? null : ErroresDePagos.ClaseDeMedioNoAdmitida(medio, recaudador);
    }

    /// <summary>
    /// 2026-11-27 (feature 028, R4, T058): los recaudadores que admiten «todo menos la cuota causada» (<see cref="ModalidadDePago.PayoffExceptCurrentInstallment"/>):
    /// la recogida al desembolsar (<c>OwnLoanPayoff</c>) y la caja de Recaudo (<c>Receipts</c>). (nuevo)
    /// </summary>
    public static IReadOnlySet<ClaseDeRecaudador> ConCancelacionSalvoCuota { get; } = new HashSet<ClaseDeRecaudador>
    {
        ClaseDeRecaudador.OwnLoanPayoff, ClaseDeRecaudador.Receipts,
    };

    /// <summary>2026-11-27 (feature 028, T058): ¿admite el recaudador esa modalidad? Las de siempre, todos; la cancelación salvo cuota, sólo los suyos. (nuevo)</summary>
    public static bool AdmiteModalidad(ClaseDeRecaudador recaudador, ModalidadDePago modalidad) =>
        Enum.IsDefined(modalidad)
        && (modalidad != ModalidadDePago.PayoffExceptCurrentInstallment || ConCancelacionSalvoCuota.Contains(recaudador));

    /// <summary>2026-11-27 (feature 028, T058): el error de la modalidad (<c>Lending.Payment.ModalityNotAllowed</c>), o nulo si el recaudador la admite. (nuevo)</summary>
    public static Error? ValidarModalidad(ClaseDeRecaudador recaudador, ModalidadDePago modalidad) =>
        AdmiteModalidad(recaudador, modalidad) ? null : ErroresDePagos.ModalidadNoAdmitidaPara(modalidad, recaudador);
}
