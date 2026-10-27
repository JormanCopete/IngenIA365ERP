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
/// Cliente tipado de la libranza (feature 013, K2, US10, T0851; contracts/api.md §13 y las dos rutas de K2 de §20): pagadurías y convenios (lista,
/// detalle, alta, versión, contactos, pendientes de giro y la plantilla <c>lending.payroll-agreements</c>), archivos de descuento (lista, detalle,
/// vista previa por POST sin clave, generar, enlace firmado de 60 s sin clave, anular), no aplicados (lista y resolver), el traslado de una libranza
/// y lo que el recaudador de prueba hace con un archivo (confirmar el envío, devolver la respuesta de la pagaduría). Los formatos de ida y vuelta se
/// leen de <c>/api/core/bank-file-formats</c> por ámbito. Sin <c>Authorization</c> a mano; toda escritura con su <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class LibranzaClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeConvenios = GobiernoDeCarteraClient.Base + "/payroll-agreements";
    public const string RutaDeArchivos = GobiernoDeCarteraClient.Base + "/payroll-deduction-files";
    public const string RutaDeNoAplicados = GobiernoDeCarteraClient.Base + "/payroll-not-applied";
    public const string RutaDeCreditos = GobiernoDeCarteraClient.Base + "/loans";
    public const string RutaDeFormatos = "/api/core/bank-file-formats";
    public const string AmbitoDeIda = "PayrollDeductionRequest";
    public const string AmbitoDeVuelta = "PayrollDeductionResult";

    private readonly InventarioClient _plantillas = new(http, auth);

    // ----------------------------------------------------------------------------------------------- convenios --

    public Task<ResultadoDeInventario<IReadOnlyList<ConvenioResumenDto>>> ConveniosAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ConvenioResumenDto>>(HttpMethod.Get, ConQuery(RutaDeConvenios, Query(("includeInactive", Texto(incluirInactivos)))), null, null, ct);

    public Task<ResultadoDeInventario<ConvenioDto>> ConvenioAsync(Guid convenio, CancellationToken ct = default) =>
        Enviar<ConvenioDto>(HttpMethod.Get, $"{RutaDeConvenios}/{convenio}", null, null, ct);

    public Task<ResultadoDeInventario<ConvenioDto>> CrearAsync(ConvenioRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConvenioDto>(HttpMethod.Post, RutaDeConvenios, request, clave, ct);

    /// <summary>Una versión nueva: cierra la anterior la víspera.</summary>
    public Task<ResultadoDeInventario<ConvenioDto>> VersionAsync(Guid convenio, VersionDeConvenioRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ConvenioDto>(HttpMethod.Post, $"{RutaDeConvenios}/{convenio}/versions", request, clave, ct);

    /// <summary>Reemplaza los contactos vigentes.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<ContactoDeConvenioDto>>> ContactosAsync(Guid convenio, IReadOnlyList<ContactoDeConvenioRequest> contactos,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ContactoDeConvenioDto>>(HttpMethod.Put, $"{RutaDeConvenios}/{convenio}/contacts", new ContactosDeConvenioRequest(contactos), clave, ct);

    /// <summary>Lo descontado pendiente de giro del convenio por período, a <paramref name="fecha"/> (hoy si no viene).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<PendienteDeGiroDto>>> PendientesDeGiroAsync(Guid convenio, DateOnly? fecha = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<PendienteDeGiroDto>>(HttpMethod.Get, ConQuery($"{RutaDeConvenios}/{convenio}/pending-transfers", Query(("asOf", Texto(fecha)))),
            null, null, ct);

    // ------------------------------------------------- feature 014 (T094; api.md §18.3): grupos de archivo y equivalencias --

    /// <summary>Los grupos de archivo que rigen el <paramref name="fecha"/> (hoy si no viene).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<GrupoDeArchivoDto>>> GruposAsync(Guid convenio, DateOnly? fecha = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<GrupoDeArchivoDto>>(HttpMethod.Get, ConQuery($"{RutaDeConvenios}/{convenio}/file-groups", Query(("date", Texto(fecha)))), null, null, ct);

    /// <summary>Fija los grupos de archivo desde una fecha: registra una versión nueva del convenio.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<GrupoDeArchivoDto>>> FijarGruposAsync(Guid convenio, GruposDeArchivoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<GrupoDeArchivoDto>>(HttpMethod.Post, $"{RutaDeConvenios}/{convenio}/file-groups", request, clave, ct);

    /// <summary>Las equivalencias de concepto que rigen el <paramref name="fecha"/> (hoy si no viene).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<EquivalenciaDeConceptoDto>>> EquivalenciasAsync(Guid convenio, DateOnly? fecha = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<EquivalenciaDeConceptoDto>>(HttpMethod.Get, ConQuery($"{RutaDeConvenios}/{convenio}/concept-equivalences", Query(("date", Texto(fecha)))),
            null, null, ct);

    /// <summary>Fija las equivalencias de concepto desde una fecha: registra una versión nueva del convenio.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<EquivalenciaDeConceptoDto>>> FijarEquivalenciasAsync(Guid convenio, EquivalenciasDeConceptoRequest request,
        ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<EquivalenciaDeConceptoDto>>(HttpMethod.Post, $"{RutaDeConvenios}/{convenio}/concept-equivalences", request, clave, ct);

    /// <summary>Los formatos activos de un ámbito de la libranza (<see cref="AmbitoDeIda"/> o <see cref="AmbitoDeVuelta"/>).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<FormatoBancarioResumenDto>>> FormatosAsync(string ambito, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<FormatoBancarioResumenDto>>(HttpMethod.Get, ConQuery(RutaDeFormatos, Query(("scope", ambito), ("onlyActive", "true"))), null, null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarPlantillaAsync(CancellationToken ct = default) =>
        _plantillas.DescargarPlantillaAsync(RutaDeConvenios, conDatos: false, ct);

    public Task<ResultadoDeInventario<ResultadoDeImportacionDto>> RevisarPlantillaAsync(string archivo, byte[] contenido, string? motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        _plantillas.RevisarPlantillaAsync(RutaDeConvenios, archivo, contenido, motivo, clave, ct);

    public Task<ResultadoDeInventario<ResultadoDeImportacionDto>> AplicarPlantillaAsync(string archivo, byte[] contenido, string? motivo, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        _plantillas.AplicarPlantillaAsync(RutaDeConvenios, archivo, contenido, motivo, clave, ct);

    // ------------------------------------------------------------------------------------------------ archivos --

    public Task<ResultadoDeInventario<PaginaDeInventarioDto<ArchivoDeDescuentosDto>>> ArchivosAsync(Guid? convenio = null, int? anio = null, int? mes = null,
        string? estado = null, int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<ArchivoDeDescuentosDto>>(HttpMethod.Get, ConQuery(RutaDeArchivos, Query(("agreementPublicId", Texto(convenio)),
            ("year", Texto(anio)), ("month", Texto(mes)), ("status", estado), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<ArchivoDeDescuentosDetalleDto>> ArchivoAsync(Guid archivo, int pagina = 1, CancellationToken ct = default) =>
        Enviar<ArchivoDeDescuentosDetalleDto>(HttpMethod.Get, ConQuery($"{RutaDeArchivos}/{archivo}", Query(("page", Texto(pagina)))), null, null, ct);

    /// <summary>Lo que saldría, sin escribir: va por POST y sin clave.</summary>
    public Task<ResultadoDeInventario<VistaPreviaDeDescuentosDto>> VistaPreviaAsync(ArchivoDeDescuentosRequest request, CancellationToken ct = default) =>
        Enviar<VistaPreviaDeDescuentosDto>(HttpMethod.Post, RutaDeArchivos + "/preview", request, null, ct);

    /// <summary>Genera o, antes del envío, regenera el archivo del período (la versión anterior queda reemplazada).</summary>
    public Task<ResultadoDeInventario<ArchivoDeDescuentosDto>> GenerarAsync(ArchivoDeDescuentosRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ArchivoDeDescuentosDto>(HttpMethod.Post, RutaDeArchivos, request, clave, ct);

    /// <summary>Un enlace firmado de 60 s (auditado). Va por POST y sin clave.</summary>
    public Task<ResultadoDeInventario<EnlaceDeCarteraDto>> EnlaceAsync(Guid archivo, CancellationToken ct = default) =>
        Enviar<EnlaceDeCarteraDto>(HttpMethod.Post, $"{RutaDeArchivos}/{archivo}/download-link", null, null, ct);

    /// <summary>Feature 014 (T091): las filas de la respuesta de la pagaduría que no se aplicaron (empleado o código sin equivalencia, lo que sobró).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<FilaNoAplicadaDePagaduriaDto>>> FilasNoAplicadasAsync(Guid archivo, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<FilaNoAplicadaDePagaduriaDto>>(HttpMethod.Get, $"{RutaDeArchivos}/{archivo}/unmatched", null, null, ct);

    /// <summary>Anula un archivo que todavía no se envió.</summary>
    public Task<ResultadoDeInventario<EmptyResponse>> AnularAsync(Guid archivo, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<EmptyResponse>(HttpMethod.Post, $"{RutaDeArchivos}/{archivo}/void", new MotivoDeCarteraRequest(motivo), clave, ct);

    // ------------------------------------------------------------------------------------ no aplicados y traslado --

    /// <summary>Los no aplicados (<c>origin</c> Contract, Activation o PaymasterResult; <c>status</c> Open o Resolved).</summary>
    public Task<ResultadoDeInventario<PaginaDeInventarioDto<NoAplicadoDeLibranzaDto>>> NoAplicadosAsync(Guid? convenio = null, string? origen = null,
        string? estado = null, int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeInventarioDto<NoAplicadoDeLibranzaDto>>(HttpMethod.Get, ConQuery(RutaDeNoAplicados, Query(("agreementPublicId", Texto(convenio)),
            ("origin", origen), ("status", estado), ("page", Texto(pagina)))), null, null, ct);

    public Task<ResultadoDeInventario<NoAplicadoDeLibranzaDto>> ResolverAsync(Guid noAplicado, ResolucionDeNoAplicadoRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<NoAplicadoDeLibranzaDto>(HttpMethod.Post, $"{RutaDeNoAplicados}/{noAplicado}/resolve", request, clave, ct);

    public Task<ResultadoDeInventario<TrasladoDeLibranzaDto>> TrasladarAsync(Guid credito, TrasladoDeLibranzaRequest request, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<TrasladoDeLibranzaDto>(HttpMethod.Post, $"{RutaDeCreditos}/{credito}/payroll-transfer", request, clave, ct);

    /// <summary>
    /// Lee un archivo de prueba con un formato guardado (T0832), sin guardar nada: la vista previa del formato de vuelta. Es una consulta por POST
    /// (multipart, campo <c>file</c>) y va sin clave.
    /// </summary>
    public async Task<ResultadoDeInventario<LecturaDeFormatoDto>> LeerConFormatoAsync(Guid formato, string nombre, byte[] contenido, CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<LecturaDeFormatoDto>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{RutaDeFormatos}/{formato}/read-preview");
            var multipart = new MultipartFormDataContent();
            var bytes = new ByteArrayContent(contenido);
            bytes.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            multipart.Add(bytes, "file", nombre);
            req.Content = multipart;
            using var resp = await http.SendAsync(req, ct);
            return await ResultadoDeInventario<LecturaDeFormatoDto>.DesdeAsync(resp, ct);
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<LecturaDeFormatoDto>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ResultadoDeInventario<LecturaDeFormatoDto>.FormatoInesperado(ex.Message);
        }
    }

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
