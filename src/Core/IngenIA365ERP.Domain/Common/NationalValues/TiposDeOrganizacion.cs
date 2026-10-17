using System.Text.Json;

namespace IngenIA365ERP.Domain.Common.NationalValues;

/// <summary>Qué dice el tipo de organización sobre el modelo de pérdida esperada: no lo admite, lo admite (voluntario) o lo exige.</summary>
public enum PerdidaEsperadaDelTipo
{
    NotAdmitted = 1,
    Admitted = 2,
    Required = 3,
}

/// <summary>Un tipo de organización solidaria de la taxonomía del supervisor.</summary>
public sealed record TipoDeOrganizacion(string Code, string Name, bool IsEmployeeFund, PerdidaEsperadaDelTipo ExpectedLoss, bool Verified)
{
    /// <summary>
    /// Feature 013, K3 (US14, T0910; FR-095): si el tipo debe reportar los castigos al supervisor y en cuántos días calendario desde que se aplican.
    /// Nulo: el documento no lo dice (no se exige reporte). Dato del documento, «pendiente de validar» con el resto del tipo (<see cref="Verified"/>).
    /// </summary>
    public ReporteDeCastigos? WriteOffReport { get; init; }

    /// <summary>
    /// Feature 013, K3 (US14, T0912; FR-102): la periodicidad de la evaluación de cartera del tipo —los meses de corte (6 y 12, semestral; 12, anual) y
    /// cuántos meses después del corte se refleja en el cierre—. Nulo: el documento no lo dice (la evaluación no se valida contra él).
    /// </summary>
    public PeriodicidadDeEvaluacion? PortfolioEvaluation { get; init; }

    /// <summary>
    /// Feature 013, K5 (US16, T1002, T1007; FR-136): el formato de cartera del SICSES según el nivel de supervisión del perfil —en qué meses se corta,
    /// cuántos días calendario después del fin del mes vence la entrega y cuántos días antes se avisa—. Nulo o sin el nivel: el documento no lo dice
    /// (el SICSES se genera igual, pero no hay plazo que vigilar). Dato del documento, «pendiente de validar» con el resto del tipo.
    /// </summary>
    public IReadOnlyList<ReporteSicsesPorNivel>? SicsesReport { get; init; }

    /// <summary>El reporte del SICSES de un nivel de supervisión, o nulo si el documento no lo dice.</summary>
    public ReporteSicsesPorNivel? SicsesDelNivel(int nivel) => SicsesReport?.FirstOrDefault(r => r.SupervisionLevel == nivel);
}

/// <summary>
/// El formato de cartera del SICSES de un nivel de supervisión (K5): los meses de corte, el plazo en días calendario desde el último día del mes cortado
/// y los días de aviso antes del plazo (<c>Cartera.PlazoRegulatorioPorVencer</c>). (nuevo)
/// </summary>
public sealed record ReporteSicsesPorNivel(int SupervisionLevel, IReadOnlyList<int> CutoffMonths, int DueDays, int WarningDays)
{
    /// <summary>¿Se reporta el mes <paramref name="mes"/>?</summary>
    public bool SeReporta(int mes) => CutoffMonths.Contains(mes);

    /// <summary>El último día para entregar el corte de <paramref name="anio"/>-<paramref name="mes"/>: el fin del mes más los días del plazo.</summary>
    public DateOnly Limite(int anio, int mes) => new DateOnly(anio, mes, 1).AddMonths(1).AddDays(-1).AddDays(DueDays);

    /// <summary>Desde qué día se avisa que vence el corte de <paramref name="anio"/>-<paramref name="mes"/>.</summary>
    public DateOnly AvisoDesde(int anio, int mes) => Limite(anio, mes).AddDays(-WarningDays);
}

/// <summary>El reporte de castigos al supervisor de un tipo de organización: si es obligatorio y su plazo en días calendario (nulo: sin plazo fijado).</summary>
public sealed record ReporteDeCastigos(bool Required, int? DueDays);

/// <summary>La periodicidad de la evaluación de cartera: los meses del año en que se corta y los meses hasta el cierre que refleja la recalificación.</summary>
public sealed record PeriodicidadDeEvaluacion(IReadOnlyList<int> CutoffMonths, int EffectMonthsAfterCutoff)
{
    /// <summary>¿El corte cae en un mes de corte del tipo y el mes de efecto es el que le corresponde?</summary>
    public bool Admite(DateOnly corte, int anioDeEfecto, int mesDeEfecto) =>
        CutoffMonths.Contains(corte.Month)
        && anioDeEfecto * 12 + mesDeEfecto == corte.Year * 12 + corte.Month + EffectMonthsAfterCutoff;
}

