using System.Security.Cryptography;
using System.Text.Json;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.SignatureDocuments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SignatureDocuments;

/// <summary>Un documento de un juego: su código, nombre, clase, la versión de la plantilla y si estaba validada.</summary>
public sealed record SignatureSetDocumentDto(string Code, string Name, string Kind, int Version, bool Verified);

/// <summary>Un juego generado.</summary>
public sealed record SignatureDocumentSetDto(
    Guid PublicId,
    DateTime GeneratedAt,
    string Status,
    Guid AttachmentPublicId,
    IReadOnlyList<SignatureSetDocumentDto> Documents);

/// <summary>Un campo propio que se digita al generar.</summary>
public sealed record SignatureTypedFieldDto(string Code, string Label, string Kind, bool IsRequired, string? DefaultValue, string? HelpText);

/// <summary>
/// Lo que la pantalla de la solicitud necesita para los documentos para firma: si se pueden generar y por qué no, las plantillas que aplican, los
/// campos que se digitan, el pagaré y los juegos generados.
/// </summary>
public sealed record ApplicationSignatureDocumentsDto(
    bool CanGenerate,
    string? Reason,
    IReadOnlyList<SignatureSetDocumentDto> Applicable,
    IReadOnlyList<SignatureTypedFieldDto> TypedFields,
    string? PromissoryNoteNumber,
    string? PromissoryNoteKind,
    IReadOnlyList<SignatureDocumentSetDto> Sets);

/// <summary>Lo que responde la generación: el juego, los campos que salieron vacíos (con raya para llenar a mano) y los avisos.</summary>
public sealed record GeneratedSignatureDocumentsDto(SignatureDocumentSetDto Set, IReadOnlyList<string> EmptyFields, IReadOnlyList<string> Warnings);

/// <summary>Las plantillas que aplican a una solicitud y el pagaré vivo que tiene.</summary>
internal static class SeleccionDePlantillas
{
    public static async Task<(List<SignatureDocumentTemplate> Plantillas, PromissoryNote? Pagare, string? CodigoDeLinea)> LeerAsync(IApplicationDbContext db,
        CreditApplication s, bool seguirPagare, CancellationToken ct)
    {
        var linea = await db.CreditLines.AsNoTracking().Where(l => l.Id == s.CreditLineId).Select(l => l.Code).FirstOrDefaultAsync(ct);
        var todas = await db.SignatureDocumentTemplates.AsNoTracking().Where(t => !t.IsDeleted && t.IsActive).ToListAsync(ct);
        var consulta = seguirPagare ? db.PromissoryNotes : db.PromissoryNotes.AsNoTracking();
        var pagare = await consulta
            .Where(p => p.ApplicationId == s.Id && p.LoanId == null && !p.IsDeleted && p.Status != PromissoryNoteStatus.Voided)
            .OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
        // El desmaterializado se firma en el depósito de valores: el pagaré y su carta no se imprimen.
        var desmaterializado = pagare?.Kind == PromissoryNoteKind.Dematerialized;
        var plantillas = todas.Where(t => t.Aplica(s.RepaymentChannel, linea))
            .Where(t => !desmaterializado || t.Kind is not (SignatureDocumentKind.PromissoryNote or SignatureDocumentKind.InstructionLetter))
            .OrderBy(t => t.Kind).ThenBy(t => t.PrintOrder).ThenBy(t => t.Code).ToList();
        return (plantillas, pagare, linea);
    }

    public static IReadOnlyList<SignatureSetDocumentDto> Documentos(IEnumerable<SignatureDocumentTemplate> plantillas) =>
        plantillas.Select(t => new SignatureSetDocumentDto(t.Code, t.Name, t.Kind.ToString(), t.Version, t.IsVerified)).ToList();

