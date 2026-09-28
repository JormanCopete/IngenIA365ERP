using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>
/// Cliente tipado del catálogo de medios de pago de Core (feature 012, I3, T633; contracts/api.md §22.1–§22.3): medios, franquicias,
/// adquirentes, datáfonos de cobro, denominaciones y —en el módulo comercial— dónde se ofrece cada medio. La cabecera
/// <c>Authorization</c> la pone el handler (<c>ElTokenDeSesionLoPoneElHandler</c>); las escrituras llevan la <c>Idempotency-Key</c> de la
/// operación de pantalla; los enums viajan por nombre. La plantilla 11 la maneja <c>ImportarPlantilla</c> con <see cref="RutaDeMedios"/>. (nuevo)
/// </summary>
public sealed class MediosDePagoClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeMedios = "/api/core/payment-means";
    public const string RutaDeFranquicias = "/api/core/card-networks";
    public const string RutaDeAdquirentes = "/api/core/card-acquirers";
    public const string RutaDeDatafonos = "/api/core/card-terminals";
    public const string RutaDeDenominaciones = "/api/core/cash-denominations";
    private const string RutaDeDisponibilidad = "/api/inventory/payment-means";

    // --------------------------------------------------------------------------------------- medios --

    /// <summary>Los medios; <paramref name="clase"/> es el número de <c>PaymentMeansClass</c> y viaja por nombre.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<MedioDePagoDto>>> ListarAsync(int? clase = null, bool? activos = null, DateOnly? vigentesAl = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<MedioDePagoDto>>(HttpMethod.Get, ConQuery(RutaDeMedios, Query(
            ("class", clase is { } c ? TextosDeVentas.NombreDeClaseDeMedio(c) : null), ("active", Texto(activos)), ("asOf", Texto(vigentesAl)))), null, null, ct);

    public Task<ResultadoDeInventario<MedioDePagoDto>> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        Enviar<MedioDePagoDto>(HttpMethod.Get, $"{RutaDeMedios}/{id}", null, null, ct);

    /// <summary>Sin <paramref name="id"/> crea y devuelve el nuevo; con él edita, y <paramref name="motivo"/> es obligatorio (§22.1).</summary>
    public async Task<ResultadoDeInventario<Guid>> GuardarAsync(Guid? id, MedioDePagoRequest medio, string? motivo, ClaveDeOperacion clave, CancellationToken ct = default)
    {
        if (id is { } existente)
        {
            medio.Reason = motivo;
            var r = await Enviar<EmptyResponse>(HttpMethod.Put, $"{RutaDeMedios}/{existente}", medio, clave, ct);
            return new(r.IsSuccess, existente, r.ErrorCode, r.ErrorMessage, r.StatusCode, r.Data, r.Repetida);
        }
        medio.Reason = null;
        var creado = await Enviar<MedioCreadoDto>(HttpMethod.Post, RutaDeMedios, medio, clave, ct);
        return new(creado.IsSuccess, creado.Value?.PaymentMeansPublicId ?? Guid.Empty, creado.ErrorCode, creado.ErrorMessage, creado.StatusCode, creado.Data,
            creado.Repetida);
    }

    public Task<ResultadoDeInventario<EmptyResponse>> BorrarAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Delete, $"{RutaDeMedios}/{id}", null, clave, ct);

    // ------------------------------------------------------------------------------- disponibilidad --

    public Task<ResultadoDeInventario<DisponibilidadDeMedioDto>> DisponibilidadAsync(Guid id, CancellationToken ct = default) =>
        Enviar<DisponibilidadDeMedioDto>(HttpMethod.Get, $"{RutaDeDisponibilidad}/{id}/availability", null, null, ct);

    public Task<ResultadoDeInventario<DisponibilidadDeMedioDto>> FijarDisponibilidadAsync(Guid id, DisponibilidadRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<DisponibilidadDeMedioDto>(HttpMethod.Put, $"{RutaDeDisponibilidad}/{id}/availability", request, clave, ct);

    // ------------------------------------------------------------------------------------ franquicias --

    public Task<ResultadoDeInventario<IReadOnlyList<FranquiciaDto>>> FranquiciasAsync(bool? activas = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<FranquiciaDto>>(HttpMethod.Get, ConQuery(RutaDeFranquicias, Query(("active", Texto(activas)))), null, null, ct);

    public Task<ResultadoDeInventario<FranquiciaDto>> GuardarFranquiciaAsync(Guid? id, FranquiciaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Guardar<FranquiciaDto>(RutaDeFranquicias, id, request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> BorrarFranquiciaAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Delete, $"{RutaDeFranquicias}/{id}", null, clave, ct);

    // ----------------------------------------------------------------------------------- adquirentes --

    public Task<ResultadoDeInventario<IReadOnlyList<AdquirenteDto>>> AdquirentesAsync(bool? activos = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<AdquirenteDto>>(HttpMethod.Get, ConQuery(RutaDeAdquirentes, Query(("active", Texto(activos)))), null, null, ct);

    public Task<ResultadoDeInventario<AdquirenteDto>> GuardarAdquirenteAsync(Guid? id, AdquirenteRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Guardar<AdquirenteDto>(RutaDeAdquirentes, id, request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> BorrarAdquirenteAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Delete, $"{RutaDeAdquirentes}/{id}", null, clave, ct);

    // ------------------------------------------------------------------------------------- datáfonos --

    public Task<ResultadoDeInventario<IReadOnlyList<DatafonoDto>>> DatafonosAsync(Guid? adquirente = null, bool? activos = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<DatafonoDto>>(HttpMethod.Get, ConQuery(RutaDeDatafonos, Query(("cardAcquirerPublicId", Texto(adquirente)), ("active", Texto(activos)))),
            null, null, ct);

    public Task<ResultadoDeInventario<DatafonoDto>> GuardarDatafonoAsync(Guid? id, DatafonoRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Guardar<DatafonoDto>(RutaDeDatafonos, id, request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> BorrarDatafonoAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Delete, $"{RutaDeDatafonos}/{id}", null, clave, ct);

    // -------------------------------------------------------------------------------- denominaciones --

    public Task<ResultadoDeInventario<IReadOnlyList<DenominacionDto>>> DenominacionesAsync(DateOnly? vigentesAl = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<DenominacionDto>>(HttpMethod.Get, ConQuery(RutaDeDenominaciones, Query(("asOf", Texto(vigentesAl)))), null, null, ct);

    public Task<ResultadoDeInventario<DenominacionDto>> GuardarDenominacionAsync(Guid? id, DenominacionRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Guardar<DenominacionDto>(RutaDeDenominaciones, id, request, clave, ct);

    public Task<ResultadoDeInventario<EmptyResponse>> BorrarDenominacionAsync(Guid id, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Delete, $"{RutaDeDenominaciones}/{id}", null, clave, ct);

    // ----------------------------------------------------------------------------------------- envío --

    private Task<ResultadoDeInventario<T>> Guardar<T>(string ruta, Guid? id, object request, ClaveDeOperacion clave, CancellationToken ct) =>
        id is { } existente
            ? Enviar<T>(HttpMethod.Put, $"{ruta}/{existente}", request, clave, ct)
            : Enviar<T>(HttpMethod.Post, ruta, request, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
