using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Receipts.Imputacion;

/// <summary>El orden de imputación de los medios (parámetro <c>Recibo.OrdenDeImputacion</c>, D35). (nuevo)</summary>
public enum OrdenDeImputacion
{
    /// <summary>Los inmediatos en el orden en que se registraron y el cheque al final (el defecto).</summary>
    InmediatosPrimeroChequeAlFinal = 1,

    /// <summary>Los medios en el orden en que el cajero los registró, cheque incluido.</summary>
    ElegidoEnCaja = 2,
}

/// <summary>Una aplicación del recibo (en el orden de la pantalla) con su valor. (nuevo)</summary>
public sealed record AplicacionAImputar(string Id, decimal Valor);

/// <summary>Un medio del recibo con su clase y su valor recibido. (nuevo)</summary>
public sealed record MedioAImputar(string Id, PaymentMeansClass Clase, decimal Valor);

/// <summary>Una porción: lo que un medio cubre de una aplicación (cada porción es un ítem del contrato, FR-188). (nuevo)</summary>
public sealed record PorcionImputada(string Aplicacion, string Medio, decimal Valor);

/// <summary>Lo que se imputa: aplicaciones, medios, el orden y, si el cajero la fijó, la imputación a mano. (nuevo)</summary>
public sealed record PedidoDeImputacion(
    IReadOnlyList<AplicacionAImputar> Aplicaciones,
    IReadOnlyList<MedioAImputar> Medios,
    OrdenDeImputacion Orden,
    IReadOnlyList<PorcionImputada>? Fijada = null);

/// <summary>El reparto: las porciones y las vueltas, o el código del error (data-model §21). (nuevo)</summary>
public sealed record ResultadoDeImputacion(IReadOnlyList<PorcionImputada> Porciones, decimal Vueltas, string? Error)
{
    public bool EsValida => Error is null;

    public static ResultadoDeImputacion Fallo(string codigo) => new([], 0m, codigo);
}

/// <summary>
/// El motor puro que reparte los medios de un recibo entre sus aplicaciones (feature 015, T0109; R-11; FR-041, FR-188; D35), sin IO y en decimal:
/// <list type="bullet">
/// <item>total de medios = aplicado + vueltas: si los medios no alcanzan, <see cref="CodigoMediosNoCuadran"/>; las vueltas sólo salen del efectivo y nunca
/// pasan de él (<see cref="CodigoVueltasSoloEnEfectivo"/>);</item>
/// <item>por defecto los medios inmediatos cubren primero las aplicaciones en el orden de la pantalla y el cheque cubre lo restante; con
/// <see cref="OrdenDeImputacion.ElegidoEnCaja"/>, el orden en que se registraron; las vueltas se descuentan del último efectivo;</item>
/// <item>una imputación fijada a mano sólo se valida (cada aplicación cubierta exacto, ningún medio usado de más, el no efectivo usado entero, ninguna
/// porción en cero): si no cuadra, <see cref="CodigoImputacionInvalida"/>.</item>
/// </list>
/// Un cheque devuelto reversa sólo sus porciones; la firmeza y los umbrales de efectivo se evalúan por porción (lo hace quien llama). (nuevo)
/// </summary>
public static class ImputacionDeMedios
{
    public const string CodigoMediosNoCuadran = "Receipts.Receipt.MeansTotalMismatch";
    public const string CodigoVueltasSoloEnEfectivo = "Receipts.Receipt.ChangeOnlyInCash";
    public const string CodigoImputacionInvalida = "Receipts.Receipt.ImputationInvalid";

