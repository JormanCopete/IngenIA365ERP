using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>
/// Por qué pide el deudor la novedad (api.md §11.1, <c>requestMotive</c>): por dificultades de pago o por su propia conveniencia. No es
/// columna: va a <c>CauseDeterminationJson</c>; la causa la calcula el sistema.
/// </summary>
public enum NoveltyRequestMotive
{
    Hardship = 1,
    DebtorRequest = 2,
}

/// <summary>La mora máxima de un mes del historial reciente (de <c>HistorialDeMora</c> o de la reconstrucción del mes en curso).</summary>
public sealed record MoraDelMes(int Anio, int Mes, int MaxDiasDeMora);

/// <summary>
/// Lo que el clasificador necesita de una novedad: el mecanismo, el motivo, el segmento SARC del crédito principal, su mora hoy, la fecha
/// de la solicitud, el historial de mora reciente y las marcas que la hacen reestructuración sin mirar nada más (acuerdo de insolvencia,
/// refinanciación que recoge un crédito en mora).
/// </summary>
public sealed record NovedadParaClasificar(
    NoveltyMechanism Mecanismo,
    NoveltyRequestMotive Motivo,
    string SegmentoSarc,
    int DiasDeMoraHoy,
    DateOnly Fecha)
{
    public IReadOnlyList<MoraDelMes> Historial { get; init; } = [];

    /// <summary>La novedad es el acuerdo de un proceso de insolvencia (FR-098).</summary>
    public bool AcuerdoDeInsolvencia { get; init; }

    /// <summary>Una refinanciación que recoge al menos un crédito en mora (FR-046, «recogida en mora»).</summary>
    public bool RecogeCreditosEnMora { get; init; }
}

/// <summary>
/// La clasificación: la causa, el problema si la petición no se admite como modificación (<see cref="CodigoModificacionNoAdmitida"/>, con
/// <c>recentDaysPastDue</c> y <c>limit</c>), la mora reciente que se miró y el límite, la explicación y el JSON canónico que va a
/// <c>CauseDeterminationJson</c>.
/// </summary>
public sealed record ClasificacionDeNovedad(
    NoveltyCause Causa,
    ProblemaDeCartera? Problema,
    int MoraReciente,
    int? Limite,
    ExplicacionDeCartera Explicacion,
    string DeterminacionJson)
{
    /// <summary>¿Se admite? Una reestructuración siempre; una modificación sólo dentro del límite.</summary>
    public bool Admitida => Problema is null;
}