    public static SignatureDocumentSetDto Dto(SignatureDocumentSet j) => new(j.PublicId, j.GeneratedAt, j.Status.ToString(), j.AttachmentPublicId,
        JsonSerializer.Deserialize<List<SignatureSetDocumentDto>>(j.DocumentsJson, GenerateSignatureDocumentsCommandHandler.Json) ?? []);

    public static string? PorQueNo(CreditApplication s, DateOnly hoy) =>
        s.Status != CreditApplicationStatus.Approved
            ? ErroresDeDocumentosParaFirma.NoAprobada(s.Status.ToString()).Message
            : s.AprobacionVencida(hoy) ? "La aprobación de la solicitud venció: vuelva a decidirla antes de generar los documentos." : null;
}

/// <summary><c>GET /api/lending/applications/{id}/signature-documents</c> (<c>Lending.Applications.Read</c>).</summary>
public sealed record GetApplicationSignatureDocumentsQuery(Guid ApplicationPublicId) : IRequest<Result<ApplicationSignatureDocumentsDto>>;

public sealed class GetApplicationSignatureDocumentsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetApplicationSignatureDocumentsQuery, Result<ApplicationSignatureDocumentsDto>>
{
    public async Task<Result<ApplicationSignatureDocumentsDto>> Handle(GetApplicationSignatureDocumentsQuery request, CancellationToken ct)
    {
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, request.ApplicationPublicId, seguir: false, ct);
        if (leida.IsFailure) return Result.Failure<ApplicationSignatureDocumentsDto>(leida.Error);
        var s = leida.Value;
        var (plantillas, pagare, _) = await SeleccionDePlantillas.LeerAsync(db, s, seguirPagare: false, ct);
        var usados = plantillas.SelectMany(t => PlantillaDeFirma.CamposUsados(t.Body)).ToHashSet(StringComparer.Ordinal);
        var propios = await CamposPropios.LeerAsync(db, ct);
        var digitados = propios.Campos.Where(f => f.Source == SignatureFieldSource.Typed
                && (usados.Contains(f.Code) || usados.Contains(f.Code + CamposDelSistema.SufijoLetras)))
            .Select(f => new SignatureTypedFieldDto(f.Code, f.Label, f.Kind.ToString(), f.IsRequired, f.DefaultValue, f.HelpText)).ToList();
        var juegos = await db.SignatureDocumentSets.AsNoTracking().Where(j => j.ApplicationId == s.Id && !j.IsDeleted)
            .OrderByDescending(j => j.GeneratedAt).ToListAsync(ct);

        var porQueNo = SeleccionDePlantillas.PorQueNo(s, reloj.HoyLocal)
            ?? (plantillas.Count == 0 ? ErroresDeDocumentosParaFirma.SinPlantillas().Message : null);
        return Result.Success(new ApplicationSignatureDocumentsDto(porQueNo is null, porQueNo, SeleccionDePlantillas.Documentos(plantillas), digitados,
            pagare?.Number ?? (pagare is null ? s.Number : null), pagare?.Kind.ToString(), juegos.Select(SeleccionDePlantillas.Dto).ToList()));
    }
}