    public static ResultadoDeImputacion Imputar(PedidoDeImputacion pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        if (pedido.Aplicaciones.Count == 0 || pedido.Aplicaciones.Any(a => a.Valor <= 0m) || pedido.Medios.Any(m => m.Valor < 0m))
            return ResultadoDeImputacion.Fallo(CodigoImputacionInvalida);

        var aplicado = pedido.Aplicaciones.Sum(a => a.Valor);
        var recibido = pedido.Medios.Sum(m => m.Valor);
        if (recibido < aplicado) return ResultadoDeImputacion.Fallo(CodigoMediosNoCuadran);
        var vueltas = recibido - aplicado;
        var efectivo = pedido.Medios.Where(m => m.Clase == PaymentMeansClass.Cash).Sum(m => m.Valor);
        if (vueltas > efectivo) return ResultadoDeImputacion.Fallo(CodigoVueltasSoloEnEfectivo);

        return pedido.Fijada is { } fijada ? Validar(pedido, fijada, vueltas) : Repartir(pedido, vueltas);
    }

    private static ResultadoDeImputacion Repartir(PedidoDeImputacion pedido, decimal vueltas)
    {
        var orden = pedido.Orden == OrdenDeImputacion.ElegidoEnCaja
            ? pedido.Medios.ToList()
            : pedido.Medios.Where(m => m.Clase != PaymentMeansClass.Check).Concat(pedido.Medios.Where(m => m.Clase == PaymentMeansClass.Check)).ToList();
        var disponible = orden.Select(m => m.Valor).ToArray();

        // Las vueltas salen del efectivo, del último hacia atrás.
        var porDevolver = vueltas;
        for (var i = orden.Count - 1; i >= 0 && porDevolver > 0m; i--)
        {
            if (orden[i].Clase != PaymentMeansClass.Cash) continue;
            var sale = Math.Min(disponible[i], porDevolver);
            disponible[i] -= sale;
            porDevolver -= sale;
        }

        var porciones = new List<PorcionImputada>();
        var j = 0;
        foreach (var a in pedido.Aplicaciones)
        {
            var falta = a.Valor;
            while (falta > 0m)
            {
                while (j < orden.Count && disponible[j] <= 0m) j++;
                if (j >= orden.Count) return ResultadoDeImputacion.Fallo(CodigoMediosNoCuadran);
                var toma = Math.Min(falta, disponible[j]);
                porciones.Add(new PorcionImputada(a.Id, orden[j].Id, toma));
                disponible[j] -= toma;
                falta -= toma;
            }
        }
        return new ResultadoDeImputacion(porciones, vueltas, null);
    }

    private static ResultadoDeImputacion Validar(PedidoDeImputacion pedido, IReadOnlyList<PorcionImputada> fijada, decimal vueltas)
    {
        var aplicaciones = pedido.Aplicaciones.ToDictionary(a => a.Id);
        var medios = pedido.Medios.ToDictionary(m => m.Id);
        if (fijada.Any(p => p.Valor <= 0m || !aplicaciones.ContainsKey(p.Aplicacion) || !medios.ContainsKey(p.Medio)))
            return ResultadoDeImputacion.Fallo(CodigoImputacionInvalida);
        if (pedido.Aplicaciones.Any(a => fijada.Where(p => p.Aplicacion == a.Id).Sum(p => p.Valor) != a.Valor))
            return ResultadoDeImputacion.Fallo(CodigoImputacionInvalida);

        var sobranteDeEfectivo = 0m;
        foreach (var m in pedido.Medios)
        {
            var usado = fijada.Where(p => p.Medio == m.Id).Sum(p => p.Valor);
            if (usado > m.Valor) return ResultadoDeImputacion.Fallo(CodigoImputacionInvalida);
            if (m.Clase != PaymentMeansClass.Cash && usado != m.Valor) return ResultadoDeImputacion.Fallo(CodigoImputacionInvalida);
            if (m.Clase == PaymentMeansClass.Cash) sobranteDeEfectivo += m.Valor - usado;
        }
        return sobranteDeEfectivo != vueltas
            ? ResultadoDeImputacion.Fallo(CodigoImputacionInvalida)
            : new ResultadoDeImputacion(fijada.ToList(), vueltas, null);
    }
}
