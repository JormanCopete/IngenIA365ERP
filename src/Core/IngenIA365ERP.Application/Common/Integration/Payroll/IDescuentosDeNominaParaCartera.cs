namespace IngenIA365ERP.Application.Common.Integration.Payroll;

// 2026-11-27 (feature 028, T064a; R6 «Nómina propia»): la lectura con la que Cartera sabe si la nómina propia ya está descontando la cuota de un crédito.
// Cartera no lee tablas PAY_ (LaCarteraSoloSeTocaPorSusInterfaces): Nómina publica este puerto de sólo lectura y lo implementa en Application/Payroll. (nuevo)

/// <summary>
/// En qué va el descuento de cartera de la nómina propia: la novedad ya está en el período abierto (<see cref="Generated"/>), una corrida en borrador ya lo
/// calculó (<see cref="Calculated"/>) o la corrida aprobada ya lo aplicó a Cartera (<see cref="Approved"/>). (nuevo)
/// </summary>
public enum EstadoDelDescuentoDeNomina
{
    Generated = 1,
    Calculated = 2,
    Approved = 3,
}

/// <summary>
/// Un descuento de cartera vivo de la nómina propia para una obligación (nuevo): en qué va, el período (su <c>PublicId</c>, su nombre y sus fechas), la
/// corrida que lo calculó o lo aplicó (nula si sólo hay novedad), la cuota que la novedad nombra (nula si suma varias) y el valor: el de la novedad, lo que
/// la corrida descuenta o lo que se aplicó.
/// </summary>
public sealed record DescuentoDeNominaVivoDto(
    Guid ObligationPublicId,
    EstadoDelDescuentoDeNomina State,
    Guid PeriodPublicId,
    string Period,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    Guid? RunPublicId,
    int? RunVersion,
    int? InstallmentNumber,
    decimal Amount);

/// <summary>
/// <b>Los descuentos de cartera vivos de la nómina propia</b> (feature 028, T064a; R6; FR-021), de sólo lectura y sin escribir: por cada obligación pedida,
/// la novedad <c>LoanDeduction</c> activa de un período que no se ha aprobado, la fila <c>PAY_PayrollLoanDeductions</c> de una corrida ordinaria en
/// borrador, o la de una corrida aprobada de un período que no había terminado a <c>fecha</c>. Lo usa Cartera (<c>CuotaEnviadaDelCredito</c>) para decir que
/// la cuota ya va en la nómina; Nómina sigue descontando y aplicando por el contrato de obligaciones. (nuevo)
/// </summary>
public interface IDescuentosDeNominaParaCartera
{
    Task<IReadOnlyList<DescuentoDeNominaVivoDto>> VivosAsync(IReadOnlyCollection<Guid> obligaciones, DateOnly fecha, CancellationToken ct);
}
