using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Un documento normativo usado por una calificación o un cierre, con lo que se sella (data-model §23.1, <c>DocumentsUsedJson</c>). (nuevo)</summary>
public sealed record DocumentoUsado(Guid PublicId, string Kind, string Code, DateOnly ValidFrom, string Sha256, bool Verified);

/// <summary>
/// Lo que el riesgo necesita del entorno a una fecha y con lo conocido en R (feature 013, US9, T0699; T45, T46, T50, D19, C33): la norma vigente
/// (Anexo 1), la regla de pérdida esperada, el catálogo de modalidades, la política SARC validada y la norma <b>efectiva</b>
/// (<see cref="PoliticaSarcEfectiva"/>), los parámetros del riesgo, los indicadores de la entidad, el perfil regulatorio y el modelo por segmento
/// (<see cref="SelectorDeModeloDeDeterioro"/>), con lo que hay que sellar: los documentos usados y lo «pendiente de validar» (T41, C76). (nuevo)
/// </summary>
public sealed class ContextoDeRiesgo
{
    public required DateOnly Fecha { get; init; }

    public required DateTime? InformacionHasta { get; init; }

    public required DocumentoNormativoVigente<Anexo1> Anexo1 { get; init; }

    public required DocumentoNormativoVigente<ReglaDePerdidaEsperada>? Regla { get; init; }

    public required DocumentoNormativoVigente<CatalogoDeModalidades> Catalogo { get; init; }

    public required PoliticaVigente? Politica { get; init; }

    public required ResultadoDePoliticaEfectiva Efectiva { get; init; }

    /// <summary>La norma que rige: el Anexo 1 con la política aplicada celda por celda.</summary>
    public Anexo1 Norma => Efectiva.Efectiva;

    public required RedondeoDeDeterioro Redondeo { get; init; }

    /// <summary><c>Riesgo.CalificacionDiariaConArrastre</c>: la informativa aplica el arrastre (pregunta E7).</summary>
    public required bool ArrastreEnLaInformativa { get; init; }

    /// <summary><c>Contabilidad.DisminucionDeDeterioro</c>: <c>MismoEjercicioPrimero</c> o <c>TodoARecuperacion</c> (C2).</summary>
    public required string DisminucionDeDeterioro { get; init; }

    public required ContextoDeAportes Aportes { get; init; }

    public required IndicadoresDeCartera? Indicadores { get; init; }

    public required PerfilRegulatorioDto? Perfil { get; init; }

    public required SeleccionDeModelos Seleccion { get; init; }

    /// <summary>Los porcentajes del general; nulo fuera del cierre.</summary>
    public required ParametrosDeDeterioroGeneral? General { get; init; }

    /// <summary>
    /// Los modelos que llevan deterioro general: el Anexo 1 y, desde K6a, la pérdida esperada si el Anexo 2 dice que aplica al tipo de entidad
    /// (<c>Anexo2.DeterioroGeneralAplica</c>; semilla: sí, pendiente de validar).
    /// </summary>
    public IReadOnlySet<ImpairmentModel> ModelosConGeneral { get; init; } = new HashSet<ImpairmentModel> { ImpairmentModel.ArrearsAnnex1 };

    /// <summary>K6a (T1036): lo que la pérdida esperada necesita; nulo si ningún segmento está en pérdida esperada al cierre.</summary>
    public ContextoDePerdidaEsperada? PerdidaEsperada { get; init; }

    public required IReadOnlyList<DocumentoUsado> DocumentosUsados { get; init; }

    /// <summary>Documentos, tablas, valores y parámetros «pendiente de validar» usados.</summary>
    public required IReadOnlyList<string> SinValidar { get; init; }

    public bool ConParametrosSinValidar => SinValidar.Count > 0;

    /// <summary>El JSON de <c>UnvalidatedParametersJson</c>.</summary>
    public string? SinValidarJson => SinValidar.Count == 0 ? null : JsonSerializer.Serialize(SinValidar);

    /// <summary>La combinación del catálogo de una modalidad y sub-segmento, o nula si salió del catálogo.</summary>
    public CombinacionDeModalidad? Combinacion(string modalidad, string? subsegmento) => Catalogo.Documento.Buscar(modalidad, subsegmento);
}

