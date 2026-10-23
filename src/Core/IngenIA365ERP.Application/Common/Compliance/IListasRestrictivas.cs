using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using ScreeningContext = IngenIA365ERP.Domain.Enums.Lending.ScreeningContext;

namespace IngenIA365ERP.Application.Common.Compliance;

// Feature 013 (T0081 adelanta lo que T0087 necesita del puerto; T70, T71; contracts/adaptadores-y-archivos.md §3.2, §3.8;
// data-model parte 3 §4.8): la consulta de listas restrictivas de la plataforma. Las entidades COR_RestrictiveListLoads/Entries, su
// importación y sus rutas son de T0087; aquí está el puerto, su registro y la lectura de la carga vigente que usa CARGA-PROPIA.

/// <summary>A quién se consulta (§3.2). (nuevo)</summary>
public sealed record ConsultaDeListas(Guid PersonaPublicId, string TipoDeDocumento, string NumeroDeDocumento, string NombreCompleto);

/// <summary>
/// Una coincidencia (§3.2): en qué lista y versión, contra qué nombre, si fue por documento (exacta) o por nombre, y con qué similitud.
/// (nuevo)
/// </summary>
public sealed record CoincidenciaEnLista(
    string ListCode,
    int Version,
    string NombreEnLista,
    string? DocumentoEnLista,
    bool PorDocumento,
    decimal Similitud,
    string? Programa);

/// <summary>Qué versión de cada lista se consultó (§3.2): la operación la sella. (nuevo)</summary>
public sealed record VersionDeListaConsultada(string ListCode, int Version, DateOnly ValidFrom, DateOnly? PublishedOn);

/// <summary>El resultado de la consulta (§3.2): <c>NoMatch</c>, <c>PossibleMatch</c> o <c>Match</c>, con las coincidencias y las versiones. (nuevo)</summary>
public sealed record ResultadoDeListas(
    ScreeningOutcome Resultado,
    IReadOnlyList<CoincidenciaEnLista> Coincidencias,
    IReadOnlyList<VersionDeListaConsultada> Versiones);

/// <summary>Lo que ofrece un canal de listas (§3.2): coincidencia por documento, por nombre y si informa la versión consultada. (nuevo)</summary>
public sealed record CapacidadesDeListas(bool PorDocumento, bool PorNombre, bool InformaVersion);

/// <summary>
/// Un canal de listas restrictivas (feature 013; T70, T71; §3.2, §3.8): <c>CARGA-PROPIA</c> (<c>CanalDeListasCargadas</c>, vale en
/// producción), <c>SIMULADO</c> (<c>ListasSimuladas</c>) y, después, un proveedor comercial. Cumple §3.1 (<c>ConformidadDeListasRestrictivas</c>).
/// (nuevo)
/// </summary>
public interface IListasRestrictivas
{
    string ChannelCode { get; }

    CapacidadesDeListas Capacidades { get; }

    Task<ResultadoExterno<ResultadoDeListas>> ConsultarAsync(ConsultaDeListas consulta, ContextoDeCanalExterno contexto, CancellationToken ct);

    Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct);
}

/// <summary>
/// El registro de los canales de listas (§3.1): resuelve por el código sellado; un código sin adaptador devuelve uno que responde
/// <c>ChannelUnavailable</c>. Sin adaptadores registrados (sin <c>AddExternalChannels</c>) es el «sin proveedores» de T0087. (nuevo)
/// </summary>
public interface IProveedoresDeListas : IRegistroDeCanalesExternos
{
    IReadOnlyCollection<string> Codigos { get; }

    IListasRestrictivas Resolver(string channelCode);
}

/// <summary>
/// Una entrada de la carga vigente, ya normalizada (data-model §4.8, <c>COR_RestrictiveListEntries</c>): lo que <c>CARGA-PROPIA</c>
/// compara en memoria. (nuevo)
/// </summary>
public sealed record EntradaDeListaRestrictiva(
    string ListCode,
    int Version,
    RestrictiveListEntityKind EntityKind,
    string? DocumentType,
    string? DocumentNumberNormalized,
    string FullName,
    string NameNormalized,
    IReadOnlyList<string> Aliases,
    string? Program);

