using System.Net.Http.Json;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Cartera;

public sealed record PlantillaDeFirmaDto(
    Guid PublicId,
    string Kind,
    string Code,
    string Name,
    string Body,
    IReadOnlyList<string> RepaymentChannels,
    IReadOnlyList<string> CreditLineCodes,
    bool IsActive,
    int PrintOrder,
    bool IncludeFingerprint,
    bool SignedByCoDebtors,
    int Version,
    bool IsVerified,
    DateTime? VerifiedAt,
    string? VerificationNote);

public sealed record CampoDeFirmaDto(
    Guid PublicId,
    string Code,
    string Label,
    string Kind,
    string Source,
    string? ConstantValue,
    string? DefaultValue,
    bool IsRequired,
    string? HelpText,
    int Order,
    bool IsActive);

public sealed record CampoDelSistemaDto(string Code, string Description, string Group, string Example);

public sealed record ListaDelSistemaDto(string Code, string Description, IReadOnlyList<CampoDelSistemaDto> Fields);

public sealed record ConfiguracionDeFirmaDto(
    IReadOnlyList<PlantillaDeFirmaDto> Templates,
    IReadOnlyList<CampoDeFirmaDto> Fields,
    IReadOnlyList<CampoDelSistemaDto> SystemFields,
    IReadOnlyList<ListaDelSistemaDto> SystemLists,
    IReadOnlyList<string> MissingBaseTemplates);

public sealed record PlantillaDeFirmaRequest(
    string Kind,
    string Code,
    string Name,
    string Body,
    IReadOnlyList<string>? RepaymentChannels,
    IReadOnlyList<string>? CreditLineCodes,
    bool IsActive,
    int PrintOrder,
    bool IncludeFingerprint,
    bool SignedByCoDebtors);

public sealed record CampoDeFirmaRequest(
    string Code,
    string Label,
    string Kind,
    string Source,
    string? ConstantValue,
    string? DefaultValue,
    bool IsRequired,
    string? HelpText,
    int Order,
    bool IsActive);

public sealed record VistaPreviaDeFirmaRequest(
    string? Name,
    string? Kind,
    string? Body,
    bool IncludeFingerprint,
    bool SignedByCoDebtors,
    Guid? ApplicationPublicId,
    Dictionary<string, string?>? Values);

public sealed record DocumentoDelJuegoDto(string Code, string Name, string Kind, int Version, bool Verified);

public sealed record JuegoDeFirmaDto(Guid PublicId, DateTime GeneratedAt, string Status, Guid AttachmentPublicId, IReadOnlyList<DocumentoDelJuegoDto> Documents);

public sealed record CampoDigitadoDto(string Code, string Label, string Kind, bool IsRequired, string? DefaultValue, string? HelpText);

public sealed record DocumentosParaFirmaDeSolicitudDto(
    bool CanGenerate,
    string? Reason,
    IReadOnlyList<DocumentoDelJuegoDto> Applicable,
    IReadOnlyList<CampoDigitadoDto> TypedFields,
    string? PromissoryNoteNumber,
    string? PromissoryNoteKind,
    IReadOnlyList<JuegoDeFirmaDto> Sets);

public sealed record JuegoGeneradoDto(JuegoDeFirmaDto Set, IReadOnlyList<string> EmptyFields, IReadOnlyList<string> Warnings);

public sealed record ValoresDeFirmaRequest(Dictionary<string, string?>? Values);

public sealed record NotaDeValidacionRequest(string? Note);

