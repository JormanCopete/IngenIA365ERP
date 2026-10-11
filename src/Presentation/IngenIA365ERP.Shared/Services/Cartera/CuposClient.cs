using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de los cupos y lo que los rodea (feature 013, US2, T0409; contracts/api.md §7.1, §7.2, §7.4 y los grupos conectados de
/// T0393): cupos con su disponible calculado, otorgar, terminar, revalidar y la preferencia del rotativo; bloqueos y embargos; los saldos de
/// aportes con la plantilla <c>lending.contribution-balances</c> (revisar y aplicar son dos operaciones con dos claves); y los grupos
/// conectados. Sin <c>Authorization</c> a mano; toda escritura con su <see cref="ClaveDeOperacion"/>; las consultas sin ella. (nuevo)
/// </summary>
public sealed class CuposClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeCupos = GobiernoDeCarteraClient.Base + "/credit-limits";
    public const string RutaDeBloqueos = GobiernoDeCarteraClient.Base + "/blocks";
    public const string RutaDeSaldosDeAportes = GobiernoDeCarteraClient.Base + "/contribution-balances";
    public const string RutaDeGrupos = GobiernoDeCarteraClient.Base + "/connected-groups";

    private readonly InventarioClient _plantillas = new(http, auth);

    // ------------------------------------------------------------------------------------------------ cupos --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<CupoDto>>> CuposAsync(FiltrosDeCupos? filtros = null, CancellationToken ct = default)
    {
        var f = filtros ?? new FiltrosDeCupos();
        return Enviar<PaginaDeInventarioDto<CupoDto>>(HttpMethod.Get, ConQuery(RutaDeCupos, Query(
            ("personPublicId", Texto(f.PersonPublicId)), ("kind", Texto(f.Kind)), ("status", f.Status), ("line", Texto(f.Line)),
            ("page", Texto(f.Page)), ("pageSize", Texto(f.PageSize)))), null, null, ct);
    }

    /// <summary>El disponible de la persona a la fecha (hoy si no viene); con la línea, también el aplicable.</summary>
    public Task<ResultadoDeInventario<DisponibleDto>> DisponibleAsync(Guid persona, Guid? linea = null, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<DisponibleDto>(HttpMethod.Get, ConQuery(RutaDeCupos + "/availability", Query(
            ("personPublicId", Texto(persona)), ("creditLinePublicId", Texto(linea)), ("date", Texto(fecha)))), null, null, ct);

    /// <summary>Otorga un cupo: <c>Active</c>, <c>PendingDecision</c> con su enrutamiento o <c>Blocked</c> con el aviso de listas.</summary>
    public Task<ResultadoDeInventario<CupoDto>> OtorgarAsync(OtorgarCupoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CupoDto>(HttpMethod.Post, RutaDeCupos, request, clave, ct);

    public Task<ResultadoDeInventario<CupoDto>> TerminarAsync(Guid cupo, DateOnly hasta, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CupoDto>(HttpMethod.Post, $"{RutaDeCupos}/{cupo}/end", new FinDeVigenciaRequest(hasta, motivo), clave, ct);

    public Task<ResultadoDeInventario<CupoDto>> RevalidarAsync(Guid cupo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CupoDto>(HttpMethod.Post, $"{RutaDeCupos}/{cupo}/revalidate", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>La preferencia de abono a capital del rotativo; la heredan los créditos que nazcan después.</summary>
    public Task<ResultadoDeInventario<CupoDto>> CambiarPreferenciaAsync(Guid cupo, PreferenciaDeAbonoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<CupoDto>(HttpMethod.Post, $"{RutaDeCupos}/{cupo}/prepayment-preference", request, clave, ct);

    // --------------------------------------------------------------------------------------------- bloqueos --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<BloqueoDto>>> BloqueosAsync(FiltrosDeBloqueos? filtros = null, CancellationToken ct = default)
    {
        var f = filtros ?? new FiltrosDeBloqueos();
        return Enviar<PaginaDeInventarioDto<BloqueoDto>>(HttpMethod.Get, ConQuery(RutaDeBloqueos, Query(
            ("personPublicId", Texto(f.PersonPublicId)), ("kind", Texto(f.Kind)), ("active", Texto(f.Active)), ("page", Texto(f.Page)),
            ("pageSize", Texto(f.PageSize)))), null, null, ct);
    }

    public Task<ResultadoDeInventario<BloqueoDto>> BloquearAsync(BloqueoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<BloqueoDto>(HttpMethod.Post, RutaDeBloqueos, request, clave, ct);

    /// <summary>Levanta un bloqueo manual o un embargo; uno automático responde <c>Lending.Block.Automatic</c> con cómo se levanta solo.</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> LevantarAsync(Guid bloqueo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeBloqueos}/{bloqueo}/lift", new MotivoDeCarteraRequest(motivo), clave, ct);

    // ------------------------------------------------------------------------------------- saldos de aportes --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<SaldoDeAportesDto>>> SaldosDeAportesAsync(Guid? persona = null, DateOnly? corte = null,
        int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<SaldoDeAportesDto>>(HttpMethod.Get, ConQuery(RutaDeSaldosDeAportes, Query(
            ("personPublicId", Texto(persona)), ("asOf", Texto(corte)), ("page", Texto(pagina)))), null, null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarPlantillaDeAportesAsync(CancellationToken ct = default) =>
        _plantillas.DescargarPlantillaAsync(RutaDeSaldosDeAportes, conDatos: false, ct);

    public Task<ResultadoDeInventario<ResultadoDeImportacionDto>> RevisarAportesAsync(string archivo, byte[] contenido, string? motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        _plantillas.RevisarPlantillaAsync(RutaDeSaldosDeAportes, archivo, contenido, motivo, clave, ct);

    public Task<ResultadoDeInventario<ResultadoDeImportacionDto>> AplicarAportesAsync(string archivo, byte[] contenido, string? motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        _plantillas.AplicarPlantillaAsync(RutaDeSaldosDeAportes, archivo, contenido, motivo, clave, ct);

    // ------------------------------------------------------------------------------------- grupos conectados --

    public Task<ResultadoDeInventario<IReadOnlyList<GrupoConectadoDto>>> GruposAsync(Guid? persona = null, bool incluirTerminados = false,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<GrupoConectadoDto>>(HttpMethod.Get, ConQuery(RutaDeGrupos, Query(
            ("personPublicId", Texto(persona)), ("includeEnded", incluirTerminados ? "true" : null))), null, null, ct);

    public Task<ResultadoDeInventario<GrupoConectadoDto>> RegistrarGrupoAsync(RegistrarGrupoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<GrupoConectadoDto>(HttpMethod.Post, RutaDeGrupos, request, clave, ct);

    public Task<ResultadoDeInventario<GrupoConectadoDto>> AgregarMiembroAsync(Guid grupo, MiembroDeGrupoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<GrupoConectadoDto>(HttpMethod.Post, $"{RutaDeGrupos}/{grupo}/members", request, clave, ct);

    public Task<ResultadoDeInventario<GrupoConectadoDto>> RetirarMiembroAsync(Guid grupo, Guid persona, string motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<GrupoConectadoDto>(HttpMethod.Post, $"{RutaDeGrupos}/{grupo}/members/{persona}/end", new MotivoDeCarteraRequest(motivo), clave, ct);

    public Task<ResultadoDeInventario<GrupoConectadoDto>> TerminarGrupoAsync(Guid grupo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<GrupoConectadoDto>(HttpMethod.Post, $"{RutaDeGrupos}/{grupo}/end", new MotivoDeCarteraRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
