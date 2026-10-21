using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado del desembolso y sus órdenes de giro (feature 013, US3, T0458; contracts/api.md §8.4, §9.1, §9.2): la vista previa (por POST y
/// sin clave: no escribe), desembolsar, los tramos desembolsados, las órdenes con su detalle, anular una orden, el giro nuevo y anular el crédito
/// pendiente de giro. Sin <c>Authorization</c> a mano; toda escritura con su <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class DesembolsosClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeDesembolsos = GobiernoDeCarteraClient.Base + "/disbursements";
    public const string RutaDeOrdenes = GobiernoDeCarteraClient.Base + "/disbursement-orders";
    public const string RutaDeCreditos = GobiernoDeCarteraClient.Base + "/loans";

    /// <summary>Lo que pasaría al desembolsar: usura, aprobación, garantías pendientes, recogidas, neto, plan y bloqueos. No escribe.</summary>
    public Task<ResultadoDeInventario<VistaPreviaDeDesembolsoDto>> VistaPreviaAsync(DesembolsoRequest request, CancellationToken ct = default) =>
        Enviar<VistaPreviaDeDesembolsoDto>(HttpMethod.Post, $"{OriginacionClient.RutaDeSolicitudes}/{request.ApplicationPublicId}/disbursement-preview",
            request, null, ct);

    public Task<ResultadoDeInventario<DesembolsoRealizadoDto>> DesembolsarAsync(DesembolsoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<DesembolsoRealizadoDto>(HttpMethod.Post, RutaDeDesembolsos, request, clave, ct);

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<TramoDesembolsadoDto>>> TramosAsync(Guid? credito = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<TramoDesembolsadoDto>>(HttpMethod.Get, ConQuery(RutaDeDesembolsos, Query(
            ("loanPublicId", Texto(credito)), ("page", Texto(pagina)))), null, null, ct);

    // ---------------------------------------------------------------------------------------- órdenes de giro --

    /// <summary>Las órdenes (<c>status</c> por su nombre: Issued, Disbursed, Rejected, CheckNotCashed, Voided).</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<OrdenDeGiroDto>>> OrdenesAsync(string? estado = null, Guid? credito = null, int pagina = 1,
        CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<OrdenDeGiroDto>>(HttpMethod.Get, ConQuery(RutaDeOrdenes, Query(
            ("status", estado), ("loanPublicId", Texto(credito)), ("page", Texto(pagina)))), null, null, ct);

    /// <summary>El detalle de una orden, con la cuenta completa.</summary>
    public Task<ResultadoDeInventario<OrdenDeGiroDto>> OrdenAsync(Guid orden, CancellationToken ct = default) =>
        Enviar<OrdenDeGiroDto>(HttpMethod.Get, $"{RutaDeOrdenes}/{orden}", null, null, ct);

    /// <summary>Pide a Tesorería anular la orden: sigue <c>Issued</c> hasta que Tesorería responda.</summary>
    public Task<ResultadoDeInventario<OrdenDeGiroDto>> AnularOrdenAsync(Guid orden, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<OrdenDeGiroDto>(HttpMethod.Post, $"{RutaDeOrdenes}/{orden}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>El giro nuevo de un crédito pendiente de giro (la orden nueva nombra a la anterior).</summary>
    public Task<ResultadoDeInventario<OrdenDeGiroDto>> GiroNuevoAsync(Guid orden, GiroNuevoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<OrdenDeGiroDto>(HttpMethod.Post, $"{RutaDeOrdenes}/{orden}/reissue", request, clave, ct);

    /// <summary>Anula un crédito pendiente de giro; reversa en la misma transacción las recogidas del desembolso.</summary>
    public Task<ResultadoDeInventario<CreditoAnuladoDto>> AnularCreditoAsync(Guid credito, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CreditoAnuladoDto>(HttpMethod.Post, $"{RutaDeCreditos}/{credito}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