/// <summary>
/// <c>POST /api/lending/applications/{id}/signature-documents</c> (2026-11-18, pedido del dueño; <c>Lending.PromissoryNotes.Manage</c>, con clave):
/// genera en un PDF los documentos para firma de una solicitud <b>aprobada y vigente</b> con las plantillas activas que le aplican (por canal de pago
/// y línea): el pagaré, la carta de instrucciones, la libranza cuando el crédito se paga por nómina y los demás de la cooperativa.
/// <list type="bullet">
/// <item>Si la solicitud no tiene pagaré vivo y hay plantilla de pagaré, crea el pagaré <b>físico</b> con el número de la solicitud; si ya lo tiene,
/// usa su número (un físico creado sin número se imprime con el de la solicitud). Con pagaré desmaterializado no imprime el pagaré ni la carta (se firman en el depósito).</item>
/// <item>Los campos propios que se digitan y son obligatorios deben llegar (<c>Lending.SignatureDocuments.FieldsMissing</c>); lo que el sistema no
/// pudo llenar sale con raya para llenar a mano y se informa.</item>
/// <item>El PDF se guarda como adjunto no borrable antes de la fila; volver a generar deja el juego anterior <c>Superseded</c>.</item>
/// </list>
/// Lo que se firma es el papel: la copia firmada se sube como siempre (custodia del pagaré, soportes de la solicitud), y el desembolso sigue exigiendo
/// el pagaré firmado. (nuevo)
/// </summary>
public sealed record GenerateSignatureDocumentsCommand(Guid ApplicationPublicId, IReadOnlyDictionary<string, string?>? Values)
    : IRequest<Result<GeneratedSignatureDocumentsDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class GenerateSignatureDocumentsCommandHandler(
    IApplicationDbContext db,
    ArmadoDeDatosParaFirma armado,
    IDocumentosParaFirmaRenderer renderizador,
    ISender sender,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IActorActual actorActual,
    IBlobStore store,
    IDateTimeService reloj,
    IAmbienteDeDespliegue? ambiente = null,
    IAttachmentCipher? cifrado = null) : IRequestHandler<GenerateSignatureDocumentsCommand, Result<GeneratedSignatureDocumentsDto>>
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public const string MarcaDePruebas = "AMBIENTE DE PRUEBAS";

    public async Task<Result<GeneratedSignatureDocumentsDto>> Handle(GenerateSignatureDocumentsCommand request, CancellationToken ct)
    {
        var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, request.ApplicationPublicId, seguir: false, ct);
        if (leida.IsFailure) return Falla(leida.Error);
        var s = leida.Value;
        var hoy = reloj.HoyLocal;
        if (s.Status != CreditApplicationStatus.Approved) return Falla(ErroresDeDocumentosParaFirma.NoAprobada(s.Status.ToString()));
        if (s.AprobacionVencida(hoy)) return Falla(new Error(ErroresDeDocumentosParaFirma.CodigoNoAprobada, SeleccionDePlantillas.PorQueNo(s, hoy)!));

        var (plantillas, pagare, _) = await SeleccionDePlantillas.LeerAsync(db, s, seguirPagare: true, ct);
        if (plantillas.Count == 0) return Falla(ErroresDeDocumentosParaFirma.SinPlantillas());

        // Los campos propios: que las plantillas estén bien y que lleguen los obligatorios que usan.
        var propios = await CamposPropios.LeerAsync(db, ct);
        foreach (var t in plantillas)
        {
            var errores = PlantillaDeFirma.Validar(t.Body, propios.Catalogo);
            if (errores.Count > 0) return Falla(ErroresDeDocumentosParaFirma.PlantillaInvalida(t.Name, errores));
        }

        var usados = plantillas.SelectMany(t => PlantillaDeFirma.CamposUsados(t.Body)).ToHashSet(StringComparer.Ordinal);
        var faltan = propios.Campos
            .Where(f => f.Source == SignatureFieldSource.Typed && f.IsRequired && usados.Contains(f.Code)
                && string.IsNullOrWhiteSpace(request.Values?.GetValueOrDefault(f.Code)) && string.IsNullOrWhiteSpace(f.DefaultValue))
            .Select(f => (f.Code, f.Label)).ToList();
        if (faltan.Count > 0) return Falla(ErroresDeDocumentosParaFirma.FaltanCampos(faltan));
        var valores = propios.Valores(request.Values);
        if (valores.IsFailure) return Falla(valores.Error);

        // El pagaré: el vivo, o uno físico nuevo con el número de la solicitud si hay plantilla de pagaré.
        if (pagare is null && plantillas.Any(t => t.Kind == SignatureDocumentKind.PromissoryNote))
        {
            pagare = new PromissoryNote
            {
                Kind = PromissoryNoteKind.Physical,
                ApplicationId = s.Id,
                IssuedOn = hoy,
                Status = PromissoryNoteStatus.Created,
                Number = s.Number,
            };
            db.PromissoryNotes.Add(pagare);
        }

        var armados = await armado.ArmarAsync(s, pagare?.Number ?? s.Number, valores.Value, ct);
        var vacios = new List<string>();
        var documentos = new List<DocumentoParaImprimir>();
        foreach (var t in plantillas)
        {
            var texto = PlantillaDeFirma.Armar(t.Body, armados.Datos);
            vacios.AddRange(texto.CamposVacios);
            documentos.Add(new DocumentoParaImprimir(t.Name, t.Kind, texto.Bloques,
                t.SignedByCoDebtors ? armados.Codeudores.Prepend(armados.Deudor).ToList() : [armados.Deudor], t.IncludeFingerprint));
        }

        var avisos = new List<string>(armados.Avisos);
        if (plantillas.Any(t => !t.IsVerified))
            avisos.Add("Hay plantillas sin validar por el abogado de la cooperativa: " + string.Join(", ", plantillas.Where(t => !t.IsVerified).Select(t => t.Name)) + ".");

        var (cooperativa, nit) = await MembreteDeCartera.CooperativaAsync(db, ct);
        var logotipo = await MembreteDeCartera.ImagenVigenteAsync(db, store, cifrado, AdjuntosDeModulo.LogotipoDeCartera, ct);
        var pdf = renderizador.Render(new PaqueteParaFirma(cooperativa, nit, logotipo?.Bytes, $"Solicitud {s.Number}", reloj.AhoraLocal,
            ambiente?.EsProduccion == true ? null : MarcaDePruebas, documentos));

        // El adjunto antes de la fila: si el almacén falla no queda un juego que nadie puede bajar.
        var juego = new SignatureDocumentSet
        {
            ApplicationId = s.Id,
            GeneratedAt = reloj.UtcNow,
            GeneratedByUserId = (await actorActual.ObtenerAsync(ct)).UserId,
            ContentSha256 = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant(),
            DocumentsJson = JsonSerializer.Serialize(SeleccionDePlantillas.Documentos(plantillas), Json),
            FieldsJson = JsonSerializer.Serialize(armados.Datos.Campos, Json),
        };
        var subida = await sender.Send(new UploadAttachmentCommand(AdjuntosDeModulo.DocumentosParaFirmaDeCartera, juego.PublicId,
            $"Documentos-para-firma-{s.Number ?? s.PublicId.ToString("N")}.pdf", "application/pdf", pdf), ct);
        if (subida.IsFailure) return Falla(subida.Error);
        juego.AttachmentPublicId = subida.Value;

        var anteriores = await db.SignatureDocumentSets.Where(j => j.ApplicationId == s.Id && !j.IsDeleted && j.Status == SignatureDocumentSetStatus.Active)
            .ToListAsync(ct);
        foreach (var anterior in anteriores)
        {
            anterior.Status = SignatureDocumentSetStatus.Superseded;
            anterior.SupersededAt = juego.GeneratedAt;
        }

        juego.PromissoryNotePublicId = pagare?.PublicId;
        db.SignatureDocumentSets.Add(juego);
        await db.SaveChangesAsync(ct);
        return Result.Success(new GeneratedSignatureDocumentsDto(SeleccionDePlantillas.Dto(juego),
            vacios.Distinct(StringComparer.Ordinal).Select(c => CamposDelSistema.Campos.FirstOrDefault(x => x.Codigo == c)?.Descripcion
                ?? propios.Campos.FirstOrDefault(f => f.Code == c)?.Label ?? c).ToList(), avisos));
    }

    private static Result<GeneratedSignatureDocumentsDto> Falla(Error e) => Result.Failure<GeneratedSignatureDocumentsDto>(e);
}
