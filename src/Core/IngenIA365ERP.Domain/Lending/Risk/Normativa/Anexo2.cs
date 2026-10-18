using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Risk.Normativa;

/// <summary>
/// El tipo cerrado de una variable del modelo de referencia (K6a, T1025): cada una se evalúa con sus parámetros del documento, nunca con
/// números del programa. Una circular que traiga una variable de otra clase es un tipo nuevo aquí (un criterio nuevo), no un dato.
/// </summary>
public enum TipoDeVariableDelModelo
{
    /// <summary>EA: el asociado está activo en el mes de evaluación (formato 9999).</summary>
    ActiveAssociate = 1,

    /// <summary>FE: la organización es un fondo de empleados.</summary>
    EmployeeFund = 2,

    /// <summary>VALCUOTA: la cuota del mes es como mucho <c>fraction</c> del SMMLV (con <c>onlyEmployeeFund</c>, sólo en fondos de empleados).</summary>
    InstallmentAtMostSmmlvFraction = 3,

    /// <summary>FONDPLAZO: el plazo del crédito es como mucho <c>months</c> meses (con <c>onlyEmployeeFund</c>, sólo en fondos de empleados).</summary>
    TermAtMostMonths = 4,

    /// <summary>MORA…, SINMORA, MORTRIM: todas las condiciones sobre la mora máxima mensual de las ventanas se cumplen.</summary>
    ArrearsHistory = 5,
}

/// <summary>Cómo mira una condición de mora la ventana: la mora máxima de toda la ventana, o si algún mes cae en el rango.</summary>
public enum MedidaDeMora
{
    /// <summary>La mora máxima de los últimos <c>months</c> meses está en el rango.</summary>
    Max = 1,

    /// <summary>Algún mes de los últimos <c>months</c> tuvo una mora máxima en el rango (MORTRIM: «una o más moras»).</summary>
    AnyMonth = 2,
}

/// <summary>Una condición de mora: los últimos <see cref="Meses"/> meses (con el de evaluación), la medida y el rango de días, cerrado.</summary>
public sealed record CondicionDeMora(int Meses, MedidaDeMora Medida, int DiasDesde, int? DiasHasta)
{
    public bool Contiene(int dias) => dias >= DiasDesde && (DiasHasta is null || dias <= DiasHasta);
}

/// <summary>Una variable binaria del modelo: código, tipo cerrado y los parámetros que su tipo pide.</summary>
public sealed record VariableDelModelo(string Codigo, TipoDeVariableDelModelo Tipo, string Descripcion)
{
    /// <summary>VALCUOTA: la fracción del SMMLV.</summary>
    public decimal? Fraccion { get; init; }

    /// <summary>FONDPLAZO: los meses de plazo.</summary>
    public int? Meses { get; init; }

    /// <summary>VALCUOTA y FONDPLAZO: sólo valen 1 en un fondo de empleados.</summary>
    public bool SoloFondoDeEmpleados { get; init; }

    /// <summary>Las condiciones de mora (todas tienen que cumplirse).</summary>
    public IReadOnlyList<CondicionDeMora> Condiciones { get; init; } = [];
}

/// <summary>Un término de Z: la variable y su coeficiente (con su signo).</summary>
public sealed record TerminoDelModelo(string Variable, decimal Coeficiente);

/// <summary>El límite superior del puntaje de una calificación (inclusive).</summary>
public sealed record CorteDePuntaje(RatingCategory Categoria, decimal Hasta);

/// <summary>La PDI de un tipo de garantía mientras el crédito no está incumplido, propia del segmento (garantía no idónea, sin garantía).</summary>
public sealed record PdiSinIncumplimiento(string TipoDeGarantia, decimal Fraccion);

/// <summary>Un segmento de pérdida esperada: su segmento SARC, cuándo está incumplido, Z (intercepto y términos), cortes y PDI propias.</summary>
public sealed record SegmentoDePerdidaEsperada(
    string Codigo,
    string Nombre,
    string SegmentoSarc,
    int DiasDeMoraDeIncumplimiento,
    decimal Intercepto,
    IReadOnlyList<TerminoDelModelo> Terminos,
    IReadOnlyList<CorteDePuntaje> Cortes,
    IReadOnlyList<PdiSinIncumplimiento> PdiSinIncumplimiento);

/// <summary>La PI de una calificación.</summary>
public sealed record PiDeCalificacion(RatingCategory Categoria, decimal Fraccion);

/// <summary>La tabla de PI de un segmento para un tipo de organización (o la clave <see cref="Anexo2.ClaveVoluntaria"/>).</summary>
public sealed record TablaDePi(string Segmento, string TipoDeOrganizacion, IReadOnlyList<PiDeCalificacion> PorCalificacion, decimal Incumplido)
{
    public decimal De(RatingCategory categoria) => PorCalificacion.First(p => p.Categoria == categoria).Fraccion;
}

/// <summary>Un tramo de PDI: desde cuántos días después del incumplimiento rige la fracción.</summary>
public sealed record TramoDePdi(int DiasDesde, decimal Fraccion);

