using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de los mensajes al deudor (feature 013, fase 4, T0310; US17, C108; contracts/api.md §14.1 y §14.2): el registro de envíos con
/// su historia, el envío manual o programado dentro de la ventana legal, la cancelación de uno que no salió, la ventana de contacto permitida
/// (no escribe) y las plantillas versionadas con su vista previa (consulta por POST, sin clave). Sin <c>Authorization</c> a mano; las escrituras
/// llevan su <see cref="ClaveDeOperacion"/>, que en el envío es también la entidad de la clave del mensaje. (nuevo)
/// </summary>
public sealed class MensajesAlDeudorClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeMensajes = GobiernoDeCarteraClient.Base + "/debtor-messages";
    public const string RutaDePlantillas = GobiernoDeCarteraClient.Base + "/message-templates";

    // ---------------------------------------------------------------------------------------------- mensajes --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<MensajeAlDeudorDto>>> MensajesAsync(FiltrosDeMensajes filtros, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<MensajeAlDeudorDto>>(HttpMethod.Get, ConQuery(RutaDeMensajes, Query(
            ("loanPublicId", Texto(filtros.Loan)), ("personPublicId", Texto(filtros.Person)), ("purpose", Texto(filtros.Purpose)),
            ("channel", Texto(filtros.Channel)), ("status", Texto(filtros.Status)), ("from", Texto(filtros.From)), ("to", Texto(filtros.To)),
            ("page", Texto(filtros.Page)), ("pageSize", Texto(filtros.PageSize)))), null, null, ct);

    public Task<ResultadoDeInventario<DetalleDelMensajeDto>> MensajeAsync(Guid mensaje, CancellationToken ct = default) =>
        Enviar<DetalleDelMensajeDto>(HttpMethod.Get, $"{RutaDeMensajes}/{mensaje}", null, null, ct);

    /// <summary>Cuándo se le podría escribir a <paramref name="persona"/> por ese canal y con ese propósito; no escribe nada.</summary>
    public Task<ResultadoDeInventario<VentanaDeContactoDto>> VentanaDeContactoAsync(Guid persona, int canal, int proposito, DateTimeOffset? en = null,
        Guid? credito = null, CancellationToken ct = default) =>
        Enviar<VentanaDeContactoDto>(HttpMethod.Get, ConQuery($"{RutaDeMensajes}/contact-window", Query(
            ("person", Texto(persona)), ("channel", Texto(canal)), ("purpose", Texto(proposito)),
            ("at", en?.ToString("o", System.Globalization.CultureInfo.InvariantCulture)), ("loan", Texto(credito)))), null, null, ct);

    public Task<ResultadoDeInventario<MensajeAlDeudorDto>> EnviarMensajeAsync(EnviarMensajeRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<MensajeAlDeudorDto>(HttpMethod.Post, RutaDeMensajes, request, clave, ct);

    /// <summary>Cancela un mensaje que todavía no salió, con motivo.</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> CancelarMensajeAsync(Guid mensaje, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeMensajes}/{mensaje}/cancel", new MotivoDeCarteraRequest(motivo), clave, ct);

    // -------------------------------------------------------------------------------------------- plantillas --

    /// <summary>Las versiones vigentes (a <paramref name="fecha"/>), filtradas por propósito y canal.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<VersionDePlantillaDeMensajeDto>>> PlantillasAsync(int? proposito = null, int? canal = null,
        DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<VersionDePlantillaDeMensajeDto>>(HttpMethod.Get, ConQuery(RutaDePlantillas, Query(
            ("purpose", Texto(proposito)), ("channel", Texto(canal)), ("date", Texto(fecha)))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<VersionDePlantillaDeMensajeDto>>> HistorialDePlantillaAsync(string codigo, int? canal = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<VersionDePlantillaDeMensajeDto>>(HttpMethod.Get, ConQuery($"{RutaDePlantillas}/{Uri.EscapeDataString(codigo)}/history",
            Query(("channel", Texto(canal)))), null, null, ct);

    public Task<ResultadoDeInventario<VersionDePlantillaDeMensajeDto>> NuevaVersionDePlantillaAsync(string codigo, VersionDePlantillaDeMensajeRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<VersionDePlantillaDeMensajeDto>(HttpMethod.Post, $"{RutaDePlantillas}/{Uri.EscapeDataString(codigo)}/versions", request, clave, ct);

    /// <summary>La vista previa con datos de ejemplo: una consulta por POST, sin clave.</summary>
    public Task<ResultadoDeInventario<VistaPreviaDeMensajeDto>> VistaPreviaAsync(VistaPreviaDeMensajeRequest request, CancellationToken ct = default) =>
        Enviar<VistaPreviaDeMensajeDto>(HttpMethod.Post, $"{RutaDePlantillas}/preview", request, null, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
