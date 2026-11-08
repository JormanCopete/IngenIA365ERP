namespace IngenIA365ERP.Domain.Transport;

/// <summary>Cómo está un documento de un vehículo a una fecha de corte (feature 020, R7; FR-022).</summary>
public enum SituacionDeDocumento
{
    Vigente = 1,
    PorVencer = 2,
    Vencido = 3,

    /// <summary>Un tipo obligatorio sin documento.</summary>
    Falta = 4,
}

/// <summary>Un tipo de documento de la cooperativa, como lo necesita la clasificación.</summary>
public sealed record TipoParaVencimiento(int Id, string Codigo, string Nombre, bool Obligatorio, int DiasDeAviso);

/// <summary>El documento vigente de un vehículo para un tipo.</summary>
public sealed record DocumentoParaVencimiento(int TipoId, string Numero, DateOnly Vence);

/// <summary>Una fila del tablero: un vehículo, un tipo y su situación.</summary>
public sealed record VencimientoClasificado(TipoParaVencimiento Tipo, SituacionDeDocumento Situacion, DateOnly? Vence, string? Numero, int? DiasParaVencer);

/// <summary>
/// La clasificación pura de los documentos de un vehículo a una fecha de corte (feature 020, R7, T069; FR-022, FR-023): vencido si la
/// fecha de vencimiento ya pasó (vencer hoy todavía es vigente hoy), por vencer si vence dentro de los días de aviso del tipo, vigente si
/// no, y «falta» para un tipo obligatorio sin documento. Un tipo no obligatorio sin documento no aparece. <b>No bloquea nada</b>: sólo
/// informa.
/// </summary>
public static class VencimientosDeDocumentos
{
    public static IReadOnlyList<VencimientoClasificado> Clasificar(
        DateOnly corte, IReadOnlyCollection<TipoParaVencimiento> tipos, IReadOnlyCollection<DocumentoParaVencimiento> vigentes)
    {
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(vigentes);
        var porTipo = vigentes.GroupBy(d => d.TipoId).ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.Vence).First());
        var resultado = new List<VencimientoClasificado>(tipos.Count);
        foreach (var tipo in tipos.OrderBy(t => t.Nombre, StringComparer.CurrentCulture))
        {
            if (!porTipo.TryGetValue(tipo.Id, out var doc))
            {
                if (tipo.Obligatorio) resultado.Add(new VencimientoClasificado(tipo, SituacionDeDocumento.Falta, null, null, null));
                continue;
            }
            var dias = doc.Vence.DayNumber - corte.DayNumber;
            resultado.Add(new VencimientoClasificado(tipo, Situacion(dias, tipo.DiasDeAviso), doc.Vence, doc.Numero, dias));
        }
        return resultado;
    }

    /// <summary>La situación por los días que faltan para vencer (negativos = ya venció).</summary>
    public static SituacionDeDocumento Situacion(int diasParaVencer, int diasDeAviso) =>
        diasParaVencer < 0 ? SituacionDeDocumento.Vencido
        : diasParaVencer <= Math.Max(0, diasDeAviso) ? SituacionDeDocumento.PorVencer
        : SituacionDeDocumento.Vigente;

    public static string Texto(SituacionDeDocumento s) => s switch
    {
        SituacionDeDocumento.Vigente => "Vigente",
        SituacionDeDocumento.PorVencer => "Por vencer",
        SituacionDeDocumento.Vencido => "Vencido",
        SituacionDeDocumento.Falta => "Falta",
        _ => s.ToString(),
    };
}