/// <summary>La PDI de un tipo de garantía: si es idónea y sus tramos por días en incumplimiento (el primero desde 0).</summary>
public sealed record PdiPorGarantia(string TipoDeGarantia, string Nombre, bool Idonea, IReadOnlyList<TramoDePdi> Tramos)
{
    /// <summary>La fracción del tramo que contiene los días en incumplimiento.</summary>
    public decimal EnIncumplimiento(int dias) => Tramos.Last(t => t.DiasDesde <= Math.Max(0, dias)).Fraccion;
}

/// <summary>
/// Una fila de homologación: la calificación del modelo (nula = incumplido) con un rango de la mora actual del deudor y la letra contable.
/// </summary>
public sealed record FilaDeHomologacion(RatingCategory? CalificacionDelModelo, int MoraDesde, int? MoraHasta, RatingCategory Contable)
{
    public bool Contiene(int mora) => mora >= MoraDesde && (MoraHasta is null || mora <= MoraHasta);
}

/// <summary>La tabla de homologación de un grupo de segmentos (consumo con y sin libranza; comercial persona natural).</summary>
public sealed record TablaDeHomologacion(IReadOnlyList<string> Segmentos, IReadOnlyList<FilaDeHomologacion> Filas);

/// <summary>Si el deterioro general alcanza a los segmentos en pérdida esperada para un tipo de organización (o <c>*</c>, todos).</summary>
public sealed record DeterioroGeneralEnPerdidaEsperada(string TipoDeOrganizacion, bool Aplica);

/// <summary>
/// Documento normativo <see cref="RegulatoryDocumentKind.SarcAnexo2"/> tipado (feature 013, K6a, T1025; T45, R54; data-model parte 3 §12.4):
/// los modelos de referencia de pérdida esperada de la Supersolidaria (Circular Básica Contable y Financiera, Título IV, Capítulo II, Anexo 2).
/// <b>Sólo forma y reglas de forma</b>: lo usa <c>MotorDePerdidaEsperada</c>. Todo número de la norma (coeficientes, cortes, PI, PDI, días)
/// llega aquí como dato con vigencia (<c>LaCarteraNoTieneValoresLegalesFijos</c>).
///
/// <para>Esquema 1 (camelCase):
/// <c>{ scoreDecimals, singleInstallmentDefaultDaysPastDue?, suspendedConceptsFullyImpaired,
/// variables: [ { code, kind, description, fraction?, months?, onlyEmployeeFund?, conditions?: [ { months, measure, fromDays, toDays? } ] } ],
/// segments: [ { code, name, sarcSegment, defaultDaysPastDue, intercept, terms: [ { variable, coefficient } ],
/// cutoffs: [ { rating, upTo } ], performingLossGivenDefault: [ { collateralType, rate } ] } ],
/// probabilityOfDefault: [ { segment, organizationType, ratings: [ { rating, rate } ], defaultRate } ],
/// lossGivenDefault: [ { collateralType, name, idoneous, brackets: [ { fromDaysAfterDefault, rate } ] } ],
/// homologation: [ { segments: [codigo], rows: [ { modelRating: "A".."E" | "Default", fromCurrentDaysPastDue, toCurrentDaysPastDue?, accountingRating } ] } ],
/// generalImpairment: [ { organizationType, appliesToExpectedLoss } ], provenance?: [ { table, verified, source } ] }</c>.</para>
///
/// <para>Reglas de forma: códigos en mayúsculas sin repetir; tipos de variable cerrados con los parámetros de su tipo; segmentos de
/// <see cref="ReglaDePerdidaEsperada.SegmentosConocidos"/> y segmento SARC del <see cref="Anexo1"/>; cada término nombra una variable
/// definida; cortes A–E en orden, <b>crecientes</b>, en (0, 1] y el de E en 1; PI de A–E e incumplido entre 0 y 1, un tipo de
/// organización por segmento (la clave <see cref="ClaveVoluntaria"/> es la PI de la aplicación voluntaria, CE 100); PDI con tramos desde
/// 0 y días crecientes; la PDI sin incumplimiento sólo de tipos de garantía de la tabla; homologación que cubre cada segmento una vez y,
/// por cada calificación A–E, la mora actual desde 0 sin huecos ni cruces, y el incumplido desde el día siguiente al umbral de
/// incumplimiento de sus segmentos.</para>
/// </summary>
public sealed class Anexo2
{
    public const short VersionDeEsquemaVigente = 1;

    public static IReadOnlyList<short> VersionesDeEsquema { get; } = [1];

    /// <summary>La clave de la tabla de PI para la aplicación voluntaria del modelo (Circular Externa 100).</summary>
    public const string ClaveVoluntaria = "VOLUNTARIA";

    /// <summary>La clave de «todo tipo de organización» en el deterioro general.</summary>
    public const string TodoTipo = "*";

