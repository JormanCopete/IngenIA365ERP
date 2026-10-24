using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Receipts.Arqueo;

/// <summary>Un medio sellado en la sesión con su método y su tolerancia de arqueo (los de la 012, sellados al abrir). (nuevo)</summary>
public sealed record MedioDeArqueo(int PaymentMeansId, string Code, PaymentMeansClass Class, CashCountMethod CountMethod, decimal Tolerance);

/// <summary>Un recibo (por medio) de la sesión; <see cref="Anulado"/> es una anulación hecha en la sesión abierta, que baja lo esperado. (nuevo)</summary>
public sealed record CobroDeLaSesion(int PaymentMeansId, decimal Amount, bool Anulado = false, string? Reference = null);

/// <summary>
/// Un movimiento de caja de la sesión (data-model §2.4). En un traslado entre cajas, <see cref="EsOrigen"/> dice si la sesión es la que envía (resta) o la
/// que recibe (suma); en una reclasificación, el medio sale de <see cref="PaymentMeansId"/> y entra a <see cref="ToPaymentMeansId"/>. (nuevo)
/// </summary>
public sealed record MovimientoDeLaSesion(CashMovementKindR Kind, int PaymentMeansId, int? ToPaymentMeansId, decimal Amount, bool EsOrigen = true);

/// <summary>Lo que el motor necesita para calcular lo esperado de una sesión. (nuevo)</summary>
public sealed record PedidoDeArqueo(
    IReadOnlyList<MedioDeArqueo> Medios,
    int? MedioDeLaBase,
    decimal Base,
    IReadOnlyList<CobroDeLaSesion> Cobros,
    IReadOnlyList<MovimientoDeLaSesion> Movimientos);

/// <summary>Lo esperado de un medio y de dónde sale. (nuevo)</summary>
public sealed record EsperadoDeMedio(MedioDeArqueo Medio, decimal Base, decimal Cobros, decimal Anulaciones, decimal Entradas, decimal Salidas)
{
    public decimal Expected => Base + Cobros - Anulaciones + Entradas - Salidas;
}

public sealed record DenominacionContada(decimal Valor, int Cantidad);

public sealed record ReferenciaContada(string Referencia, decimal Valor, bool Confirmada);

public sealed record LoteContado(int TerminalId, string Lote, decimal Valor);

/// <summary>Lo contado de un medio: el total, o el detalle que pide su método (denominaciones, referencias, lotes de datáfono). (nuevo)</summary>
public sealed record ConteoDeMedio(
    int PaymentMeansId,
    decimal? Total,
    IReadOnlyList<DenominacionContada>? Denominaciones,
    IReadOnlyList<ReferenciaContada>? Referencias,
    IReadOnlyList<LoteContado>? Lotes,
    string? Motivo);

/// <summary>Una línea del arqueo por medio. (nuevo)</summary>
public sealed record LineaDeArqueo(
    int PaymentMeansId,
    string Code,
    CashCountMethod Method,
    decimal Expected,
    decimal Counted,
    decimal Difference,
    decimal Tolerance,
    bool WithinTolerance,
    CountTreatment Treatment,
    string? Reason);

/// <summary>El arqueo de la sesión: líneas, totales, lo que hay que aprobar y los medios con diferencia sin motivo. (nuevo)</summary>
public sealed record ResultadoDeArqueo(
    IReadOnlyList<LineaDeArqueo> Lineas,
    decimal TotalEsperado,
    decimal TotalContado,
    decimal TotalDiferencia,
    decimal MontoAAprobar,
    IReadOnlyList<string> SinMotivo)
{
    public bool HayDiferencia => Lineas.Any(l => l.Difference != 0m);

    public bool RequiereAprobacion => MontoAAprobar > 0m;
}

