using IngenIA365ERP.Shared.Services.Core;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Compliance;

/// <summary>
/// Cliente tipado de las solicitudes del titular (feature 013, fase 2, T0094; contracts/api.md §22.3): radicar (el plazo lo calcula la API
/// en días hábiles nacionales), prorrogar una sola vez y responder. Los soportes son adjuntos <c>DataSubjectRequest</c> de la persona, que se
/// suben antes con <c>SubirSoporte</c>. Sin <c>Authorization</c> a mano; las escrituras llevan <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public sealed class SolicitudesDelTitularClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/compliance/data-subject-requests";

    /// <summary>El tipo de dueño de los soportes de una solicitud (el dueño es la persona).</summary>
    public const string TipoDeSoporte = "DataSubjectRequest";

    /// <summary>Lista; <paramref name="estado"/> y <paramref name="clase"/> son los números de sus enums y viajan por nombre.</summary>
    public Task<ResultadoDeInventario<PaginaDePlataformaDto<SolicitudDelTitularDto>>> ListarAsync(int? estado = null, int? clase = null, Guid? persona = null,
        DateOnly? vencenAntesDe = null, int pagina = 1, int tamano = 50, CancellationToken ct = default) =>
        Enviar<PaginaDePlataformaDto<SolicitudDelTitularDto>>(HttpMethod.Get, ConQuery(Ruta, Query(
            ("status", estado is { } e ? NombreDeEstado(e) : null),
            ("kind", clase is { } c ? TextosDePlataforma.NombreDeClaseDeSolicitud(c) : null),
            ("personPublicId", Texto(persona)), ("dueBefore", Texto(vencenAntesDe)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    public Task<ResultadoDeInventario<SolicitudDelTitularDto>> ObtenerAsync(Guid solicitud, CancellationToken ct = default) =>
        Enviar<SolicitudDelTitularDto>(HttpMethod.Get, $"{Ruta}/{solicitud}", null, null, ct);

    public Task<ResultadoDeInventario<SolicitudDelTitularDto>> RadicarAsync(RadicarSolicitudRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<SolicitudDelTitularDto>(HttpMethod.Post, Ruta, request, clave, ct);

    public Task<ResultadoDeInventario<SolicitudDelTitularDto>> ProrrogarAsync(Guid solicitud, ProrrogaDeSolicitudRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SolicitudDelTitularDto>(HttpMethod.Post, $"{Ruta}/{solicitud}/extend", request, clave, ct);

    public Task<ResultadoDeInventario<SolicitudDelTitularDto>> ResponderAsync(Guid solicitud, RespuestaDeSolicitudRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<SolicitudDelTitularDto>(HttpMethod.Post, $"{Ruta}/{solicitud}/respond", request, clave, ct);

    private static string? NombreDeEstado(int estado) => estado switch
    {
        1 => "Received",
        2 => "Extended",
        3 => "Answered",
        4 => "Withdrawn",
        _ => null,
    };

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