/// <summary>Una categoría de los fondos de empleados.</summary>
public sealed record CategoriaDeFondoDeEmpleados(string Code, string Name);

/// <summary>
/// Documento normativo <see cref="RegulatoryDocumentKind.TiposDeOrganizacion"/> tipado (feature 013, T45, T50, R15;
/// data-model parte 3 §4.5, §12.4). El perfil regulatorio de la cooperativa guarda un <c>OrganizationTypeCode</c> de este
/// documento (no es enum: la taxonomía del supervisor cambia) y, si es fondo de empleados, su categoría.
///
/// <para>Esquema 1 (camelCase): <c>{ employeeFundCategories: [ { code, name } ], organizationTypes: [ { code, name,
/// isEmployeeFund, expectedLoss: NotAdmitted | Admitted | Required, verified?, writeOffReport?: { required, dueDays? },
/// portfolioEvaluation?: { cutoffMonths: [1..12], effectMonthsAfterCutoff }, sicsesReport?: [ { supervisionLevel: 1..3, cutoffMonths: [1..12],
/// dueDays, warningDays } ] } ] }</c>. Códigos únicos; si algún tipo es
/// fondo de empleados, las categorías no pueden faltar. <c>writeOffReport</c> y <c>portfolioEvaluation</c> son opcionales (K3, US14: sin
/// cambio de versión de esquema); un plazo obligatorio no puede faltar si el reporte es obligatorio y los meses de corte van del 1 al 12.
/// <c>sicsesReport</c> es opcional (K5, US16; tampoco cambia la versión): un nivel por fila, sin repetirse, plazo de más de cero días y aviso de cero o
/// más.</para>
/// </summary>
public sealed class TiposDeOrganizacion
{
    public const short VersionDeEsquemaVigente = 1;

    public static IReadOnlyList<short> VersionesDeEsquema { get; } = [1];

    private TiposDeOrganizacion(IReadOnlyList<TipoDeOrganizacion> tipos, IReadOnlyList<CategoriaDeFondoDeEmpleados> categorias)
    {
        Tipos = tipos;
        CategoriasDeFondoDeEmpleados = categorias;
    }

    public IReadOnlyList<TipoDeOrganizacion> Tipos { get; }

    public IReadOnlyList<CategoriaDeFondoDeEmpleados> CategoriasDeFondoDeEmpleados { get; }

    public TipoDeOrganizacion? Buscar(string? codigo) =>
        Tipos.FirstOrDefault(t => string.Equals(t.Code, codigo, StringComparison.Ordinal));

    /// <summary>
    /// Si la categoría declarada es válida para el tipo: un fondo de empleados declara una categoría del documento; los
    /// demás tipos no declaran ninguna. Un tipo inexistente nunca es válido.
    /// </summary>
    public bool CategoriaValida(string? tipo, string? categoria)
    {
        var t = Buscar(tipo);
        if (t is null) return false;
        if (!t.IsEmployeeFund) return categoria is null;
        return categoria is not null && CategoriasDeFondoDeEmpleados.Any(c => string.Equals(c.Code, categoria, StringComparison.Ordinal));
    }

