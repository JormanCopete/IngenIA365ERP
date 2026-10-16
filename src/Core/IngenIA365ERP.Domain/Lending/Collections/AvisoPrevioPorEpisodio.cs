using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Collections;

/// <summary>Los días de mora de un crédito un día, como los da la reconstrucción a (D, R) (T24) o la posición diaria. (nuevo)</summary>
public sealed record MoraDelDia(DateOnly Dia, int Dias);

/// <summary>
/// Un episodio de mora: <see cref="Inicio"/> es el vencimiento de la primera cuota impaga del episodio (<c>EpisodeStartDate</c>), el primer
/// día medido en mora y, si el crédito se puso al día, el primer día medido sin mora. (nuevo)
/// </summary>
public sealed record EpisodioDeMora(DateOnly Inicio, DateOnly PrimerDiaEnMora, DateOnly? CerradoEl)
{
    public bool Abierto => CerradoEl is null;
}

/// <summary>Una constancia de una comunicación del aviso: el día local del <c>Sent</c> (o del registro de la carta) y si salió por <c>SIMULADO</c>. (nuevo)</summary>
public sealed record ConstanciaDeAviso(DateOnly Dia, bool Simulada = false);

/// <summary>
/// Lo que el cálculo mira de un aviso previo (<c>LND_PriorNotices</c>, data-model §26.11): el episodio, las comunicaciones exigidas y los
/// días mínimos sellados, y lo migrado (fechas de envío y reporte vigente en centrales). (nuevo)
/// </summary>
public sealed record AvisoParaEvaluar(DateOnly InicioDelEpisodio, int Exigidas, int DiasMinimos)
{
    public DateOnly? MigradoPrimerEnvio { get; init; }
    public DateOnly? MigradoSegundoEnvio { get; init; }
    public DateOnly? ReportadoNegativoDesde { get; init; }
}

/// <summary>
/// El estado derivado de un aviso previo: el estado, los días distintos con constancia válida, la fecha más temprana en que el dato negativo
/// puede ir al archivo de centrales (nula mientras no está completo) y el cierre del episodio. (nuevo)
/// </summary>
public sealed record EvaluacionDeAviso(PriorNoticeStatus Estado, IReadOnlyList<DateOnly> DiasConConstancia, DateOnly? FechaMasTempranaDeReporte, DateOnly? CerradoEl)
{
    /// <summary>¿El dato negativo de este episodio puede ir al archivo de centrales el <paramref name="dia"/>?</summary>
    public bool PermiteReportar(DateOnly dia) => Estado == PriorNoticeStatus.Valid && FechaMasTempranaDeReporte is { } f && dia >= f;
}

/// <summary>
/// Motor puro del aviso previo al reporte negativo por episodio de mora (feature 013, K4, T0937; FR-123, SC-012, T77; Leyes 1266 de 2008 y
/// 2157 de 2021; US12-5). Sin valores legales: los días mínimos y el umbral llegan sellados o del parámetro vigente.
/// <list type="bullet">
/// <item><b>Episodio</b> (<see cref="Episodios"/>): una racha de días medidos con mora; empieza en el vencimiento de la primera cuota impaga
/// (<c>día − días de mora</c> del primer día de la racha) y se cierra el primer día medido sin mora. Recaer es un episodio nuevo, con su
/// propio aviso.</item>
/// <item><b>Comunicaciones exigidas</b> (<see cref="ComunicacionesExigidas"/>): dos, en días distintos, si el saldo no supera el umbral; si
/// no, una.</item>
/// <item><b>Constancia</b>: cada día distinto con un envío real; en producción un <c>SIMULADO</c> no es constancia. Lo migrado cuenta: sus
/// fechas de envío son constancias y un reporte negativo vigente desde la migración deja el aviso válido desde ese día.</item>
/// <item><b>Estado</b>: <c>Closed</c> si el episodio se cerró; <c>Valid</c> con las exigidas; <c>InProgress</c> con alguna;
/// <c>Pending</c> sin ninguna. Fecha más temprana de reporte = el día de la última constancia exigida + los días mínimos.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class AvisoPrevioPorEpisodio
{
    /// <summary>
    /// Los episodios de una serie de días medidos (se ordena por día). Un día con más de cero días de mora está en mora; uno con cero o menos,
    /// al día. Entre dos días medidos rige el primero.
    /// </summary>
    public static IReadOnlyList<EpisodioDeMora> Episodios(IEnumerable<MoraDelDia> serie)
    {
        ArgumentNullException.ThrowIfNull(serie);
        var episodios = new List<EpisodioDeMora>();
        DateOnly? inicio = null;
        DateOnly primerDia = default;
        foreach (var m in serie.OrderBy(m => m.Dia))
        {
            if (m.Dias > 0)
            {
                if (inicio is null)
                {
                    inicio = m.Dia.AddDays(-m.Dias);
                    primerDia = m.Dia;
                }
            }
            else if (inicio is { } i)
            {
                episodios.Add(new EpisodioDeMora(i, primerDia, m.Dia));
                inicio = null;
            }
        }
        if (inicio is { } abierto) episodios.Add(new EpisodioDeMora(abierto, primerDia, null));
        return episodios;
    }

    /// <summary>El episodio abierto de la serie, o nulo si el crédito está al día en su último día medido.</summary>
    public static EpisodioDeMora? EpisodioVigente(IEnumerable<MoraDelDia> serie) => Episodios(serie).LastOrDefault(e => e.Abierto);

    /// <summary>Dos comunicaciones si el saldo de la obligación no supera el umbral en pesos; si no, una.</summary>
    public static int ComunicacionesExigidas(decimal saldo, decimal umbral) => saldo <= umbral ? 2 : 1;

    public static EvaluacionDeAviso Evaluar(AvisoParaEvaluar aviso, IEnumerable<ConstanciaDeAviso> constancias, EpisodioDeMora? episodio, bool enProduccion)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        ArgumentNullException.ThrowIfNull(constancias);
        if (aviso.Exigidas < 1) throw new ArgumentOutOfRangeException(nameof(aviso), aviso.Exigidas, "El aviso exige al menos una comunicación.");
        if (aviso.DiasMinimos < 0) throw new ArgumentOutOfRangeException(nameof(aviso), aviso.DiasMinimos, "Los días mínimos no pueden ser negativos.");
        if (episodio is not null && episodio.Inicio != aviso.InicioDelEpisodio)
            throw new ArgumentException("El episodio no es el del aviso.", nameof(episodio));

        var dias = constancias.Where(c => !(enProduccion && c.Simulada)).Select(c => c.Dia)
            .Concat(new[] { aviso.MigradoPrimerEnvio, aviso.MigradoSegundoEnvio }.Where(d => d is not null).Select(d => d!.Value))
            .Distinct().Order().ToList();

        if (episodio?.CerradoEl is { } cerrado)
            return new EvaluacionDeAviso(PriorNoticeStatus.Closed, dias, null, cerrado);

        if (aviso.ReportadoNegativoDesde is { } reportado)
            return new EvaluacionDeAviso(PriorNoticeStatus.Valid, dias, reportado, null);

        if (dias.Count >= aviso.Exigidas)
            return new EvaluacionDeAviso(PriorNoticeStatus.Valid, dias, dias[aviso.Exigidas - 1].AddDays(aviso.DiasMinimos), null);

        return new EvaluacionDeAviso(dias.Count > 0 ? PriorNoticeStatus.InProgress : PriorNoticeStatus.Pending, dias, null, null);
    }
}