/// <summary>La carga vigente de todas las listas a una fecha: sus versiones y sus entradas. (nuevo)</summary>
public sealed record CargaDeListasRestrictivas(IReadOnlyList<VersionDeListaConsultada> Versiones, IReadOnlyList<EntradaDeListaRestrictiva> Entradas)
{
    public static CargaDeListasRestrictivas Vacia { get; } = new([], []);
}

/// <summary>
/// Lee la carga vigente de las listas a una fecha (feature 013, T0081; data-model §4.8): por cada <c>ListCode</c>, la carga de mayor
/// <c>ValidFrom ≤ fecha</c>. La implementación sobre <c>COR_RestrictiveListLoads</c>/<c>Entries</c> es
/// <c>Compliance.RestrictiveLists.CargaDeListasRestrictivasEnBase</c> (T0087); el defecto <see cref="SinCargaDeListasRestrictivas"/> (sin
/// listas) queda para las pruebas. (nuevo)
/// </summary>
public interface ICargaDeListasRestrictivas
{
    Task<CargaDeListasRestrictivas> LeerVigenteAsync(DateOnly fecha, CancellationToken ct);
}

/// <summary>
/// El efecto de una coincidencia según el contexto de la consulta (feature 015, T0025; R-24; FR-010, FR-098, FR-203; D22). En el recaudo
/// (<see cref="ScreeningContext.Collection"/>) y en el giro (<see cref="ScreeningContext.Disbursement"/>) una coincidencia en una lista
/// <b>vinculante</b> bloquea siempre: lo impone la norma y no hay parámetro para que «siga» (<c>Receipts.Compliance.BindingListMatch</c>,
/// <c>Treasury.Compliance.BindingListMatch</c>). Una coincidencia en una lista no vinculante sigue el parámetro
/// <c>Sarlaft.ListasNoVinculantes.Efecto</c> (<c>Sigue</c> o <c>Retiene</c>). Los contextos de Cartera conservan su tratamiento. (nuevo)
/// </summary>
public static class EfectoDeListasPorContexto
{
    /// <summary>¿Una coincidencia vinculante en este contexto bloquea sin excepción posible?</summary>
    public static bool LaVinculanteBloqueaSiempre(ScreeningContext contexto) =>
        contexto is ScreeningContext.Collection or ScreeningContext.Disbursement;

    /// <summary>¿El contexto es de Recaudo o Tesorería (feature 015)?</summary>
    public static bool EsDeRecaudoOTesoreria(ScreeningContext contexto) =>
        contexto is ScreeningContext.Collection or ScreeningContext.Disbursement;
}

/// <summary>El defecto sin listas cargadas: toda consulta de <c>CARGA-PROPIA</c> responde <c>NoMatch</c> sin versiones. (nuevo)</summary>
public sealed class SinCargaDeListasRestrictivas : ICargaDeListasRestrictivas
{
    public Task<CargaDeListasRestrictivas> LeerVigenteAsync(DateOnly fecha, CancellationToken ct) => Task.FromResult(CargaDeListasRestrictivas.Vacia);
}

/// <summary>
/// El registro de canales de listas (§3.1): por código sin distinguir mayúsculas; dos con el mismo código lanzan al construirse; uno sin
/// adaptador responde <c>ChannelUnavailable</c>; las consultas pasan por el circuito de la cooperativa (§3.5). (nuevo)
/// </summary>
public sealed class ProveedoresDeListas : IProveedoresDeListas
{
    private static readonly ExternalChannelKind[] ClasesAtendidas = [ExternalChannelKind.RestrictiveLists];

    private readonly Dictionary<string, IListasRestrictivas> _porCodigo;
    private readonly ICircuitoDeCanalExterno _circuito;

