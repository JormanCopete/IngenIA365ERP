namespace IngenIA365ERP.Application.Common.Integration.Payroll;

/// <summary>Lo que Nómina recaudó para Cartera y Captaciones un día de pago: créditos, cuentas y cuántas deducciones. (nuevo)</summary>
public sealed record RecaudoDeNominaDelDiaDto(DateOnly PayDate, decimal Loans, decimal Accounts, int Deductions);

/// <summary>
/// La consulta de sólo lectura de Nómina para el consolidado de Recaudo (feature 015, R1, T0400; E-05; contratos-v2.md §5; FR-081): lo descontado por la nómina propia y
/// aplicado por los contratos en las corridas aprobadas, por fecha de pago. Nómina sigue recaudando por sus contratos; Recaudo sólo lo muestra. Síncrona, sin escribir. (nuevo)
/// </summary>
public interface IRecaudoDeNominaParaConsolidado
{
    Task<IReadOnlyList<RecaudoDeNominaDelDiaDto>> PorDiaAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
}
