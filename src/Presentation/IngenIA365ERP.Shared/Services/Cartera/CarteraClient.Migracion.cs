using System.Net.Http.Headers;
using System.Security.Cryptography;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado de la migración y la activación de Cartera (feature 013, US8, T0758; contracts/api.md §19). Es <c>partial</c>: este archivo
/// lleva la base y las cargas (§19.1, §19.2) y <c>CarteraClient.Activacion.cs</c> la activación (§19.3). Las partes se suben por la API, archivo
/// por archivo, con el componente <c>ImportarPlantilla</c> sobre <see cref="RutaDeParte"/> (estas plantillas no van por la subida directa de
/// adjuntos); los cobros provisionales, con <see cref="RevisarCobrosProvisionalesAsync"/>. Toda escritura lleva su <see cref="ClaveDeOperacion"/>
/// (una por intento de operación); las consultas no. Sin <c>Authorization</c> a mano: la pone el handler de la sesión. (nuevo)
/// </summary>
public sealed partial class CarteraClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeCargas = GobiernoDeCarteraClient.Base + "/migration/loads";

    /// <summary>La ruta base de la plantilla de una parte de la carga (<c>…/parts/loans</c>): <c>/template.xlsx</c> e <c>/import</c> cuelgan de ella.</summary>
    public static string RutaDeParte(Guid carga, string parte) => $"{RutaDeCargas}/{carga}/parts/{Uri.EscapeDataString(parte)}";

    public Task<ResultadoDeInventario<IReadOnlyList<CargaDeMigracionDto>>> CargasAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CargaDeMigracionDto>>(HttpMethod.Get, RutaDeCargas, null, null, ct);

    public Task<ResultadoDeInventario<CargaDeMigracionDto>> CargaAsync(Guid carga, CancellationToken ct = default) =>
        Enviar<CargaDeMigracionDto>(HttpMethod.Get, $"{RutaDeCargas}/{carga}", null, null, ct);

    /// <summary>Una carga nueva en borrador con su corte; un borrador anterior queda reemplazado.</summary>
    public Task<ResultadoDeInventario<CargaDeMigracionDto>> CrearCargaAsync(CrearCargaRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CargaDeMigracionDto>(HttpMethod.Post, RutaDeCargas, request, clave, ct);

    /// <summary>
    /// Aplicar sólo guarda la carga: valida entre partes (todo o nada, <c>Lending.Migration.CrossPartErrors</c> con <c>data.errors[]</c>), calcula el
    /// cuadre y la conciliación de ventas, y deja la aplicada anterior reemplazada.
    /// </summary>
    public Task<ResultadoDeInventario<CargaAplicadaDto>> AplicarCargaAsync(Guid carga, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CargaAplicadaDto>(HttpMethod.Post, $"{RutaDeCargas}/{carga}/apply", new MotivoDeCarteraRequest(motivo), clave, ct);

    /// <summary>El cuadre contra Contabilidad (sin documentos ni nombres; <c>Lending.Reconciliation.View</c>).</summary>
    public Task<ResultadoDeInventario<CuadreDeMigracionDto>> CuadreAsync(Guid carga, CancellationToken ct = default) =>
        Enviar<CuadreDeMigracionDto>(HttpMethod.Get, $"{RutaDeCargas}/{carga}/balance-check", null, null, ct);

    /// <summary>Las ventas acumuladas hasta el corte con su clasificación (1 a 4; nula = todas).</summary>
    public Task<ResultadoDeInventario<PaginaDeCarteraDto<VentaDeMigracionDto>>> VentasAsync(Guid carga, int? clasificacion = null, int pagina = 1,
        int tamano = 100, CancellationToken ct = default) =>
        Enviar<PaginaDeCarteraDto<VentaDeMigracionDto>>(HttpMethod.Get, ConQuery($"{RutaDeCargas}/{carga}/reconciliation", Query(
            ("classification", Texto(clasificacion)), ("page", Texto(pagina)), ("pageSize", Texto(tamano)))), null, null, ct);

    /// <summary>Marca desde la lista una venta pendiente: 2 cancelada antes del corte, 4 fuera de Cartera (servicios a asociados).</summary>
    public Task<ResultadoDeInventario<VentaDeMigracionDto>> MarcarVentaAsync(Guid carga, MarcaDeVentaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<VentaDeMigracionDto>(HttpMethod.Post, $"{RutaDeCargas}/{carga}/reconciliation/marks", request, clave, ct);

    /// <summary>Acepta diferencias con el punto de una sesión cerrada (D17; <c>Lending.Migration.AcceptDifferences</c>).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<DiferenciaDeMigracionDto>>> AceptarDiferenciasAsync(Guid carga, AceptarDiferenciasRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<DiferenciaDeMigracionDto>>(HttpMethod.Post, $"{RutaDeCargas}/{carga}/differences/accept", request, clave, ct);

    // -------------------------------------------------------------------------------------------------------- envío --

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);

    private Task<InvitationApiResult<ArchivoDescargado>> Descargar(string url, string nombre, CancellationToken ct) =>
        EnvioDeVentas.DescargarAsync(http, auth, url, nombre, ct);

    /// <summary>Un archivo por multipart (campo <c>file</c>) con su clave; la huella es la ruta, el nombre y el SHA-256 del contenido.</summary>
    private async Task<ResultadoDeInventario<T>> SubirAsync<T>(string url, string archivo, byte[] contenido, ClaveDeOperacion clave, CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<T>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            var multipart = new MultipartFormDataContent();
            var bytes = new ByteArrayContent(contenido);
            bytes.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(bytes, "file", archivo);
            req.Content = multipart;
            clave.Aplicar(req, new { url, archivo, sha256 = Convert.ToHexString(SHA256.HashData(contenido)) });
            using var resp = await http.SendAsync(req, ct);
            var resultado = await ResultadoDeInventario<T>.DesdeAsync(resp, ct);
            if (resultado.IsSuccess) clave.Exito();
            return resultado;
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<T>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ResultadoDeInventario<T>.FormatoInesperado(ex.Message);
        }
    }
}