    public ProveedoresDeListas(IEnumerable<IListasRestrictivas> canales, ICircuitoDeCanalExterno circuito)
    {
        ArgumentNullException.ThrowIfNull(canales);
        _circuito = circuito ?? throw new ArgumentNullException(nameof(circuito));
        _porCodigo = new Dictionary<string, IListasRestrictivas>(StringComparer.OrdinalIgnoreCase);
        foreach (var canal in canales)
        {
            if (string.IsNullOrWhiteSpace(canal.ChannelCode))
                throw new InvalidOperationException($"El adaptador {canal.GetType().Name} no declara su ChannelCode.");
            if (!_porCodigo.TryAdd(canal.ChannelCode.Trim(), canal))
                throw new InvalidOperationException($"Dos adaptadores de listas declaran el canal «{canal.ChannelCode}».");
        }
        Codigos = _porCodigo.Keys.Order(StringComparer.Ordinal).ToList();
    }

    public IReadOnlyCollection<string> Codigos { get; }

    public IReadOnlyCollection<ExternalChannelKind> Clases => ClasesAtendidas;

    public IReadOnlyCollection<string> CodigosPara(ExternalChannelKind clase) => clase == ExternalChannelKind.RestrictiveLists ? Codigos : [];

    public IListasRestrictivas Resolver(string channelCode) =>
        !string.IsNullOrWhiteSpace(channelCode) && _porCodigo.TryGetValue(channelCode.Trim(), out var canal)
            ? new ListasVigiladas(canal, _circuito)
            : new ListasNoInstaladas(channelCode?.Trim() ?? string.Empty);

    public Task<ResultadoExterno<bool>> ProbarAsync(ExternalChannelKind clase, string channelCode, ContextoDeCanalExterno contexto, CancellationToken ct) =>
        Resolver(channelCode).ProbarAsync(contexto, ct);

    private sealed class ListasNoInstaladas(string channelCode) : IListasRestrictivas
    {
        public string ChannelCode { get; } = channelCode;

        public CapacidadesDeListas Capacidades { get; } = new(false, false, false);

        public Task<ResultadoExterno<ResultadoDeListas>> ConsultarAsync(ConsultaDeListas consulta, ContextoDeCanalExterno contexto, CancellationToken ct) =>
            Task.FromResult(ResultadoExterno<ResultadoDeListas>.NoInstalado(ChannelCode));

        public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) =>
            Task.FromResult(ResultadoExterno<bool>.NoInstalado(ChannelCode));
    }

    private sealed class ListasVigiladas(IListasRestrictivas canal, ICircuitoDeCanalExterno circuito) : IListasRestrictivas
    {
        public string ChannelCode => canal.ChannelCode;

        public CapacidadesDeListas Capacidades => canal.Capacidades;

        public async Task<ResultadoExterno<ResultadoDeListas>> ConsultarAsync(ConsultaDeListas consulta, ContextoDeCanalExterno contexto, CancellationToken ct)
        {
            if (!await circuito.PuedeLlamarAsync(ExternalChannelKind.RestrictiveLists, ChannelCode,
                    async c => (await canal.ProbarAsync(contexto, c)).Outcome, ct))
            {
                return ResultadoExterno<ResultadoDeListas>.Sin(ExternalOutcome.ChannelUnavailable, ChannelCode, CodigosDeCanalExterno.EsSimulado(ChannelCode),
                    $"El circuito del canal {ChannelCode} está abierto: no se llamó al proveedor.");
            }
            var r = await canal.ConsultarAsync(consulta, contexto, ct);
            await circuito.RegistrarAsync(ExternalChannelKind.RestrictiveLists, ChannelCode, r.Outcome, ct);
            return r;
        }

        public Task<ResultadoExterno<bool>> ProbarAsync(ContextoDeCanalExterno contexto, CancellationToken ct) => canal.ProbarAsync(contexto, ct);
    }
}
