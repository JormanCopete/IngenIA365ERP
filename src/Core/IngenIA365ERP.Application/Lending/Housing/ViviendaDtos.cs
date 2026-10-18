using IngenIA365ERP.Domain.Lending.Housing;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Application.Lending.Housing;

// Feature 013, K6b (bloque 2; FR-024, D18; api.md §3.1, §4.1, §8.1): lo de vivienda y UVR que las consultas exponen. Todo es opcional en el
// contrato (nulo en una línea o un crédito en pesos) para que las respuestas de K1–K6a no cambien. Sólo PublicId hacia afuera (Principio VI). (nuevo)

/// <summary>Una cuota del plan en UVR (la simulación de una línea en UVR): capital, interés, seguro, cuota y saldo, en UVR a la diezmilésima. (nuevo)</summary>
public sealed record UvrInstallmentDto(int Number, DateOnly DueDate, decimal Principal, decimal Interest, decimal Insurance, decimal Total, decimal BalanceAfter);

/// <summary>
/// El plan simulado en UVR (api.md §4.1, <c>uvr</c> de <c>ScheduleSimulationDto</c>): la UVR publicada del desembolso con que el plan en pesos se valoró,
/// el monto y el total en UVR, las cuotas en UVR y la leyenda: las cuotas en pesos son una <b>equivalencia</b> a esa UVR, no un compromiso. (nuevo)
/// </summary>
public sealed record ScheduleUvrDto(DateOnly UvrDate, decimal UvrValue, decimal AmountUvr, decimal ToPayUvr, IReadOnlyList<UvrInstallmentDto> Installments,
    string Legend)
{
    /// <summary>La leyenda de la equivalencia en pesos del plan en UVR.</summary>
    public static string LeyendaDePlan(DateOnly fecha) =>
        $"Crédito en UVR: las cuotas se pactan en UVR. Los valores en pesos son su equivalencia a la UVR del {fecha:dd/MM/yyyy} y cambian con la UVR de cada día de pago.";

    /// <summary>El plan en UVR de una simulación; nulo si la línea es en pesos.</summary>
    public static ScheduleUvrDto? De(PlanDePagos? planEnUvr, decimal? valorUvr, DateOnly fecha) =>
        planEnUvr is null || valorUvr is not { } uvr
            ? null
            : new ScheduleUvrDto(fecha, uvr, planEnUvr.TotalCapital, planEnUvr.TotalAPagar,
                planEnUvr.Cuotas.Select(c => new UvrInstallmentDto(c.Numero, c.Vencimiento, c.Capital, c.Interes, c.Seguro, c.Total, c.SaldoFinal)).ToList(),
                LeyendaDePlan(fecha));
}

/// <summary>
/// Lo de un crédito en UVR en la liquidación y el estado de cuenta (api.md §8.1, <c>uvr</c>): la UVR del día con que se valoró —la publicada o, a una
/// fecha sin UVR publicada, la <b>proyectada</b> desde la última publicada, sólo para mostrar—, el capital y el valor para cancelar en UVR y los saldos
/// por concepto en UVR. Los montos en pesos del resto de la respuesta son su valor a esa UVR. (nuevo)
/// </summary>
public sealed record LoanUvrDto(
    DateOnly UvrDate,
    decimal UvrValue,
    bool Projected,
    DateOnly LastPublished,
    decimal PrincipalUvr,
    decimal ToPayOffUvr,
    IReadOnlyDictionary<string, decimal> BalancesUvr,
    string Legend)
{
    /// <summary>Lo de UVR de un estado reconstruido; nulo si el crédito es en pesos.</summary>
    public static LoanUvrDto? De(EstadoDelCredito estado) => estado.Uvr is { } v ? De(v) : null;

    public static LoanUvrDto De(ValoracionEnUvr v) => new(
        v.UvrDelDia.Fecha,
        v.Valor,
        v.Proyectada,
        v.UvrDelDia.UltimaPublicada,
        v.CapitalEnUvr,
        v.Estado.ValorParaCancelar,
        v.Estado.SaldosPorConcepto.ToDictionary(s => s.Key.ToString(), s => s.Value),
        v.Proyectada
            ? $"UVR proyectada desde la última publicada ({v.UvrDelDia.UltimaPublicada:dd/MM/yyyy}): los pesos son sólo una referencia; se pagan a la UVR publicada del día del pago."
            : $"Valores en pesos a la UVR publicada del {v.UvrDelDia.Fecha:dd/MM/yyyy}.");
}

/// <summary>Un límite de vivienda vigente (valor nacional <c>LIMITE</c>): código, valor, fuente y su <c>PublicId</c>. (nuevo)</summary>
public sealed record HousingLimitDto(string Code, decimal Value, string? Source, Guid? NationalValuePublicId);

/// <summary>El tope de tasa de vivienda vigente (<c>TOPE_TASA_VIVIENDA</c>, EA): tasa, vigencia, fuente y su <c>PublicId</c>. (nuevo)</summary>
public sealed record HousingRateCapDto(decimal Rate, DateOnly ValidFrom, string? Source, Guid NationalValuePublicId);

/// <summary>
/// Las reglas de la Ley 546 de 1999 que rigen una línea de vivienda a una fecha (api.md §3.1, <c>housing</c> de <c>CreditLineDto</c>): el tope de
/// tasa y los cinco límites con su fuente. Lo que falte se dice en <see cref="Missing"/> (el código y la fecha), sin romper la consulta: la línea
/// se lee igual, pero no se origina ni se desembolsa hasta que el valor exista. (nuevo)
/// </summary>
public sealed record CreditLineHousingDto(DateOnly Date, HousingRateCapDto? RateCap, IReadOnlyList<HousingLimitDto> Limits, IReadOnlyList<string> Missing)
{
    /// <summary>Lee el tope y los límites vigentes a la fecha.</summary>
    public static async Task<CreditLineHousingDto> LeerAsync(LectorDeVivienda lector, DateOnly fecha, CancellationToken ct)
    {
        var faltan = new List<string>();
        HousingRateCapDto? tope = null;
        var leido = await lector.TopeAsync(fecha, ct);
        if (leido.IsSuccess) tope = new HousingRateCapDto(leido.Value.Tasa, leido.Value.VigenteDesde, leido.Value.Fuente, leido.Value.PublicId);
        else faltan.Add(leido.Error.Message);
        var limites = new List<HousingLimitDto>();
        var leidos = await lector.LimitesAsync(fecha, ct);
        if (leidos.IsSuccess)
        {
            var l = leidos.Value;
            limites.AddRange(new[] { l.PlazoMinimoMeses, l.PlazoMaximoMeses, l.Financiable, l.FinanciableVis, l.CuotaIngreso }
                .Select(x => new HousingLimitDto(x.Codigo, x.Valor, x.Fuente, x.PublicId)));
        }
        else
        {
            faltan.Add(leidos.Error.Message);
        }
        return new CreditLineHousingDto(fecha, tope, limites, faltan);
    }
}
