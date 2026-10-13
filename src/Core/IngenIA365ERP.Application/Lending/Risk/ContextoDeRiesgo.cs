using System.Text.Json;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Risk;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

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

    /// <summary>Los modelos que llevan deterioro general (en K1, el Anexo 1).</summary>
    public IReadOnlySet<ImpairmentModel> ModelosConGeneral { get; init; } = new HashSet<ImpairmentModel> { ImpairmentModel.ArrearsAnnex1 };

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
    PoliticaSarcVigente politicas)
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

        var seleccion = SelectorDeModeloDeDeterioro.Seleccionar(regla.IsSuccess ? regla.Value.Documento : null,
            perfil is null ? null : new PerfilParaModelo(perfil.OrganizationTypeCode, perfil.EmployeeFundCategory), null, CatalogoDeCartera.EntregaVigente);

        var usados = new List<DocumentoUsado>
        {
            Usado(anexo.Value),
            Usado(catalogo.Value),
        };
        if (regla.IsSuccess) usados.Add(Usado(regla.Value));

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
            DocumentosUsados = usados,
            SinValidar = sinValidar,
        });
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