/// <summary>
/// K6a (feature 013, T1036; FR-105, FR-106): lo que la pérdida esperada del cierre necesita del entorno: el Anexo 2 vigente y el <b>efectivo</b>
/// (con la política del consejo, <c>PoliticaSarcEfectiva.CombinarAnexo2</c>: sólo más estricta), el tipo de organización del perfil vigente (la
/// clave de la PI), si es fondo de empleados (<c>TiposDeOrganizacion</c>), el SMMLV vigente (VALCUOTA), la regla de meses faltantes
/// (<c>Riesgo.ReglaMesesFaltantes</c> y, con «Fija», <c>Riesgo.MesesFaltantesDiasFijos</c>) y los meses de historial que se miran: los de
/// <c>ReglaDePerdidaEsperada</c> o la ventana más larga de las variables del documento, la mayor. (nuevo)
/// </summary>
public sealed record ContextoDePerdidaEsperada(
    DocumentoNormativoVigente<Anexo2> Documento,
    Anexo2 Efectivo,
    string TipoDeOrganizacion,
    bool EsFondoDeEmpleados,
    decimal Smmlv,
    ReglaDeMesesFaltantes ReglaDeMesesFaltantes,
    int? DiasFijos,
    int MesesDeHistorial)
{
    /// <summary>El contexto del motor para un crédito: con la PI voluntaria si su segmento está en pérdida esperada por adopción.</summary>
    public ContextoDelModelo Modelo(bool voluntaria) => new(TipoDeOrganizacion, EsFondoDeEmpleados, Smmlv, voluntaria);

    /// <summary>La ventana más larga de las variables de mora del documento.</summary>
    public static int VentanaDelDocumento(Anexo2 anexo) =>
        anexo.Variables.SelectMany(v => v.Condiciones).Select(c => c.Meses).DefaultIfEmpty(1).Max();
}

