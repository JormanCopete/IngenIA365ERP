using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Payroll.Settlements.Severance;

/// <summary>
/// Los errores propios de la consignación anual de cesantías que <c>SettlementErrors</c> (el núcleo
/// común) no trae. Los del contrato (<c>Payroll.Severance.NotApproved</c>, <c>.AlreadyDeposited</c>,
/// <c>.FundFormatMissing</c>) siguen allá; aquí sólo lo que nació al implementar US2.
/// </summary>
public static class SeveranceErrors
{
    public static readonly Error YearInvalid = new("Payroll.Severance.YearInvalid", "El año de la liquidación de cesantías debe ser un año de cuatro cifras.");

    public static Error CutoffOutsideYear(int year, DateOnly cutoff) =>
        new ErrorConDatos("Payroll.Severance.CutoffOutsideYear",
            $"La fecha de corte ({cutoff:dd/MM/yyyy}) debe caer dentro del año liquidado ({year}); por defecto es el 31 de diciembre.",
            new { year, cutoffDate = cutoff });

    public static readonly Error FundNotFound = new("Payroll.SeveranceFund.NotFound", "No existe el fondo de cesantías indicado.");

    public static Error FundNotInRun(string fundName) =>
        new ErrorConDatos("Payroll.Severance.FundNotInRun",
            $"Ningún empleado de esta liquidación consigna en {fundName}: no hay nada que marcar.",
            new { fundName });

    public static Error DepositDateInvalid(DateOnly depositedAt, DateOnly cutoff, DateOnly today) =>
        new ErrorConDatos("Payroll.Severance.DepositDateInvalid",
            $"La fecha de consignación ({depositedAt:dd/MM/yyyy}) debe estar entre la fecha de corte ({cutoff:dd/MM/yyyy}) y hoy ({today:dd/MM/yyyy}).",
            new { depositedAt, cutoffDate = cutoff, today });

    public static Error PayDateBeforeCutoff(DateOnly payDate, DateOnly cutoff) =>
        new ErrorConDatos("Payroll.Severance.PayDateBeforeCutoff",
            $"La fecha de pago de los intereses ({payDate:dd/MM/yyyy}) no puede ser anterior a la fecha de corte ({cutoff:dd/MM/yyyy}).",
            new { payDate, cutoffDate = cutoff });
}