    /// <summary>El texto de la calificación «incumplido» en la homologación.</summary>
    public const string Incumplido = "Default";

    private Anexo2() { }

    /// <summary>Decimales del puntaje: la única escala a la que se redondea en el modelo (pregunta E9).</summary>
    public int DecimalesDelPuntaje { get; private init; }

    /// <summary>Días de mora desde los que un crédito de pago único está incumplido (mayor que); nulo si el documento no lo trae.</summary>
    public int? DiasDeIncumplimientoDePagoUnico { get; private init; }

    /// <summary>¿Los intereses y otros conceptos de un crédito con causación suspendida se deterioran al 100 %?</summary>
    public bool SuspendidosAlCien { get; private init; }

    public IReadOnlyList<VariableDelModelo> Variables { get; private init; } = [];

    public IReadOnlyList<SegmentoDePerdidaEsperada> Segmentos { get; private init; } = [];

    public IReadOnlyList<TablaDePi> ProbabilidadesDeIncumplimiento { get; private init; } = [];

    public IReadOnlyList<PdiPorGarantia> PerdidasDadoElIncumplimiento { get; private init; } = [];

    public IReadOnlyList<TablaDeHomologacion> Homologacion { get; private init; } = [];

    public IReadOnlyList<DeterioroGeneralEnPerdidaEsperada> DeterioroGeneral { get; private init; } = [];

    public IReadOnlyList<ProcedenciaDeTabla> Procedencia { get; private init; } = [];

    /// <summary>Las tablas «pendientes de validar»; sin procedencia declarada, todo el documento lo está (<c>*</c>).</summary>
    public IReadOnlyList<string> TablasSinCotejar =>
        Procedencia.Count == 0 ? ["*"] : Procedencia.Where(p => !p.Verificada).Select(p => p.Tabla).ToList();

    // ------------------------------------------------------------------------------------------------ consultas --

    public SegmentoDePerdidaEsperada? Segmento(string codigo) => Segmentos.FirstOrDefault(s => s.Codigo == codigo);

    public SegmentoDePerdidaEsperada SegmentoObligatorio(string codigo) =>
        Segmento(codigo) ?? throw new InvalidOperationException($"El Anexo 2 vigente no trae el segmento de pérdida esperada «{codigo}».");

    public VariableDelModelo Variable(string codigo) =>
        Variables.FirstOrDefault(v => v.Codigo == codigo) ?? throw new InvalidOperationException($"El Anexo 2 vigente no define la variable «{codigo}».");

    /// <summary>La calificación del modelo: la del primer corte cuyo límite superior alcanza el puntaje.</summary>
    public RatingCategory CalificacionPorPuntaje(string segmento, decimal puntaje) =>
        SegmentoObligatorio(segmento).Cortes.First(c => puntaje <= c.Hasta).Categoria;

    /// <summary>La tabla de PI del segmento para el tipo de organización (o la voluntaria); nula si el documento no la trae.</summary>
    public TablaDePi? TablaDePi(string segmento, string tipoDeOrganizacion) =>
        ProbabilidadesDeIncumplimiento.FirstOrDefault(t => t.Segmento == segmento && t.TipoDeOrganizacion == tipoDeOrganizacion);

    /// <summary>¿Trae la PI de la aplicación voluntaria para el segmento?</summary>
    public bool TienePiVoluntaria(string segmento) => TablaDePi(segmento, ClaveVoluntaria) is not null;

    /// <summary>La PDI de un tipo de garantía; nula si la tabla no lo trae.</summary>
    public PdiPorGarantia? Pdi(string tipoDeGarantia) => PerdidasDadoElIncumplimiento.FirstOrDefault(p => p.TipoDeGarantia == tipoDeGarantia);

    /// <summary>La tabla de homologación del segmento.</summary>
    public TablaDeHomologacion HomologacionDe(string segmento) =>
        Homologacion.FirstOrDefault(h => h.Segmentos.Contains(segmento, StringComparer.Ordinal))
        ?? throw new InvalidOperationException($"El Anexo 2 vigente no trae la homologación de «{segmento}».");

    /// <summary>¿El deterioro general alcanza a los segmentos en pérdida esperada de este tipo de organización? Sin fila, no.</summary>
    public bool DeterioroGeneralAplica(string? tipoDeOrganizacion) =>
        (DeterioroGeneral.FirstOrDefault(d => d.TipoDeOrganizacion == tipoDeOrganizacion)
         ?? DeterioroGeneral.FirstOrDefault(d => d.TipoDeOrganizacion == TodoTipo))?.Aplica ?? false;