/// <summary>
/// Cliente tipado de los documentos para firma (2026-11-18, pedido del dueño): la configuración de plantillas y campos propios, la vista previa en
/// PDF y, en la solicitud, lo que aplica, los juegos generados y generar. Sin <c>Authorization</c> a mano; toda escritura lleva su
/// <see cref="ClaveDeOperacion"/> (generar, la de la pantalla, para que un reintento no genere dos juegos). (nuevo)
/// </summary>
public sealed class DocumentosParaFirmaClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDePlantillas = GobiernoDeCarteraClient.Base + "/signature-templates";
    public const string RutaDeCampos = GobiernoDeCarteraClient.Base + "/signature-fields";
    public const string RutaDeSolicitudes = GobiernoDeCarteraClient.Base + "/applications";

    public Task<ResultadoDeInventario<ConfiguracionDeFirmaDto>> ConfiguracionAsync(CancellationToken ct = default) =>
        Enviar<ConfiguracionDeFirmaDto>(HttpMethod.Get, RutaDePlantillas, null, null, ct);

    public Task<ResultadoDeInventario<PlantillaDeFirmaDto>> GuardarPlantillaAsync(Guid? plantilla, PlantillaDeFirmaRequest request, CancellationToken ct = default) =>
        plantilla is { } id
            ? Enviar<PlantillaDeFirmaDto>(HttpMethod.Put, $"{RutaDePlantillas}/{id}", request, new ClaveDeOperacion(), ct)
            : Enviar<PlantillaDeFirmaDto>(HttpMethod.Post, RutaDePlantillas, request, new ClaveDeOperacion(), ct);

    public Task<ResultadoDeInventario<object>> BorrarPlantillaAsync(Guid plantilla, CancellationToken ct = default) =>
        Enviar<object>(HttpMethod.Delete, $"{RutaDePlantillas}/{plantilla}", null, new ClaveDeOperacion(), ct);

    public Task<ResultadoDeInventario<PlantillaDeFirmaDto>> ValidarPlantillaAsync(Guid plantilla, string? nota, CancellationToken ct = default) =>
        Enviar<PlantillaDeFirmaDto>(HttpMethod.Post, $"{RutaDePlantillas}/{plantilla}/verify", new NotaDeValidacionRequest(nota), new ClaveDeOperacion(), ct);

    public Task<ResultadoDeInventario<ConfiguracionDeFirmaDto>> CargarBaseAsync(CancellationToken ct = default) =>
        Enviar<ConfiguracionDeFirmaDto>(HttpMethod.Post, $"{RutaDePlantillas}/load-base", null, new ClaveDeOperacion(), ct);

    public Task<ResultadoDeInventario<CampoDeFirmaDto>> GuardarCampoAsync(Guid? campo, CampoDeFirmaRequest request, CancellationToken ct = default) =>
        campo is { } id
            ? Enviar<CampoDeFirmaDto>(HttpMethod.Put, $"{RutaDeCampos}/{id}", request, new ClaveDeOperacion(), ct)
            : Enviar<CampoDeFirmaDto>(HttpMethod.Post, RutaDeCampos, request, new ClaveDeOperacion(), ct);

    public Task<ResultadoDeInventario<object>> BorrarCampoAsync(Guid campo, CancellationToken ct = default) =>
        Enviar<object>(HttpMethod.Delete, $"{RutaDeCampos}/{campo}", null, new ClaveDeOperacion(), ct);

    /// <summary>La vista previa en PDF (no guarda nada).</summary>
    public async Task<InvitationApiResult<ArchivoDescargado>> VistaPreviaAsync(VistaPreviaDeFirmaRequest request, CancellationToken ct = default)
    {
        if (auth.CurrentAccessToken is null)
            return InvitationApiResult<ArchivoDescargado>.Failure("Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, RutaDePlantillas + "/preview") { Content = JsonContent.Create(request) };
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return await CentralAuthApi.ParseAsync<ArchivoDescargado>(resp, ct);
            return InvitationApiResult<ArchivoDescargado>.Success(await EnvioDeVentas.ArchivoAsync(resp, "vista-previa.pdf", ct));
        }
        catch (HttpRequestException ex)
        {
            return InvitationApiResult<ArchivoDescargado>.NetworkError(ex.Message);
        }
    }

    public Task<ResultadoDeInventario<DocumentosParaFirmaDeSolicitudDto>> DeLaSolicitudAsync(Guid solicitud, CancellationToken ct = default) =>
        Enviar<DocumentosParaFirmaDeSolicitudDto>(HttpMethod.Get, $"{RutaDeSolicitudes}/{solicitud}/signature-documents", null, null, ct);

    public Task<ResultadoDeInventario<JuegoGeneradoDto>> GenerarAsync(Guid solicitud, Dictionary<string, string?> valores, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<JuegoGeneradoDto>(HttpMethod.Post, $"{RutaDeSolicitudes}/{solicitud}/signature-documents", new ValoresDeFirmaRequest(valores), clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
}