/// <summary>
/// El lector del <see cref="ContextoDeRiesgo"/> (feature 013, US9, T0699): lee cada pieza por su lector único —documentos por
/// <see cref="INormativaVigente"/> con su SHA-256, el límite <c>DETERIORO_GENERAL_MINIMO</c> por <see cref="IValoresNacionales"/>, los parámetros
/// por <see cref="ReglasDeParametrosDeCartera"/>, los indicadores por <see cref="LectorDeIndicadoresDeLaEntidad"/>, el perfil por
/// <see cref="IPerfilRegulatorio"/> y la política por <see cref="PoliticaSarcVigente"/>—. Sin Anexo 1 o sin catálogo vigente no hay
/// calificación: <c>Lending.MonthlyClose.RegulationMissing</c> (Principio IX, nunca una tabla supuesta). Mientras rija D19 los aportes no
/// cuentan (<see cref="ContextoDeAportes"/>). (nuevo)
/// </summary>
public sealed class LectorDelContextoDeRiesgo(
    INormativaVigente normativa,
    IValoresNacionales valores,
    ReglasDeParametrosDeCartera parametros,
    LectorDeIndicadoresDeLaEntidad indicadores,
    IPerfilRegulatorio perfiles,
    PoliticaSarcVigente politicas,
    IApplicationDbContext? db = null)
{
    /// <summary>Mientras Aportes y Ahorros no ofrezcan su consulta (<c>ConsultaDeAportesNoDisponible</c>), rige D19.</summary>
    public const bool RigeD19 = true;

    /// <param name="fecha">La fecha de la calificación (el último día del mes en el cierre).</param>
    /// <param name="informacionHasta">R: lo conocido hasta este instante.</param>
    /// <param name="paraElCierre">Con el general, los indicadores y el perfil (el cierre); sin ellos, la calificación informativa.</param>
    public async Task<Result<ContextoDeRiesgo>> LeerAsync(DateOnly fecha, DateTime? informacionHasta, bool paraElCierre, CancellationToken ct)
    {
        var anexo = await normativa.LeerAsync(RegulatoryDocumentKind.SarcAnexo1, fecha, Anexo1.Leer, informacionHasta, ct);
        if (anexo.IsFailure) return Result.Failure<ContextoDeRiesgo>(ErroresDeRiesgo.SinNorma($"no hay Anexo 1 del SARC vigente el {fecha:yyyy-MM-dd}. {anexo.Error.Message}"));
        var catalogo = await normativa.CatalogoDeModalidadesAsync(fecha, informacionHasta, ct);
        if (catalogo.IsFailure)
            return Result.Failure<ContextoDeRiesgo>(ErroresDeRiesgo.SinNorma($"no hay catálogo de modalidades vigente el {fecha:yyyy-MM-dd}. {catalogo.Error.Message}"));
        var regla = await normativa.LeerAsync(RegulatoryDocumentKind.ReglaDePerdidaEsperada, fecha, ReglaDePerdidaEsperada.Leer, informacionHasta, ct);

        var politica = await politicas.LeerAsync(fecha, informacionHasta, ct);
        var efectiva = PoliticaSarcEfectiva.Combinar(anexo.Value.Documento, politica?.Politica);

        var redondeo = await TextoAsync(ParametrosDeCartera.RiesgoRedondeoDeterioro, fecha, informacionHasta, "Centavo", ct);
        var arrastre = await LogicoAsync(ParametrosDeCartera.RiesgoCalificacionDiariaConArrastre, fecha, informacionHasta, true, ct);
        var disminucion = await TextoAsync(ParametrosDeCartera.ContabilidadDisminucionDeDeterioro, fecha, informacionHasta, "MismoEjercicioPrimero", ct);

        var sinValidar = new List<string>();
        void Documento<T>(DocumentoNormativoVigente<T> d) where T : class
        {
            if (!d.Verified) sinValidar.Add($"document:{d.Clase}:{d.Codigo}");
        }
        Documento(anexo.Value);
        Documento(catalogo.Value);
        if (regla.IsSuccess) Documento(regla.Value);
        sinValidar.AddRange(anexo.Value.Documento.TablasSinCotejar.Select(t => $"table:{RegulatoryDocumentKind.SarcAnexo1}:{t}"));
        if (disminucion.SinValidar) sinValidar.Add($"parameter:{ParametrosDeCartera.ContabilidadDisminucionDeDeterioro}");

        IndicadoresDeCartera? leidos = null;
        PerfilRegulatorioDto? perfil = null;
        ParametrosDeDeterioroGeneral? general = null;
        var aportes = ContextoDeAportes.D19SinIndicadores;
        if (paraElCierre)
        {
            leidos = await indicadores.LeerAsync(fecha, ct);
            aportes = leidos.ContextoDeAportes(RigeD19);
            perfil = await perfiles.VigenteAsync(fecha, informacionHasta, ct);
            var limite = await valores.LeerAsync(CodigosDeValorNacional.Limite, CatalogoDeLimitesLegales.DeterioroGeneralMinimo, fecha, informacionHasta, ct);
            if (limite.IsFailure) return Result.Failure<ContextoDeRiesgo>(limite.Error);
            if (limite.Value.Estado != EstadoDeRevision.Validada)
                sinValidar.Add($"value:{CodigosDeValorNacional.Limite}:{CatalogoDeLimitesLegales.DeterioroGeneralMinimo}");
            try
            {
                general = ParametrosDeDeterioroGeneral.Desde(efectiva, perfil?.OrganizationTypeCode, limite.Value.Valor ?? 0m);
            }
            catch (InvalidOperationException ex)
            {
                return Result.Failure<ContextoDeRiesgo>(ErroresDeRiesgo.SinNorma(ex.Message));
            }
        }

        // K6a (T1014, T1036): el modelo de cada segmento al cierre, con el perfil vigente y la última elección voluntaria registrada hasta R cuyo
        // ValidFrom ya llegó (la adopción rige desde su fecha y el desistimiento vuelve al Anexo 1 desde la suya; nunca hacia atrás).
        var elecciones = paraElCierre && db is not null ? await EleccionesAsync(db, fecha, informacionHasta, ct) : [];
        var seleccion = SelectorDeModeloDeDeterioro.SeleccionarAl(fecha, regla.IsSuccess ? regla.Value.Documento : null,
            perfil is null ? [] : [new VigenciaDePerfil(perfil.ValidFrom, new PerfilParaModelo(perfil.OrganizationTypeCode, perfil.EmployeeFundCategory))],
            elecciones, CatalogoDeCartera.EntregaVigente);

        var usados = new List<DocumentoUsado>
        {
            Usado(anexo.Value),
            Usado(catalogo.Value),
        };
        if (regla.IsSuccess) usados.Add(Usado(regla.Value));

        ContextoDePerdidaEsperada? perdida = null;
        var modelosConGeneral = new HashSet<ImpairmentModel> { ImpairmentModel.ArrearsAnnex1 };
        var enPerdida = seleccion.Segmentos.Where(s => s.Modelo == ImpairmentModel.ExpectedLossAnnex2).ToList();
        if (enPerdida.Count > 0)
        {
            var leida = await PerdidaEsperadaAsync(fecha, informacionHasta, perfil, politica, regla.IsSuccess ? regla.Value.Documento : null, enPerdida,
                sinValidar, ct);
            if (leida.IsFailure) return Result.Failure<ContextoDeRiesgo>(leida.Error);
            perdida = leida.Value;
            usados.Add(Usado(perdida.Documento));
            if (perdida.Efectivo.DeterioroGeneralAplica(perdida.TipoDeOrganizacion)) modelosConGeneral.Add(ImpairmentModel.ExpectedLossAnnex2);
        }

        return Result.Success(new ContextoDeRiesgo
        {
            Fecha = fecha,
            InformacionHasta = informacionHasta,
            Anexo1 = anexo.Value,
            Regla = regla.IsSuccess ? regla.Value : null,
            Catalogo = catalogo.Value,
            Politica = politica,
            Efectiva = efectiva,
            Redondeo = string.Equals(redondeo.Texto, nameof(RedondeoDeDeterioro.Peso), StringComparison.Ordinal) ? RedondeoDeDeterioro.Peso : RedondeoDeDeterioro.Centavo,
            ArrastreEnLaInformativa = arrastre,
            DisminucionDeDeterioro = disminucion.Texto,
            Aportes = aportes,
            Indicadores = leidos,
            Perfil = perfil,
            Seleccion = seleccion,
            General = general,
            ModelosConGeneral = modelosConGeneral,
            PerdidaEsperada = perdida,
            DocumentosUsados = usados,
            SinValidar = sinValidar,
        });
    }

    /// <summary>Las elecciones voluntarias registradas hasta R (K6a, <c>LND_ImpairmentModelElections</c>) que ya rigen a la fecha.</summary>
    public static async Task<IReadOnlyList<EleccionDeModelo>> EleccionesAsync(IApplicationDbContext db, DateOnly fecha, DateTime? informacionHasta,
        CancellationToken ct)
    {
        var r = informacionHasta ?? DateTime.MaxValue;
        var filas = await db.ImpairmentModelElections.AsNoTracking().Where(e => !e.IsDeleted && e.ValidFrom <= fecha && e.RegisteredAt <= r)
            .OrderBy(e => e.ValidFrom).ToListAsync(ct);
        return filas.Select(e => new EleccionDeModelo(e.Kind, e.Segmentos) { Desde = e.ValidFrom }).ToList();
    }

    /// <summary>
    /// K6a (T1036): el contexto de la pérdida esperada. Sin Anexo 2 vigente, o sin la tabla de PI de la clave de un segmento en pérdida esperada (el
    /// tipo de organización, o <c>VOLUNTARIA</c> en la adopción), no hay cierre: <c>Lending.MonthlyClose.RegulationMissing</c> (Principio IX: el motor
    /// nunca inventa una tabla). Sin SMMLV vigente sólo falla un fondo de empleados (VALCUOTA); para los demás no se usa.
    /// </summary>
    private async Task<Result<ContextoDePerdidaEsperada>> PerdidaEsperadaAsync(DateOnly fecha, DateTime? r, PerfilRegulatorioDto? perfil, PoliticaVigente? politica,
        ReglaDePerdidaEsperada? regla, IReadOnlyList<ModeloDelSegmento> enPerdida, List<string> sinValidar, CancellationToken ct)
    {
        var anexo2 = await normativa.LeerAsync(RegulatoryDocumentKind.SarcAnexo2, fecha, Anexo2.Leer, r, ct);
        if (anexo2.IsFailure)
            return Result.Failure<ContextoDePerdidaEsperada>(ErroresDeRiesgo.SinNorma(
                $"hay segmentos en pérdida esperada y no hay Anexo 2 del SARC vigente el {fecha:yyyy-MM-dd}. {anexo2.Error.Message}"));
        var efectivo = PoliticaSarcEfectiva.CombinarAnexo2(anexo2.Value.Documento, politica?.Politica).Efectiva;
        var tipo = perfil?.OrganizationTypeCode ?? string.Empty;
        foreach (var s in enPerdida)
        {
            var clave = s.Fundamento == ModelBasis.Voluntary ? Anexo2.ClaveVoluntaria : tipo;
            if (efectivo.TablaDePi(s.Segmento, clave) is null)
                return Result.Failure<ContextoDePerdidaEsperada>(ErroresDeRiesgo.SinNorma(s.Fundamento == ModelBasis.Voluntary
                    ? $"el Anexo 2 vigente no trae la probabilidad de incumplimiento de la aplicación voluntaria en {s.Segmento}."
                    : $"el Anexo 2 vigente no trae la probabilidad de incumplimiento del tipo de organización {tipo} en {s.Segmento}."));
        }
        if (!anexo2.Value.Verified) sinValidar.Add($"document:{anexo2.Value.Clase}:{anexo2.Value.Codigo}");
        sinValidar.AddRange(anexo2.Value.Documento.TablasSinCotejar.Select(t => $"table:{RegulatoryDocumentKind.SarcAnexo2}:{t}"));

        var tipos = await normativa.TiposDeOrganizacionAsync(fecha, r, ct);
        var esFondo = (tipos.IsSuccess && tipos.Value.Documento.Buscar(tipo)?.IsEmployeeFund == true) || perfil?.EmployeeFundCategory is not null;

        var smmlv = await valores.LeerAsync(CodigosDeValorNacional.Smmlv, string.Empty, fecha, r, ct);
        if (smmlv.IsFailure && esFondo)
            return Result.Failure<ContextoDePerdidaEsperada>(ErroresDeRiesgo.SinNorma(
                $"no hay salario mínimo vigente el {fecha:yyyy-MM-dd} y el modelo lo usa para la cuota de un fondo de empleados. {smmlv.Error.Message}"));
        if (smmlv.IsSuccess && smmlv.Value.Estado != EstadoDeRevision.Validada) sinValidar.Add($"value:{CodigosDeValorNacional.Smmlv}");

        var reglaTexto = await TextoAsync(ParametrosDeCartera.RiesgoReglaMesesFaltantes, fecha, r, nameof(ReglaDeMesesFaltantes.MayorObservada), ct);
        if (reglaTexto.SinValidar) sinValidar.Add($"parameter:{ParametrosDeCartera.RiesgoReglaMesesFaltantes}");
        ReglaDeMesesFaltantes reglaDeMeses;
        try
        {
            reglaDeMeses = HistorialDeMora.LeerRegla(reglaTexto.Texto);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ContextoDePerdidaEsperada>(ErroresDeRiesgo.SinNorma(ex.Message));
        }
        int? diasFijos = null;
        if (reglaDeMeses == ReglaDeMesesFaltantes.Fija)
        {
            var dias = await parametros.LeerAsync(ParametrosDeCartera.RiesgoMesesFaltantesDiasFijos, fecha, r, ct: ct);
            if (dias.IsFailure || !int.TryParse(dias.Value.Texto, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture,
                    out var d))
                return Result.Failure<ContextoDePerdidaEsperada>(ErroresDeRiesgo.SinNorma(
                    $"la regla de meses faltantes es «Fija» y {ParametrosDeCartera.RiesgoMesesFaltantesDiasFijos} no tiene valor vigente."));
            diasFijos = d;
            if (dias.Value.DeLaCooperativa.Definicion.PendienteDeValidar is not null)
                sinValidar.Add($"parameter:{ParametrosDeCartera.RiesgoMesesFaltantesDiasFijos}");
        }

        var meses = Math.Max(regla?.MesesDeHistorial ?? 0, ContextoDePerdidaEsperada.VentanaDelDocumento(efectivo));
        return Result.Success(new ContextoDePerdidaEsperada(anexo2.Value, efectivo, tipo, esFondo, smmlv.IsSuccess ? smmlv.Value.Valor ?? 0m : 0m,
            reglaDeMeses, diasFijos, meses));
    }

    private static DocumentoUsado Usado<T>(DocumentoNormativoVigente<T> d) where T : class =>
        new(d.PublicId, d.Clase.ToString(), d.Codigo, d.ValidFrom, d.Sha256, d.Verified);

    private async Task<(string Texto, bool SinValidar)> TextoAsync(string clave, DateOnly fecha, DateTime? r, string defecto, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, r, ct: ct);
        return leido.IsSuccess
            ? (string.IsNullOrWhiteSpace(leido.Value.Texto) ? defecto : leido.Value.Texto, leido.Value.DeLaCooperativa.Definicion.PendienteDeValidar is not null)
            : (defecto, true);
    }

    private async Task<bool> LogicoAsync(string clave, DateOnly fecha, DateTime? r, bool defecto, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(clave, fecha, r, ct: ct);
        return leido.IsSuccess && leido.Value.Valor is bool b ? b : defecto;
    }
}