    /// <summary>Una copia con las tablas que la política endurece (<c>PoliticaSarcEfectiva.CombinarAnexo2</c>); lo demás sigue a la norma.</summary>
    internal Anexo2 ConTablas(IReadOnlyList<SegmentoDePerdidaEsperada> segmentos, IReadOnlyList<TablaDePi> pi, IReadOnlyList<PdiPorGarantia> pdi) => new()
    {
        DecimalesDelPuntaje = DecimalesDelPuntaje,
        DiasDeIncumplimientoDePagoUnico = DiasDeIncumplimientoDePagoUnico,
        SuspendidosAlCien = SuspendidosAlCien,
        Variables = Variables,
        Segmentos = segmentos,
        ProbabilidadesDeIncumplimiento = pi,
        PerdidasDadoElIncumplimiento = pdi,
        Homologacion = Homologacion,
        DeterioroGeneral = DeterioroGeneral,
        Procedencia = Procedencia,
    };

    // ------------------------------------------------------------------------------------------------- lectura --

    public static ResultadoDeDocumento<Anexo2> Leer(string contenido, short versionDeEsquema)
    {
        var errores = new List<ErrorDeDocumentoNormativo>();
        if (!LecturaDeDocumento.VersionSoportada(versionDeEsquema, VersionesDeEsquema, errores)) return new(null, errores);
        using var doc = LecturaDeDocumento.Abrir(contenido, errores);
        if (doc is null) return new(null, errores);
        var raiz = doc.RootElement;

        var decimales = Anexo1.Entero(raiz, "scoreDecimals", "$", requerido: true, errores);
        if (decimales is > 28) errores.Add(new("$.scoreDecimals", "Los decimales del puntaje van de 0 a 28."));
        var pagoUnico = Anexo1.Entero(raiz, "singleInstallmentDefaultDaysPastDue", "$", requerido: false, errores);
        var suspendidos = LecturaDeDocumento.Booleano(raiz, "suspendedConceptsFullyImpaired", "$", false, errores);

        var variables = LeerVariables(raiz, errores);
        var segmentos = LeerSegmentos(raiz, variables, errores);
        var pdi = LeerPdi(raiz, errores);
        foreach (var (s, i) in segmentos.Select((s, i) => (s, i)))
            foreach (var (p, j) in s.PdiSinIncumplimiento.Select((p, j) => (p, j)))
                if (pdi.All(x => x.TipoDeGarantia != p.TipoDeGarantia))
                    errores.Add(new($"$.segments[{i}].performingLossGivenDefault[{j}].collateralType",
                        $"El tipo de garantía {p.TipoDeGarantia} no está en la tabla de PDI."));
        var pi = LeerPi(raiz, segmentos, errores);
        var homologacion = LeerHomologacion(raiz, segmentos, errores);
        var general = LeerGeneral(raiz, errores);
        var procedencia = Anexo1.LeerProcedencia(raiz, errores);

        return errores.Count > 0 || decimales is null
            ? new(null, errores)
            : new(new Anexo2
            {
                DecimalesDelPuntaje = decimales.Value,
                DiasDeIncumplimientoDePagoUnico = pagoUnico,
                SuspendidosAlCien = suspendidos,
                Variables = variables,
                Segmentos = segmentos,
                ProbabilidadesDeIncumplimiento = pi,
                PerdidasDadoElIncumplimiento = pdi,
                Homologacion = homologacion,
                DeterioroGeneral = general,
                Procedencia = procedencia,
            }, errores);
    }

    private static List<VariableDelModelo> LeerVariables(JsonElement raiz, List<ErrorDeDocumentoNormativo> errores)
    {
        var variables = new List<VariableDelModelo>();
        var lista = LecturaDeDocumento.Arreglo(raiz, "variables", "$", noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var ruta = $"$.variables[{i}]";
            if (!EsObjeto(lista[i], ruta, errores)) continue;
            var codigo = Codigo(lista[i], "code", ruta, errores);
            var tipo = Enumerado<TipoDeVariableDelModelo>(lista[i], "kind", ruta, errores);
            var descripcion = LecturaDeDocumento.Texto(lista[i], "description", ruta, requerido: true, errores);
            if (codigo is null || tipo is null || descripcion is null) continue;
            if (variables.Any(v => v.Codigo == codigo)) errores.Add(new($"{ruta}.code", $"La variable {codigo} está repetida."));

            var variable = new VariableDelModelo(codigo, tipo.Value, descripcion)
            {
                SoloFondoDeEmpleados = LecturaDeDocumento.Booleano(lista[i], "onlyEmployeeFund", ruta, false, errores),
            };
            switch (tipo.Value)
            {
                case TipoDeVariableDelModelo.InstallmentAtMostSmmlvFraction:
                    variable = variable with { Fraccion = Anexo1.Fraccion(lista[i], "fraction", ruta, errores) };
                    break;
                case TipoDeVariableDelModelo.TermAtMostMonths:
                    var meses = Anexo1.Entero(lista[i], "months", ruta, requerido: true, errores);
                    if (meses == 0) errores.Add(new($"{ruta}.months", "Los meses de plazo son mayores que cero."));
                    variable = variable with { Meses = meses };
                    break;
                case TipoDeVariableDelModelo.ArrearsHistory:
                    variable = variable with { Condiciones = LeerCondiciones(lista[i], ruta, errores) };
                    break;
            }
            variables.Add(variable);
        }
        return variables;
    }

