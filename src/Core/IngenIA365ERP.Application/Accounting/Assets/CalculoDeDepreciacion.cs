namespace IngenIA365ERP.Application.Accounting.Assets;

/// <summary>Un mes del calendario (año y mes); se compara y se resta en meses.</summary>
public readonly record struct Mes(int Year, int Month) : IComparable<Mes>
{
    public int Indice => Year * 12 + Month - 1;

    public static Mes De(DateOnly fecha) => new(fecha.Year, fecha.Month);

    public static Mes DeIndice(int indice) => new(indice / 12, indice % 12 + 1);

    public Mes Siguiente() => DeIndice(Indice + 1);

    public Mes Anterior() => DeIndice(Indice - 1);

    /// <summary>Meses de <paramref name="desde"/> a este, sin contar <paramref name="desde"/> (0 si es el mismo).</summary>
    public int MesesDesde(Mes desde) => Indice - desde.Indice;

    public int CompareTo(Mes other) => Indice.CompareTo(other.Indice);

    public static bool operator <(Mes a, Mes b) => a.Indice < b.Indice;
    public static bool operator >(Mes a, Mes b) => a.Indice > b.Indice;
    public static bool operator <=(Mes a, Mes b) => a.Indice <= b.Indice;
    public static bool operator >=(Mes a, Mes b) => a.Indice >= b.Indice;

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}

/// <summary>Lo que el cálculo necesita del activo: costo, residual, vida útil, la historia con que entró al ERP y su primer mes.</summary>
public sealed record ActivoParaCalculo(decimal Cost, decimal ResidualValue, int LifeMonths, decimal OpeningAccumulated, int OpeningElapsedMonths, Mes Inicio);

/// <summary>Una cuota contabilizada y no reversada: el mes de su período y su valor.</summary>
public sealed record CuotaContabilizada(Mes Mes, decimal Amount);

/// <summary>Una fila de la proyección: mes, cuota, acumulada después de la cuota y valor neto en libros.</summary>
public sealed record CuotaProyectada(Mes Mes, decimal Amount, decimal Accumulated, decimal NetBookValue);

/// <summary>
/// La depreciación y amortización en línea recta (feature 009 E4, US12, FR-076..FR-078). Es puro: no guarda cuotas pendientes, las
/// calcula. La cuota de un mes es lo que falta por depreciar (costo − residual − acumulada) repartido en la vida útil que queda,
/// multiplicado por los meses que la corrida cubre (más de uno si hubo meses sin correr: un activo comprado en un período ya cerrado
/// acumula en su primera cuota). Por eso cambiar vida útil o residual es prospectivo sin regenerar nada: las cuotas contabilizadas
/// no cambian y las siguientes salen del valor neto en libros. En la última cuota (o cuando la vida útil se agotó) va todo el saldo,
/// así el redondeo no deja centavos. (nuevo)
/// </summary>
public static class CalculoDeDepreciacion
{
    /// <summary>El último mes cubierto: el de la última cuota contabilizada, o el anterior al inicio si no hay ninguna.</summary>
    public static Mes UltimoMesCubierto(ActivoParaCalculo activo, IReadOnlyCollection<CuotaContabilizada> contabilizadas) =>
        contabilizadas.Count == 0 ? activo.Inicio.Anterior() : contabilizadas.Max(c => c.Mes);

    /// <summary>Acumulada: la que traía al entrar más las cuotas contabilizadas.</summary>
    public static decimal Acumulada(ActivoParaCalculo activo, IReadOnlyCollection<CuotaContabilizada> contabilizadas) =>
        activo.OpeningAccumulated + contabilizadas.Sum(c => c.Amount);

    /// <summary>Meses de vida útil consumidos hasta el último mes cubierto.</summary>
    public static int MesesConsumidos(ActivoParaCalculo activo, IReadOnlyCollection<CuotaContabilizada> contabilizadas) =>
        activo.OpeningElapsedMonths + Math.Max(0, UltimoMesCubierto(activo, contabilizadas).MesesDesde(activo.Inicio) + 1);

    /// <summary>Lo que falta por depreciar (nunca negativo).</summary>
    public static decimal PorDepreciar(ActivoParaCalculo activo, IReadOnlyCollection<CuotaContabilizada> contabilizadas) =>
        Math.Max(0m, activo.Cost - activo.ResidualValue - Acumulada(activo, contabilizadas));

    /// <summary>La cuota del mes <paramref name="mes"/>: cero si el activo no ha empezado, ya lo cubre o ya no tiene nada por depreciar.</summary>
    public static decimal CuotaDe(ActivoParaCalculo activo, IReadOnlyCollection<CuotaContabilizada> contabilizadas, Mes mes)
    {
        if (mes < activo.Inicio) return 0m;
        var meses = mes.MesesDesde(UltimoMesCubierto(activo, contabilizadas));
        if (meses <= 0) return 0m;
        var pendiente = PorDepreciar(activo, contabilizadas);
        if (pendiente <= 0m) return 0m;
        var restantes = activo.LifeMonths - MesesConsumidos(activo, contabilizadas);
        if (restantes <= meses) return pendiente;
        return Math.Round(pendiente * meses / restantes, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Las cuotas que faltan, mes a mes desde el siguiente al último cubierto, hasta agotar lo depreciable.</summary>
    public static IReadOnlyList<CuotaProyectada> Proyeccion(ActivoParaCalculo activo, IReadOnlyCollection<CuotaContabilizada> contabilizadas)
    {
        var historia = contabilizadas.ToList();
        var filas = new List<CuotaProyectada>();
        var mes = UltimoMesCubierto(activo, historia).Siguiente();
        // La vida útil acota la vuelta: con lo pendiente en cero o la vida agotada, CuotaDe entrega todo y se sale.
        for (var vuelta = 0; vuelta <= activo.LifeMonths + 1 && PorDepreciar(activo, historia) > 0m; vuelta++, mes = mes.Siguiente())
        {
            var cuota = CuotaDe(activo, historia, mes);
            if (cuota <= 0m) break;
            historia.Add(new CuotaContabilizada(mes, cuota));
            var acumulada = Acumulada(activo, historia);
            filas.Add(new CuotaProyectada(mes, cuota, acumulada, activo.Cost - acumulada));
        }
        return filas;
    }
}