/// <summary>
/// El clasificador de novedades (feature 013, K3, T0873; FR-090): la causa la da el <b>motivo</b>, no el mecanismo.
/// <list type="bullet">
/// <item><b>Reestructuración</b>, cualquiera sea el mecanismo, si el crédito está en mora hoy, si el deudor la pide por dificultades de pago,
/// si es el acuerdo de un proceso de insolvencia o si es una refinanciación que recoge un crédito en mora.</item>
/// <item><b>Modificación</b> en otro caso —a petición del deudor y sin deterioro—, sólo si la mora máxima de los últimos meses que fija el
/// Anexo 1 (<see cref="ReglaDeModificaciones.MesesDeObservacion"/>, contando el de la solicitud) no pasa del límite del segmento. Si pasa, o
/// si la norma vigente no trae la regla o el límite del segmento, no se admite (<see cref="CodigoModificacionNoAdmitida"/>): se tramita como
/// reestructuración.</item>
/// </list>
/// Puro y sin valores legales: los meses y los días llegan en el Anexo 1 tipado (<c>LaCarteraNoTieneValoresLegalesFijos</c>). (nuevo)
/// </summary>
public static class ClasificadorDeNovedad
{
    /// <summary>La petición no cabe como modificación: el historial reciente pasa del límite de la norma (api.md §11.1).</summary>
    public const string CodigoModificacionNoAdmitida = "Lending.Novelty.ModificationNotAllowed";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static ClasificacionDeNovedad Clasificar(Anexo1 norma, NovedadParaClasificar n)
    {
        ArgumentNullException.ThrowIfNull(norma);
        ArgumentNullException.ThrowIfNull(n);
        if (n.DiasDeMoraHoy < 0) throw new ArgumentOutOfRangeException(nameof(n), n.DiasDeMoraHoy, "Los días de mora no son negativos.");

        var explicacion = new ExplicacionDeCartera()
            .Linea($"Novedad {n.Mecanismo} pedida el {ExplicacionDeCartera.Fecha(n.Fecha)} por {n.Motivo}; segmento {n.SegmentoSarc}, "
                + $"{n.DiasDeMoraHoy.ToString(Inv)} días de mora hoy.");

        var motivos = new List<string>();
        if (n.DiasDeMoraHoy > 0) motivos.Add("InArrears");
        if (n.Motivo == NoveltyRequestMotive.Hardship) motivos.Add("Hardship");
        if (n.AcuerdoDeInsolvencia) motivos.Add("InsolvencyAgreement");
        if (n.RecogeCreditosEnMora) motivos.Add("CollectsLoansInArrears");

        var regla = norma.Modificaciones;
        var meses = regla?.MesesDeObservacion;
        var reciente = MoraReciente(n, meses);

        if (motivos.Count > 0)
        {
            explicacion.Linea("Reestructuración por la causa, cualquiera sea el mecanismo (FR-090): " + string.Join(", ", motivos.Select(Texto)) + ".");
            return Cerrar(NoveltyCause.Restructuring, null, n, motivos, reciente, null, meses, explicacion);
        }

        var limite = regla?.LimiteDe(n.SegmentoSarc);
        if (regla is null || limite is null)
        {
            var texto = regla is null
                ? "el Anexo 1 vigente no trae la regla de las modificaciones"
                : $"el Anexo 1 vigente no fija el límite de las modificaciones en {n.SegmentoSarc}";
            explicacion.Linea($"A petición del deudor y sin deterioro, pero {texto}: no se admite como modificación; se tramita como reestructuración.");
            var problema = new ProblemaDeCartera(CodigoModificacionNoAdmitida,
                $"La norma vigente no permite tramitar esta novedad como modificación ({texto}); tramítela como reestructuración.",
                new Dictionary<string, object?> { ["recentDaysPastDue"] = reciente, ["limit"] = null });
            return Cerrar(NoveltyCause.Modification, problema, n, motivos, reciente, null, meses, explicacion);
        }

        var textoVentana = $"la mora máxima de los últimos {regla.MesesDeObservacion.ToString(Inv)} meses es {reciente.ToString(Inv)} días";
        if (reciente > limite.MaxDiasDeMora)
        {
            explicacion.Linea($"A petición del deudor y sin deterioro, pero {textoVentana} y el límite de {n.SegmentoSarc} es "
                + $"{limite.MaxDiasDeMora.ToString(Inv)}: no se admite como modificación; se tramita como reestructuración.");
            var problema = new ProblemaDeCartera(CodigoModificacionNoAdmitida,
                $"El historial de mora reciente ({reciente.ToString(Inv)} días) pasa del límite de la norma para una modificación "
                + $"({limite.MaxDiasDeMora.ToString(Inv)} días): tramítela como reestructuración.",
                new Dictionary<string, object?> { ["recentDaysPastDue"] = reciente, ["limit"] = limite.MaxDiasDeMora });
            return Cerrar(NoveltyCause.Modification, problema, n, motivos, reciente, limite.MaxDiasDeMora, meses, explicacion);
        }

        explicacion.Linea($"Modificación: a petición del deudor, sin deterioro, y {textoVentana}, dentro del límite de {n.SegmentoSarc} "
            + $"({limite.MaxDiasDeMora.ToString(Inv)})"
            + (regla.DiasParaVolverAReestructuracion is { } r
                ? $"; si bajo las condiciones nuevas pasa de {r.ToString(Inv)} días de mora, vuelve a ser reestructuración."
                : "."));
        return Cerrar(NoveltyCause.Modification, null, n, motivos, reciente, limite.MaxDiasDeMora, meses, explicacion);
    }

    /// <summary>La mora máxima de la ventana: los meses del historial dentro de los últimos <paramref name="meses"/> y la de hoy.</summary>
    private static int MoraReciente(NovedadParaClasificar n, int? meses)
    {
        var hoy = n.Fecha.Year * 12 + n.Fecha.Month - 1;
        var desde = meses is { } m ? hoy - m + 1 : hoy;
        var historial = n.Historial.Where(h => h.Anio * 12 + h.Mes - 1 >= desde && h.Anio * 12 + h.Mes - 1 <= hoy).Select(h => h.MaxDiasDeMora);
        return historial.Append(n.DiasDeMoraHoy).Max();
    }

    private static ClasificacionDeNovedad Cerrar(NoveltyCause causa, ProblemaDeCartera? problema, NovedadParaClasificar n, List<string> motivos,
        int reciente, int? limite, int? meses, ExplicacionDeCartera explicacion)
    {
        var json = new HuellaDeCartera()
            .Enumeracion<NoveltyCause>("cause", causa)
            .Enumeracion<NoveltyMechanism>("mechanism", n.Mecanismo)
            .Enumeracion<NoveltyRequestMotive>("requestMotive", n.Motivo)
            .Texto("riskSegment", n.SegmentoSarc)
            .Fecha("requestedOn", n.Fecha)
            .Entero("daysPastDue", n.DiasDeMoraHoy)
            .Entero("recentDaysPastDue", reciente)
            .Entero("lookbackMonths", meses)
            .Entero("limit", limite)
            .Textos("restructuringReasons", motivos)
            .Logico("admitted", problema is null)
            .Texto("explanation", explicacion.Texto)
            .Canonico();
        return new(causa, problema, reciente, limite, explicacion, json);
    }

    private static string Texto(string motivo) => motivo switch
    {
        "InArrears" => "el crédito está en mora",
        "Hardship" => "la pide por dificultades de pago",
        "InsolvencyAgreement" => "es el acuerdo de un proceso de insolvencia",
        "CollectsLoansInArrears" => "recoge créditos en mora",
        _ => motivo,
    };
}