    private static List<CondicionDeMora> LeerCondiciones(JsonElement v, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        var condiciones = new List<CondicionDeMora>();
        var lista = LecturaDeDocumento.Arreglo(v, "conditions", ruta, noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var r = $"{ruta}.conditions[{i}]";
            if (!EsObjeto(lista[i], r, errores)) continue;
            var meses = Anexo1.Entero(lista[i], "months", r, requerido: true, errores);
            var medida = Enumerado<MedidaDeMora>(lista[i], "measure", r, errores);
            var desde = Anexo1.Entero(lista[i], "fromDays", r, requerido: true, errores);
            var hasta = Anexo1.Entero(lista[i], "toDays", r, requerido: false, errores);
            if (meses == 0) errores.Add(new($"{r}.months", "La ventana tiene al menos un mes."));
            if (hasta is { } h && desde is { } d && h < d) errores.Add(new(r, "El rango de días termina antes de empezar."));
            if (meses is > 0 && medida is not null && desde is not null) condiciones.Add(new(meses.Value, medida.Value, desde.Value, hasta));
        }
        return condiciones;
    }

    private static List<SegmentoDePerdidaEsperada> LeerSegmentos(JsonElement raiz, List<VariableDelModelo> variables, List<ErrorDeDocumentoNormativo> errores)
    {
        var segmentos = new List<SegmentoDePerdidaEsperada>();
        var lista = LecturaDeDocumento.Arreglo(raiz, "segments", "$", noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var ruta = $"$.segments[{i}]";
            if (!EsObjeto(lista[i], ruta, errores)) continue;
            var codigo = LecturaDeDocumento.Texto(lista[i], "code", ruta, requerido: true, errores);
            if (codigo is not null && !ReglaDePerdidaEsperada.SegmentosConocidos.Contains(codigo, StringComparer.Ordinal))
                errores.Add(new($"{ruta}.code", $"«{codigo}» no se conoce; se admiten: {string.Join(", ", ReglaDePerdidaEsperada.SegmentosConocidos)}."));
            if (codigo is not null && segmentos.Any(s => s.Codigo == codigo)) errores.Add(new($"{ruta}.code", $"El segmento {codigo} está repetido."));
            var nombre = LecturaDeDocumento.Texto(lista[i], "name", ruta, requerido: true, errores);
            var sarc = LecturaDeDocumento.Texto(lista[i], "sarcSegment", ruta, requerido: true, errores);
            if (sarc is not null && !Anexo1.SegmentosConocidos.Contains(sarc, StringComparer.Ordinal))
                errores.Add(new($"{ruta}.sarcSegment", $"«{sarc}» no es un segmento SARC; se admiten: {string.Join(", ", Anexo1.SegmentosConocidos)}."));
            var incumplimiento = Anexo1.Entero(lista[i], "defaultDaysPastDue", ruta, requerido: true, errores);
            if (incumplimiento == 0) errores.Add(new($"{ruta}.defaultDaysPastDue", "El umbral de incumplimiento es mayor que cero."));
            var intercepto = Numero(lista[i], "intercept", ruta, errores);

            var terminos = new List<TerminoDelModelo>();
            var listaDeTerminos = LecturaDeDocumento.Arreglo(lista[i], "terms", ruta, noVacio: true, errores);
            for (var j = 0; j < listaDeTerminos.Count; j++)
            {
                var r = $"{ruta}.terms[{j}]";
                if (!EsObjeto(listaDeTerminos[j], r, errores)) continue;
                var variable = LecturaDeDocumento.Texto(listaDeTerminos[j], "variable", r, requerido: true, errores);
                var coeficiente = Numero(listaDeTerminos[j], "coefficient", r, errores);
                if (variable is null || coeficiente is null) continue;
                if (variables.All(v => v.Codigo != variable)) errores.Add(new($"{r}.variable", $"La variable {variable} no está definida en variables."));
                if (terminos.Any(t => t.Variable == variable)) errores.Add(new($"{r}.variable", $"La variable {variable} está repetida en el segmento."));
                terminos.Add(new(variable, coeficiente.Value));
            }

            var cortes = new List<CorteDePuntaje>();
            var listaDeCortes = LecturaDeDocumento.Arreglo(lista[i], "cutoffs", ruta, noVacio: true, errores);
            for (var j = 0; j < listaDeCortes.Count; j++)
            {
                var r = $"{ruta}.cutoffs[{j}]";
                if (!EsObjeto(listaDeCortes[j], r, errores)) continue;
                var cat = Anexo1.Categoria(listaDeCortes[j], "rating", r, errores);
                var hasta = Anexo1.Fraccion(listaDeCortes[j], "upTo", r, errores);
                if (cat is not null && hasta is not null) cortes.Add(new(cat.Value, hasta.Value));
            }
            RevisarCortes(cortes, $"{ruta}.cutoffs", errores);

            var sinIncumplimiento = new List<PdiSinIncumplimiento>();
            if (lista[i].TryGetProperty("performingLossGivenDefault", out _))
            {
                var listaDePdi = LecturaDeDocumento.Arreglo(lista[i], "performingLossGivenDefault", ruta, noVacio: false, errores);
                for (var j = 0; j < listaDePdi.Count; j++)
                {
                    var r = $"{ruta}.performingLossGivenDefault[{j}]";
                    if (!EsObjeto(listaDePdi[j], r, errores)) continue;
                    var tipo = Codigo(listaDePdi[j], "collateralType", r, errores);
                    var tasa = Anexo1.Fraccion(listaDePdi[j], "rate", r, errores);
                    if (tipo is null || tasa is null) continue;
                    if (sinIncumplimiento.Any(p => p.TipoDeGarantia == tipo)) errores.Add(new($"{r}.collateralType", $"El tipo {tipo} está repetido."));
                    sinIncumplimiento.Add(new(tipo, tasa.Value));
                }
            }

            if (codigo is null || nombre is null || sarc is null || incumplimiento is null || intercepto is null) continue;
            segmentos.Add(new(codigo, nombre, sarc, incumplimiento.Value, intercepto.Value, terminos, cortes, sinIncumplimiento));
        }
        return segmentos;
    }