    public static ResultadoDeDocumento<TiposDeOrganizacion> Leer(string contenido, short versionDeEsquema)
    {
        var errores = new List<ErrorDeDocumentoNormativo>();
        if (!LecturaDeDocumento.VersionSoportada(versionDeEsquema, VersionesDeEsquema, errores))
            return new(null, errores);

        using var doc = LecturaDeDocumento.Abrir(contenido, errores);
        if (doc is null) return new(null, errores);

        var categorias = new List<CategoriaDeFondoDeEmpleados>();
        var codigosCategoria = new HashSet<string>(StringComparer.Ordinal);
        var listaCategorias = LecturaDeDocumento.Arreglo(doc.RootElement, "employeeFundCategories", "$", noVacio: false, errores);
        for (var i = 0; i < listaCategorias.Count; i++)
        {
            var ruta = $"$.employeeFundCategories[{i}]";
            if (listaCategorias[i].ValueKind != JsonValueKind.Object)
            {
                errores.Add(new(ruta, "Cada categoría es un objeto."));
                continue;
            }
            var code = Codigo(listaCategorias[i], ruta, codigosCategoria, "La categoría", errores);
            var name = LecturaDeDocumento.Texto(listaCategorias[i], "name", ruta, requerido: true, errores);
            categorias.Add(new(code ?? string.Empty, name ?? string.Empty));
        }

        var tipos = new List<TipoDeOrganizacion>();
        var codigosTipo = new HashSet<string>(StringComparer.Ordinal);
        var listaTipos = LecturaDeDocumento.Arreglo(doc.RootElement, "organizationTypes", "$", noVacio: true, errores);
        for (var i = 0; i < listaTipos.Count; i++)
        {
            var ruta = $"$.organizationTypes[{i}]";
            var t = listaTipos[i];
            if (t.ValueKind != JsonValueKind.Object)
            {
                errores.Add(new(ruta, "Cada tipo de organización es un objeto."));
                continue;
            }
            var code = Codigo(t, ruta, codigosTipo, "El tipo", errores);
            var name = LecturaDeDocumento.Texto(t, "name", ruta, requerido: true, errores);
            var fondo = LecturaDeDocumento.Booleano(t, "isEmployeeFund", ruta, false, errores);
            var textoPerdida = LecturaDeDocumento.Texto(t, "expectedLoss", ruta, requerido: true, errores);
            if (textoPerdida is not null && !Enum.GetNames<PerdidaEsperadaDelTipo>().Contains(textoPerdida, StringComparer.Ordinal))
                errores.Add(new($"{ruta}.expectedLoss", $"«{textoPerdida}» no es válido; se admiten: {string.Join(", ", Enum.GetNames<PerdidaEsperadaDelTipo>())}."));
            var perdida = textoPerdida is not null && Enum.TryParse<PerdidaEsperadaDelTipo>(textoPerdida, out var p) ? p : PerdidaEsperadaDelTipo.NotAdmitted;
            var verificado = LecturaDeDocumento.Booleano(t, "verified", ruta, false, errores);
            tipos.Add(new(code ?? string.Empty, name ?? string.Empty, fondo, perdida, verificado)
            {
                WriteOffReport = Reporte(t, ruta, errores),
                PortfolioEvaluation = Evaluacion(t, ruta, errores),
                SicsesReport = Sicses(t, ruta, errores),
            });
        }

        if (tipos.Any(t => t.IsEmployeeFund) && categorias.Count == 0)
            errores.Add(new("$.employeeFundCategories", "Hay fondos de empleados: el documento debe declarar sus categorías."));

        return errores.Count > 0 ? new(null, errores) : new(new TiposDeOrganizacion(tipos, categorias), errores);
    }

    private static ReporteDeCastigos? Reporte(JsonElement tipo, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (!tipo.TryGetProperty("writeOffReport", out var r) || r.ValueKind == JsonValueKind.Null) return null;
        var aqui = $"{ruta}.writeOffReport";
        if (r.ValueKind != JsonValueKind.Object)
        {
            errores.Add(new(aqui, "El reporte de castigos es un objeto { required, dueDays }."));
            return null;
        }
        var obligatorio = LecturaDeDocumento.Booleano(r, "required", aqui, false, errores);
        int? dias = null;
        if (r.TryGetProperty("dueDays", out var d) && d.ValueKind != JsonValueKind.Null)
        {
            if (d.ValueKind == JsonValueKind.Number && d.TryGetInt32(out var n) && n > 0) dias = n;
            else errores.Add(new($"{aqui}.dueDays", "El plazo del reporte es un número entero de días mayor que cero."));
        }
        if (obligatorio && dias is null)
            errores.Add(new($"{aqui}.dueDays", "Un reporte de castigos obligatorio lleva su plazo en días."));
        return new ReporteDeCastigos(obligatorio, dias);
    }