/// <summary>
/// El motor puro del arqueo de la caja de Recaudo (feature 015, T0088; R-14; FR-031, FR-032), sin IO y en decimal, con los métodos de arqueo del catálogo
/// común de la 012:
/// <list type="bullet">
/// <item>esperado por medio = base (sólo el medio de la base) + recibos − anulaciones en la sesión ± movimientos (abastecimiento, retiro a bóveda,
/// traslados, custodia) ± reclasificaciones; una línea por medio sellado, en su orden;</item>
/// <item>contado según el método: efectivo por denominaciones (Σ valor × cantidad) o por total; tarjetas por lote de datáfono; transferencias,
/// consignaciones y cheques por referencias (sólo las confirmadas); sin método, lo esperado;</item>
/// <item>diferencia = contado − esperado; dentro de la tolerancia del medio si |diferencia| ≤ tolerancia; el faltante va al tratamiento sellado (cuenta por
/// cobrar al cajero o gasto) y el sobrante a sobrantes; a aprobar = Σ |diferencia| de las líneas sobre la tolerancia; toda diferencia lleva motivo.</item>
/// </list>
/// El arqueo ciego no cambia el cálculo: sólo cuándo se ve lo esperado. (nuevo)
/// </summary>
public static class ArqueoDeCaja
{
    public static IReadOnlyList<EsperadoDeMedio> Esperado(PedidoDeArqueo pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var a = pedido.Medios.ToDictionary(m => m.PaymentMeansId, _ => new decimal[5]);
        decimal[] De(int medio) => a.TryGetValue(medio, out var v)
            ? v
            : throw new ArgumentException($"El medio {medio} no está sellado en la sesión.", nameof(pedido));

        if (pedido.MedioDeLaBase is { } medioDeLaBase) De(medioDeLaBase)[0] += pedido.Base;
        foreach (var c in pedido.Cobros)
        {
            var v = De(c.PaymentMeansId);
            v[1] += c.Amount;
            if (c.Anulado) v[2] += c.Amount;
        }
        foreach (var m in pedido.Movimientos)
        {
            switch (m.Kind)
            {
                case CashMovementKindR.Opening:
                    break;
                case CashMovementKindR.SupplyFromVault:
                case CashMovementKindR.Custody:
                    De(m.PaymentMeansId)[3] += m.Amount;
                    break;
                case CashMovementKindR.WithdrawToVault:
                case CashMovementKindR.CustodyRelease:
                    De(m.PaymentMeansId)[4] += m.Amount;
                    break;
                case CashMovementKindR.TransferBetweenBoxes:
                    De(m.PaymentMeansId)[m.EsOrigen ? 4 : 3] += m.Amount;
                    break;
                case CashMovementKindR.MeansReclassification:
                    De(m.PaymentMeansId)[4] += m.Amount;
                    De(m.ToPaymentMeansId ?? throw new ArgumentException("La reclasificación no trae el medio destino.", nameof(pedido)))[3] += m.Amount;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(pedido), m.Kind, "Movimiento de caja desconocido.");
            }
        }
        return pedido.Medios.Select(m => { var v = a[m.PaymentMeansId]; return new EsperadoDeMedio(m, v[0], v[1], v[2], v[3], v[4]); }).ToList();
    }

    /// <summary>Lo contado de un medio según su método.</summary>
    public static decimal Contado(MedioDeArqueo medio, ConteoDeMedio? conteo, decimal esperado) => medio.CountMethod switch
    {
        CashCountMethod.None => esperado,
        CashCountMethod.PhysicalCount when conteo?.Denominaciones is { Count: > 0 } d => d.Sum(x => x.Valor * x.Cantidad),
        CashCountMethod.VoucherTotal when conteo?.Lotes is { Count: > 0 } l => l.Sum(x => x.Valor),
        CashCountMethod.ByReference when conteo?.Referencias is { Count: > 0 } r => r.Where(x => x.Confirmada).Sum(x => x.Valor),
        _ => conteo?.Total ?? 0m,
    };

    public static ResultadoDeArqueo Arquear(IReadOnlyList<EsperadoDeMedio> esperado, IReadOnlyList<ConteoDeMedio> conteos, CountTreatment tratamientoDelFaltante)
    {
        ArgumentNullException.ThrowIfNull(esperado);
        ArgumentNullException.ThrowIfNull(conteos);
        if (tratamientoDelFaltante is not (CountTreatment.CashierReceivable or CountTreatment.Expense))
            throw new ArgumentOutOfRangeException(nameof(tratamientoDelFaltante), tratamientoDelFaltante, "El faltante va al cajero o al gasto.");
        var porMedio = conteos.ToDictionary(c => c.PaymentMeansId);
        var ajenos = porMedio.Keys.Except(esperado.Select(e => e.Medio.PaymentMeansId)).ToList();
        if (ajenos.Count > 0) throw new ArgumentException($"Se contaron medios que no están en la sesión: {string.Join(", ", ajenos)}.", nameof(conteos));

        var lineas = new List<LineaDeArqueo>();
        var sinMotivo = new List<string>();
        var aAprobar = 0m;
        foreach (var e in esperado)
        {
            var conteo = porMedio.GetValueOrDefault(e.Medio.PaymentMeansId);
            var motivo = string.IsNullOrWhiteSpace(conteo?.Motivo) ? null : conteo!.Motivo!.Trim();
            var contado = Contado(e.Medio, conteo, e.Expected);
            var diferencia = contado - e.Expected;
            var dentro = Math.Abs(diferencia) <= e.Medio.Tolerance;
            var tratamiento = diferencia switch { > 0m => CountTreatment.Surplus, < 0m => tratamientoDelFaltante, _ => CountTreatment.None };
            lineas.Add(new LineaDeArqueo(e.Medio.PaymentMeansId, e.Medio.Code, e.Medio.CountMethod, e.Expected, contado, diferencia, e.Medio.Tolerance,
                dentro, tratamiento, motivo));
            if (diferencia == 0m) continue;
            if (!dentro) aAprobar += Math.Abs(diferencia);
            if (motivo is null) sinMotivo.Add(e.Medio.Code);
        }
        return new ResultadoDeArqueo(lineas, lineas.Sum(l => l.Expected), lineas.Sum(l => l.Counted), lineas.Sum(l => l.Difference), aAprobar, sinMotivo);
    }
}