    private static void RevisarCortes(List<CorteDePuntaje> cortes, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (!cortes.Select(c => c.Categoria).SequenceEqual(Enum.GetValues<RatingCategory>()))
        {
            errores.Add(new(ruta, "Los cortes van una vez por calificación y en orden: A, B, C, D, E."));
            return;
        }
        for (var i = 0; i < cortes.Count; i++)
        {
            if (cortes[i].Hasta <= 0m) errores.Add(new($"{ruta}[{i}].upTo", "El corte es mayor que cero."));
            if (i > 0 && cortes[i].Hasta <= cortes[i - 1].Hasta) errores.Add(new($"{ruta}[{i}].upTo", "Los cortes son crecientes."));
        }
        if (cortes[^1].Hasta != 1m) errores.Add(new($"{ruta}[{cortes.Count - 1}].upTo", "El corte de E es 1: todo puntaje tiene calificación."));
    }

    private static List<TablaDePi> LeerPi(JsonElement raiz, List<SegmentoDePerdidaEsperada> segmentos, List<ErrorDeDocumentoNormativo> errores)
    {
        var tablas = new List<TablaDePi>();
        var lista = LecturaDeDocumento.Arreglo(raiz, "probabilityOfDefault", "$", noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var ruta = $"$.probabilityOfDefault[{i}]";
            if (!EsObjeto(lista[i], ruta, errores)) continue;
            var segmento = LecturaDeDocumento.Texto(lista[i], "segment", ruta, requerido: true, errores);
            if (segmento is not null && segmentos.All(s => s.Codigo != segmento))
                errores.Add(new($"{ruta}.segment", $"El segmento {segmento} no está en segments."));
            var tipo = Codigo(lista[i], "organizationType", ruta, errores);
            var incumplido = Anexo1.Fraccion(lista[i], "defaultRate", ruta, errores);
            var porCalificacion = new List<PiDeCalificacion>();
            var tasas = LecturaDeDocumento.Arreglo(lista[i], "ratings", ruta, noVacio: true, errores);
            for (var j = 0; j < tasas.Count; j++)
            {
                var r = $"{ruta}.ratings[{j}]";
                if (!EsObjeto(tasas[j], r, errores)) continue;
                var cat = Anexo1.Categoria(tasas[j], "rating", r, errores);
                var tasa = Anexo1.Fraccion(tasas[j], "rate", r, errores);
                if (cat is not null && tasa is not null) porCalificacion.Add(new(cat.Value, tasa.Value));
            }
            if (!porCalificacion.Select(p => p.Categoria).SequenceEqual(Enum.GetValues<RatingCategory>()))
                errores.Add(new($"{ruta}.ratings", "La PI va una vez por calificación y en orden: A, B, C, D, E."));
            if (segmento is null || tipo is null || incumplido is null) continue;
            if (tablas.Any(t => t.Segmento == segmento && t.TipoDeOrganizacion == tipo))
                errores.Add(new($"{ruta}.organizationType", $"La PI de {tipo} en {segmento} está repetida."));
            tablas.Add(new(segmento, tipo, porCalificacion, incumplido.Value));
        }
        return tablas;
    }