    private static PeriodicidadDeEvaluacion? Evaluacion(JsonElement tipo, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (!tipo.TryGetProperty("portfolioEvaluation", out var e) || e.ValueKind == JsonValueKind.Null) return null;
        var aqui = $"{ruta}.portfolioEvaluation";
        if (e.ValueKind != JsonValueKind.Object)
        {
            errores.Add(new(aqui, "La evaluación de cartera es un objeto { cutoffMonths, effectMonthsAfterCutoff }."));
            return null;
        }
        var meses = new List<int>();
        foreach (var m in LecturaDeDocumento.Arreglo(e, "cutoffMonths", aqui, noVacio: true, errores))
        {
            if (m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out var mes) && mes is >= 1 and <= 12 && !meses.Contains(mes)) meses.Add(mes);
            else errores.Add(new($"{aqui}.cutoffMonths", "Los meses de corte van del 1 al 12, sin repetirse."));
        }
        var efecto = 0;
        if (!e.TryGetProperty("effectMonthsAfterCutoff", out var x) || x.ValueKind != JsonValueKind.Number || !x.TryGetInt32(out efecto) || efecto < 0)
            errores.Add(new($"{aqui}.effectMonthsAfterCutoff", "Los meses entre el corte y el cierre que refleja la evaluación son un entero de cero o más."));
        return new PeriodicidadDeEvaluacion(meses, efecto);
    }

    private static IReadOnlyList<ReporteSicsesPorNivel>? Sicses(JsonElement tipo, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (!tipo.TryGetProperty("sicsesReport", out var s) || s.ValueKind == JsonValueKind.Null) return null;
        var aqui = $"{ruta}.sicsesReport";
        if (s.ValueKind != JsonValueKind.Array)
        {
            errores.Add(new(aqui, "El reporte del SICSES es una lista de { supervisionLevel, cutoffMonths, dueDays, warningDays }."));
            return null;
        }
        var lista = new List<ReporteSicsesPorNivel>();
        var i = 0;
        foreach (var r in s.EnumerateArray())
        {
            var donde = $"{aqui}[{i++}]";
            if (r.ValueKind != JsonValueKind.Object)
            {
                errores.Add(new(donde, "Cada nivel del reporte del SICSES es un objeto."));
                continue;
            }
            var nivel = Entero(r, "supervisionLevel", donde, n => n is >= 1 and <= 3, "El nivel de supervisión es 1, 2 o 3.", errores);
            if (nivel > 0 && lista.Any(x => x.SupervisionLevel == nivel))
                errores.Add(new($"{donde}.supervisionLevel", $"El nivel {nivel} está repetido."));
            var meses = new List<int>();
            foreach (var m in LecturaDeDocumento.Arreglo(r, "cutoffMonths", donde, noVacio: true, errores))
            {
                if (m.ValueKind == JsonValueKind.Number && m.TryGetInt32(out var mes) && mes is >= 1 and <= 12 && !meses.Contains(mes)) meses.Add(mes);
                else errores.Add(new($"{donde}.cutoffMonths", "Los meses de corte van del 1 al 12, sin repetirse."));
            }
            var plazo = Entero(r, "dueDays", donde, n => n > 0, "El plazo del SICSES es un número entero de días mayor que cero.", errores);
            var aviso = Entero(r, "warningDays", donde, n => n >= 0, "Los días de aviso son un entero de cero o más.", errores);
            lista.Add(new ReporteSicsesPorNivel(nivel, meses, plazo, aviso));
        }
        return lista;
    }

    private static int Entero(JsonElement objeto, string propiedad, string ruta, Func<int, bool> valido, string mensaje, List<ErrorDeDocumentoNormativo> errores)
    {
        if (objeto.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && valido(n)) return n;
        errores.Add(new($"{ruta}.{propiedad}", mensaje));
        return 0;
    }

    private static string? Codigo(JsonElement objeto, string ruta, HashSet<string> vistos, string que, List<ErrorDeDocumentoNormativo> errores)
    {
        var code = LecturaDeDocumento.Texto(objeto, "code", ruta, requerido: true, errores);
        if (code is null) return null;
        if (!LecturaDeDocumento.EsCodigo(code))
            errores.Add(new($"{ruta}.code", "El código va en mayúsculas, dígitos y guion bajo."));
        else if (!vistos.Add(code))
            errores.Add(new($"{ruta}.code", $"{que} {code} está repetido."));
        return code;
    }
}

/// <summary>El validador de la plataforma para <see cref="RegulatoryDocumentKind.TiposDeOrganizacion"/>.</summary>
public sealed class ValidadorDeTiposDeOrganizacion : IValidadorDeDocumentoNormativo
{
    public RegulatoryDocumentKind Clase => RegulatoryDocumentKind.TiposDeOrganizacion;

    public IReadOnlyList<short> VersionesDeEsquema => TiposDeOrganizacion.VersionesDeEsquema;

    public IReadOnlyList<ErrorDeDocumentoNormativo> Validar(string contenido, short versionDeEsquema) =>
        TiposDeOrganizacion.Leer(contenido, versionDeEsquema).Errores;
}
