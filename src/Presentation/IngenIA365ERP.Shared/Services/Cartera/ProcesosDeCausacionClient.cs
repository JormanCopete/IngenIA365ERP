using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado del proceso de causación (2026-11-16, pedido del dueño; feature 024, fase 3; <c>/api/lending/accrual-processes</c>): las opciones
/// (conceptos con su modo y lo que espera), la lista, el detalle y las líneas —consultas, sin clave—; armar, recalcular, anular, pedir la firma y
/// reprocesar, y el desafío y la decisión del aprobador presente —escrituras, cada una con su <see cref="ClaveDeOperacion"/>—. Sin <c>Authorization</c>
/// a mano: la pone el handler de la sesión. (nuevo)
/// </summary>
public sealed class ProcesosDeCausacionClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = GobiernoDeCarteraClient.Base + "/accrual-processes";

    public Task<ResultadoDeInventario<OpcionesDeCausacionDto>> OpcionesAsync(CancellationToken ct = default) =>
        Enviar<OpcionesDeCausacionDto>(HttpMethod.Get, $"{Ruta}/options", null, null, ct);

    public Task<ResultadoDeInventario<PaginaDeCarteraDto<ProcesoDeCausacionEnListaDto>>> ListarAsync(string? estado = null, DateOnly? desde = null,
        DateOnly? hasta = null, int pagina = 1, int tamano = 20, CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<ProcesoDeCausacionEnListaDto>>(HttpMethod.Get, ConQuery(Ruta, Query(("status", estado), ("from", Texto(desde)),
            ("to", Texto(hasta)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<ProcesoDeCausacionDto>> ProcesoAsync(Guid proceso, CancellationToken ct = default) =>
        Enviar<ProcesoDeCausacionDto>(HttpMethod.Get, $"{Ruta}/{proceso}", null, null, ct);

    /// <summary>Las líneas por crédito y concepto, por estado, motivo, tipo de hecho y número de crédito.</summary>
    public Task<ResultadoDeInventario<PaginaDeCarteraDto<LineaDeCausacionDto>>> LineasAsync(Guid proceso, string? estado = null, string? motivo = null,
        string? tipoDeHecho = null, string? buscar = null, int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<LineaDeCausacionDto>>(HttpMethod.Get, ConQuery($"{Ruta}/{proceso}/lines", Query(("status", estado), ("reason", motivo),
            ("factType", tipoDeHecho), ("search", buscar), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>Arma el proceso y calcula su borrador (201). No contabiliza nada.</summary>
    public Task<ResultadoDeInventario<ProcesoDeCausacionDto>> ArmarAsync(ProcesoDeCausacionRequest pedido, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ProcesoDeCausacionDto>(HttpMethod.Post, Ruta, pedido, clave, ct);

    /// <summary>Recalcula el borrador, con lo mismo o con <paramref name="cambios"/>.</summary>
    public Task<ResultadoDeInventario<ProcesoDeCausacionDto>> RecalcularAsync(Guid proceso, ProcesoDeCausacionRequest? cambios, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ProcesoDeCausacionDto>(HttpMethod.Post, $"{Ruta}/{proceso}/recalculate", new RecalculoDeCausacionRequest(cambios), clave, ct);

    public Task<ResultadoDeInventario<ProcesoDeCausacionDto>> AnularAsync(Guid proceso, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ProcesoDeCausacionDto>(HttpMethod.Post, $"{Ruta}/{proceso}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>Pide poner en firme el borrador revisado (<paramref name="huella"/>): queda esperando la aprobación de otra persona presente.</summary>
    public Task<ResultadoDeInventario<ProcesoDeCausacionDto>> PedirFirmaAsync(Guid proceso, string motivo, string huella, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ProcesoDeCausacionDto>(HttpMethod.Post, $"{Ruta}/{proceso}/confirm", new FirmaDeCausacionRequest(motivo, huella), clave, ct);

    /// <summary>Reprocesa un proceso en firme: arma un borrador nuevo con lo mismo (201).</summary>
    public Task<ResultadoDeInventario<ProcesoDeCausacionDto>> ReprocesarAsync(Guid proceso, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ProcesoDeCausacionDto>(HttpMethod.Post, $"{Ruta}/{proceso}/reprocess", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>El desafío para que el aprobador presente decida en este equipo (con su correo).</summary>
    public Task<ResultadoDeInventario<DesafioDePresenciaDto>> DesafioDePresenciaAsync(Guid solicitud, string correo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DesafioDePresenciaDto>(HttpMethod.Post, $"{Ruta}/approvals/{solicitud}/presence-challenge", new DesafioDePresenciaRequest(correo), clave, ct);

    /// <summary>La decisión del aprobador presente, con su llave o su código.</summary>
    public Task<ResultadoDeInventario<ResultadoDeDecisionDto>> DecidirEnPersonaAsync(Guid solicitud, DecisionEnPersonaRequest decision, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ResultadoDeDecisionDto>(HttpMethod.Post, $"{Ruta}/approvals/{solicitud}/decide", decision, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