    private static List<PdiPorGarantia> LeerPdi(JsonElement raiz, List<ErrorDeDocumentoNormativo> errores)
    {
        var tablas = new List<PdiPorGarantia>();
        var lista = LecturaDeDocumento.Arreglo(raiz, "lossGivenDefault", "$", noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var ruta = $"$.lossGivenDefault[{i}]";
            if (!EsObjeto(lista[i], ruta, errores)) continue;
            var tipo = Codigo(lista[i], "collateralType", ruta, errores);
            var nombre = LecturaDeDocumento.Texto(lista[i], "name", ruta, requerido: true, errores);
            var idonea = LecturaDeDocumento.Booleano(lista[i], "idoneous", ruta, false, errores);
            var tramos = new List<TramoDePdi>();
            var listaDeTramos = LecturaDeDocumento.Arreglo(lista[i], "brackets", ruta, noVacio: true, errores);
            for (var j = 0; j < listaDeTramos.Count; j++)
            {
                var r = $"{ruta}.brackets[{j}]";
                if (!EsObjeto(listaDeTramos[j], r, errores)) continue;
                var desde = Anexo1.Entero(listaDeTramos[j], "fromDaysAfterDefault", r, requerido: true, errores);
                var tasa = Anexo1.Fraccion(listaDeTramos[j], "rate", r, errores);
                if (desde is null || tasa is null) continue;
                if (tramos.Count == 0 && desde != 0) errores.Add(new($"{r}.fromDaysAfterDefault", "El primer tramo empieza en 0 días."));
                if (tramos.Count > 0 && desde <= tramos[^1].DiasDesde) errores.Add(new($"{r}.fromDaysAfterDefault", "Los tramos van en días crecientes."));
                tramos.Add(new(desde.Value, tasa.Value));
            }
            if (tipo is null || nombre is null) continue;
            if (tablas.Any(t => t.TipoDeGarantia == tipo)) errores.Add(new($"{ruta}.collateralType", $"El tipo de garantía {tipo} está repetido."));
            tablas.Add(new(tipo, nombre, idonea, tramos));
        }
        return tablas;
    }

    private static List<TablaDeHomologacion> LeerHomologacion(JsonElement raiz, List<SegmentoDePerdidaEsperada> segmentos, List<ErrorDeDocumentoNormativo> errores)
    {
        var tablas = new List<TablaDeHomologacion>();
        var lista = LecturaDeDocumento.Arreglo(raiz, "homologation", "$", noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var ruta = $"$.homologation[{i}]";
            if (!EsObjeto(lista[i], ruta, errores)) continue;
            var deLaTabla = new List<string>();
            var codigos = LecturaDeDocumento.Arreglo(lista[i], "segments", ruta, noVacio: true, errores);
            for (var j = 0; j < codigos.Count; j++)
            {
                var texto = codigos[j].ValueKind == JsonValueKind.String ? codigos[j].GetString() : null;
                if (texto is null || segmentos.All(s => s.Codigo != texto))
                    errores.Add(new($"{ruta}.segments[{j}]", $"«{texto}» no está en segments."));
                else if (deLaTabla.Contains(texto) || tablas.Any(t => t.Segmentos.Contains(texto)))
                    errores.Add(new($"{ruta}.segments[{j}]", $"El segmento {texto} ya tiene homologación."));
                else deLaTabla.Add(texto);
            }

            var filas = new List<FilaDeHomologacion>();
            var listaDeFilas = LecturaDeDocumento.Arreglo(lista[i], "rows", ruta, noVacio: true, errores);
            for (var j = 0; j < listaDeFilas.Count; j++)
            {
                var r = $"{ruta}.rows[{j}]";
                if (!EsObjeto(listaDeFilas[j], r, errores)) continue;
                var textoModelo = LecturaDeDocumento.Texto(listaDeFilas[j], "modelRating", r, requerido: true, errores);
                RatingCategory? modelo = null;
                var valida = textoModelo is not null;
                if (textoModelo is not null && textoModelo != Incumplido)
                {
                    if (textoModelo.Length == 1 && Enum.TryParse<RatingCategory>(textoModelo, out var c) && Enum.IsDefined(c)) modelo = c;
                    else
                    {
                        errores.Add(new($"{r}.modelRating", $"«{textoModelo}» no es una calificación del modelo (A, B, C, D, E o {Incumplido})."));
                        valida = false;
                    }
                }
                var desde = Anexo1.Entero(listaDeFilas[j], "fromCurrentDaysPastDue", r, requerido: true, errores);
                var hasta = Anexo1.Entero(listaDeFilas[j], "toCurrentDaysPastDue", r, requerido: false, errores);
                var contable = Anexo1.Categoria(listaDeFilas[j], "accountingRating", r, errores);
                if (hasta is { } h && desde is { } d && h < d) errores.Add(new(r, "El rango de mora termina antes de empezar."));
                if (valida && desde is not null && contable is not null) filas.Add(new(modelo, desde.Value, hasta, contable.Value));
            }
            RevisarHomologacion(filas, deLaTabla.Select(s => segmentos.First(x => x.Codigo == s)).ToList(), $"{ruta}.rows", errores);
            tablas.Add(new(deLaTabla, filas));
        }
        foreach (var s in segmentos.Where(s => tablas.All(t => !t.Segmentos.Contains(s.Codigo))))
            errores.Add(new("$.homologation", $"El segmento {s.Codigo} no tiene homologación."));
        return tablas;
    }

    /// <summary>Por cada calificación A–E, la mora actual desde 0; el incumplido, desde el día siguiente al umbral; sin huecos ni cruces.</summary>
    private static void RevisarHomologacion(List<FilaDeHomologacion> filas, List<SegmentoDePerdidaEsperada> segmentos, string ruta,
        List<ErrorDeDocumentoNormativo> errores)
    {
        var grupos = Enum.GetValues<RatingCategory>().Select(c => (RatingCategory?)c).Append(null);
        foreach (var calificacion in grupos)
        {
            var nombre = calificacion?.ToString() ?? Incumplido;
            var rango = filas.Where(f => f.CalificacionDelModelo == calificacion).OrderBy(f => f.MoraDesde).ToList();
            if (rango.Count == 0)
            {
                errores.Add(new(ruta, $"Falta la homologación de {nombre}."));
                continue;
            }
            var inicios = calificacion is null ? segmentos.Select(s => s.DiasDeMoraDeIncumplimiento + 1).Distinct().ToList() : [0];
            if (inicios.Count != 1 || rango[0].MoraDesde != inicios[0])
                errores.Add(new(ruta, calificacion is null
                    ? "El incumplido se homologa desde el día siguiente al umbral de incumplimiento de sus segmentos, que tiene que ser el mismo."
                    : $"La homologación de {nombre} empieza en 0 días de mora."));
            for (var i = 0; i < rango.Count; i++)
            {
                var ultima = i == rango.Count - 1;
                if (ultima && rango[i].MoraHasta is not null) errores.Add(new(ruta, $"La última fila de {nombre} no tiene tope."));
                if (!ultima && rango[i].MoraHasta is null) errores.Add(new(ruta, $"Sólo la última fila de {nombre} va sin tope."));
                if (i > 0 && rango[i - 1].MoraHasta is { } anterior && rango[i].MoraDesde != anterior + 1)
                    errores.Add(new(ruta, $"La homologación de {nombre} tiene un hueco o un cruce en {rango[i].MoraDesde.ToString(CultureInfo.InvariantCulture)} días."));
            }
        }
    }

    private static List<DeterioroGeneralEnPerdidaEsperada> LeerGeneral(JsonElement raiz, List<ErrorDeDocumentoNormativo> errores)
    {
        var general = new List<DeterioroGeneralEnPerdidaEsperada>();
        var lista = LecturaDeDocumento.Arreglo(raiz, "generalImpairment", "$", noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var ruta = $"$.generalImpairment[{i}]";
            if (!EsObjeto(lista[i], ruta, errores)) continue;
            var tipo = LecturaDeDocumento.Texto(lista[i], "organizationType", ruta, requerido: true, errores);
            if (tipo is null) continue;
            if (tipo != TodoTipo && !LecturaDeDocumento.EsCodigo(tipo))
                errores.Add(new($"{ruta}.organizationType", $"«{tipo}» no es un código ni «{TodoTipo}»."));
            if (general.Any(g => g.TipoDeOrganizacion == tipo)) errores.Add(new($"{ruta}.organizationType", $"{tipo} está repetido."));
            if (!lista[i].TryGetProperty("appliesToExpectedLoss", out _)) errores.Add(new($"{ruta}.appliesToExpectedLoss", "Es obligatorio."));
            general.Add(new(tipo, LecturaDeDocumento.Booleano(lista[i], "appliesToExpectedLoss", ruta, false, errores)));
        }
        return general;
    }

    // ------------------------------------------------------------------------------------------------- lectores --

    private static bool EsObjeto(JsonElement e, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (e.ValueKind == JsonValueKind.Object) return true;
        errores.Add(new(ruta, "Es un objeto."));
        return false;
    }

    private static string? Codigo(JsonElement objeto, string propiedad, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        var texto = LecturaDeDocumento.Texto(objeto, propiedad, ruta, requerido: true, errores);
        if (texto is null) return null;
        if (LecturaDeDocumento.EsCodigo(texto)) return texto;
        errores.Add(new($"{ruta}.{propiedad}", "El código va en mayúsculas, dígitos y guion bajo."));
        return null;
    }

    /// <summary>Un enumerado por su nombre (nunca por número): el tipo es cerrado.</summary>
    private static T? Enumerado<T>(JsonElement objeto, string propiedad, string ruta, List<ErrorDeDocumentoNormativo> errores) where T : struct, Enum
    {
        var texto = LecturaDeDocumento.Texto(objeto, propiedad, ruta, requerido: true, errores);
        if (texto is null) return null;
        if (!int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) && Enum.TryParse<T>(texto, out var valor) && Enum.IsDefined(valor))
            return valor;
        errores.Add(new($"{ruta}.{propiedad}", $"«{texto}» no se conoce; se admiten: {string.Join(", ", Enum.GetNames<T>())}."));
        return null;
    }

    /// <summary>Un número con signo (los coeficientes y el intercepto).</summary>
    private static decimal? Numero(JsonElement objeto, string propiedad, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (objeto.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        errores.Add(new($"{ruta}.{propiedad}", "Es obligatorio y es un número."));
        return null;
    }
}
