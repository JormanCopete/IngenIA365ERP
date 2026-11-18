using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.SignatureDocuments;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SignatureDocuments;

// 2026-11-18 (pedido del dueño): la configuración de los documentos para firma —plantillas y campos propios— con Lending.SignatureTemplates.Manage.
// (nuevo)

public sealed record SignatureTemplateDto(
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

public sealed record SignatureFieldDto(
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

public sealed record SystemFieldDto(string Code, string Description, string Group, string Example);

public sealed record SystemListDto(string Code, string Description, IReadOnlyList<SystemFieldDto> Fields);

/// <summary>Todo lo de la configuración: las plantillas, los campos propios, los del sistema y si faltan plantillas base por cargar.</summary>
public sealed record SignatureConfigurationDto(
    IReadOnlyList<SignatureTemplateDto> Templates,
    IReadOnlyList<SignatureFieldDto> Fields,
    IReadOnlyList<SystemFieldDto> SystemFields,
    IReadOnlyList<SystemListDto> SystemLists,
    IReadOnlyList<string> MissingBaseTemplates);

public sealed record SignatureTemplateInput(
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

public sealed record SignatureFieldInput(
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

internal static class MapeoDeFirma
{
    public static SignatureTemplateDto Dto(SignatureDocumentTemplate t) => new(t.PublicId, t.Kind.ToString(), t.Code, t.Name, t.Body,
        SignatureDocumentTemplate.Lista(t.RepaymentChannels), SignatureDocumentTemplate.Lista(t.CreditLineCodes), t.IsActive, t.PrintOrder,
        t.IncludeFingerprint, t.SignedByCoDebtors, t.Version, t.IsVerified, t.VerifiedAt, t.VerificationNote);

    public static SignatureFieldDto Dto(SignatureDocumentField f) => new(f.PublicId, f.Code, f.Label, f.Kind.ToString(), f.Source.ToString(),
        f.ConstantValue, f.DefaultValue, f.IsRequired, f.HelpText, f.Order, f.IsActive);

    public static string? Recortar(string? texto, int largo)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        return t.Length <= largo ? t : t[..largo];
    }

    public static bool CodigoDePlantillaValido(string? codigo) =>
        codigo is { Length: >= 2 and <= SignatureDocumentTemplate.LargoDelCodigo }
        && codigo.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '_');
}

// ------------------------------------------------------------------------------------------------------------------ consulta --

/// <summary><c>GET /api/lending/signature-templates</c>: la configuración completa de los documentos para firma.</summary>
public sealed record GetSignatureConfigurationQuery : IRequest<Result<SignatureConfigurationDto>>;

public sealed class GetSignatureConfigurationQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetSignatureConfigurationQuery, Result<SignatureConfigurationDto>>
{
    public async Task<Result<SignatureConfigurationDto>> Handle(GetSignatureConfigurationQuery request, CancellationToken ct)
    {
        var plantillas = await db.SignatureDocumentTemplates.AsNoTracking().Where(t => !t.IsDeleted)
            .OrderBy(t => t.Kind).ThenBy(t => t.PrintOrder).ThenBy(t => t.Code).ToListAsync(ct);
        var campos = await db.SignatureDocumentFields.AsNoTracking().Where(f => !f.IsDeleted).OrderBy(f => f.Order).ThenBy(f => f.Code).ToListAsync(ct);
        var codigos = plantillas.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Result.Success(new SignatureConfigurationDto(
            plantillas.Select(MapeoDeFirma.Dto).ToList(),
            campos.Select(MapeoDeFirma.Dto).ToList(),
            CamposDelSistema.Campos.Select(c => new SystemFieldDto(c.Codigo, c.Descripcion, c.Grupo, c.Ejemplo)).ToList(),
            CamposDelSistema.Listas.Select(l => new SystemListDto(l.Codigo, l.Descripcion,
                l.Campos.Select(c => new SystemFieldDto(c.Codigo, c.Descripcion, c.Grupo, c.Ejemplo)).ToList())).ToList(),
            PlantillasBase.Plantillas.Where(p => !codigos.Contains(p.Codigo)).Select(p => p.Nombre).ToList()));
    }
}

// ---------------------------------------------------------------------------------------------------------------- plantillas --

/// <summary>
/// <c>POST /api/lending/signature-templates</c> (sin <see cref="PublicId"/>) o <c>PUT …/{id}</c>: crea o cambia una plantilla. El texto se valida
/// contra los campos del sistema y los propios (<c>Lending.SignatureTemplate.Invalid</c> con renglón y mensaje); cambiar el texto sube la versión y
/// quita la validación del abogado.
/// </summary>
public sealed record SaveSignatureTemplateCommand(Guid? PublicId, SignatureTemplateInput Input) : IRequest<Result<SignatureTemplateDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveSignatureTemplateCommandValidator : AbstractValidator<SaveSignatureTemplateCommand>
{
    public SaveSignatureTemplateCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.Kind).Must(k => Enum.TryParse<SignatureDocumentKind>(k, true, out _)).WithMessage("Elija la clase del documento.");
        RuleFor(x => x.Input.Code).Must(MapeoDeFirma.CodigoDePlantillaValido)
            .WithMessage($"El código va en mayúsculas, con números, guion o guion bajo, de 2 a {SignatureDocumentTemplate.LargoDelCodigo} caracteres.");
        RuleFor(x => x.Input.Name).NotEmpty().MaximumLength(SignatureDocumentTemplate.LargoDelNombre).WithMessage("Escriba el nombre del documento.");
        RuleFor(x => x.Input.Body).NotEmpty().MaximumLength(SignatureDocumentTemplate.LargoDelTexto).WithMessage("Escriba el texto de la plantilla.");
        RuleForEach(x => x.Input.RepaymentChannels).Must(c => Enum.TryParse<RepaymentChannel>(c, true, out _)).WithMessage("Un canal de pago no existe.");
        RuleFor(x => x.Input.PrintOrder).InclusiveBetween(0, 999);
    }
}

public sealed class SaveSignatureTemplateCommandHandler(IApplicationDbContext db)
    : IRequestHandler<SaveSignatureTemplateCommand, Result<SignatureTemplateDto>>
{
    public async Task<Result<SignatureTemplateDto>> Handle(SaveSignatureTemplateCommand request, CancellationToken ct)
    {
        var e = request.Input;
        var codigo = e.Code.Trim();
        var otra = await db.SignatureDocumentTemplates.AsNoTracking()
            .Where(t => !t.IsDeleted && t.Code == codigo && (request.PublicId == null || t.PublicId != request.PublicId))
            .Select(t => t.Name).FirstOrDefaultAsync(ct);
        if (otra is not null) return Result.Failure<SignatureTemplateDto>(ErroresDeDocumentosParaFirma.CodigoDuplicado(codigo, otra));

        var propios = await CamposPropios.LeerAsync(db, ct);
        var errores = PlantillaDeFirma.Validar(e.Body, propios.Catalogo);
        if (errores.Count > 0) return Result.Failure<SignatureTemplateDto>(ErroresDeDocumentosParaFirma.PlantillaInvalida(e.Name, errores));

        SignatureDocumentTemplate plantilla;
        if (request.PublicId is { } id)
        {
            var existente = await db.SignatureDocumentTemplates.FirstOrDefaultAsync(t => t.PublicId == id && !t.IsDeleted, ct);
            if (existente is null) return Result.Failure<SignatureTemplateDto>(ErroresDeDocumentosParaFirma.PlantillaInexistente());
            plantilla = existente;
            plantilla.CambiarTexto(e.Body);
        }
        else
        {
            plantilla = new SignatureDocumentTemplate { Body = e.Body };
            db.SignatureDocumentTemplates.Add(plantilla);
        }

        plantilla.Kind = Enum.Parse<SignatureDocumentKind>(e.Kind, true);
        plantilla.Code = codigo;
        plantilla.Name = e.Name.Trim();
        plantilla.RepaymentChannels = SignatureDocumentTemplate.Texto(e.RepaymentChannels?.Select(c => Enum.Parse<RepaymentChannel>(c, true).ToString()));
        plantilla.CreditLineCodes = SignatureDocumentTemplate.Texto(e.CreditLineCodes?.Select(c => c.ToUpperInvariant()));
        plantilla.IsActive = e.IsActive;
        plantilla.PrintOrder = e.PrintOrder;
        plantilla.IncludeFingerprint = e.IncludeFingerprint;
        plantilla.SignedByCoDebtors = e.SignedByCoDebtors;
        await db.SaveChangesAsync(ct);
        return Result.Success(MapeoDeFirma.Dto(plantilla));
    }
}

/// <summary><c>DELETE /api/lending/signature-templates/{id}</c>: la baja lógica de una plantilla. Lo ya generado conserva su PDF.</summary>
public sealed record DeleteSignatureTemplateCommand(Guid PublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeleteSignatureTemplateCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<DeleteSignatureTemplateCommand, Result>
{
    public async Task<Result> Handle(DeleteSignatureTemplateCommand request, CancellationToken ct)
    {
        var plantilla = await db.SignatureDocumentTemplates.FirstOrDefaultAsync(t => t.PublicId == request.PublicId && !t.IsDeleted, ct);
        if (plantilla is null) return Result.Failure(ErroresDeDocumentosParaFirma.PlantillaInexistente());
        plantilla.IsDeleted = true;
        plantilla.DeletedAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary><c>POST /api/lending/signature-templates/{id}/verify</c>: deja constancia de que el abogado validó la versión vigente del texto.</summary>
public sealed record VerifySignatureTemplateCommand(Guid PublicId, string? Note) : IRequest<Result<SignatureTemplateDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class VerifySignatureTemplateCommandHandler(IApplicationDbContext db, IActorActual actor, IDateTimeService reloj)
    : IRequestHandler<VerifySignatureTemplateCommand, Result<SignatureTemplateDto>>
{
    public async Task<Result<SignatureTemplateDto>> Handle(VerifySignatureTemplateCommand request, CancellationToken ct)
    {
        var plantilla = await db.SignatureDocumentTemplates.FirstOrDefaultAsync(t => t.PublicId == request.PublicId && !t.IsDeleted, ct);
        if (plantilla is null) return Result.Failure<SignatureTemplateDto>(ErroresDeDocumentosParaFirma.PlantillaInexistente());
        plantilla.Validar((await actor.ObtenerAsync(ct)).UserId, reloj.UtcNow, MapeoDeFirma.Recortar(request.Note, SignatureDocumentTemplate.LargoDeLaNota));
        await db.SaveChangesAsync(ct);
        return Result.Success(MapeoDeFirma.Dto(plantilla));
    }
}

/// <summary>
/// <c>POST /api/lending/signature-templates/load-base</c>: carga las plantillas base (pagaré, carta de instrucciones, libranza) y sus campos propios
/// que la cooperativa todavía no tiene por código. No pisa nada; entran sin validar.
/// </summary>
public sealed record LoadBaseSignatureTemplatesCommand : IRequest<Result<SignatureConfigurationDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class LoadBaseSignatureTemplatesCommandHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<LoadBaseSignatureTemplatesCommand, Result<SignatureConfigurationDto>>
{
    public async Task<Result<SignatureConfigurationDto>> Handle(LoadBaseSignatureTemplatesCommand request, CancellationToken ct)
    {
        var plantillas = (await db.SignatureDocumentTemplates.AsNoTracking().Where(t => !t.IsDeleted).Select(t => t.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var campos = (await db.SignatureDocumentFields.AsNoTracking().Where(f => !f.IsDeleted).Select(f => f.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var c in PlantillasBase.Campos.Where(c => !campos.Contains(c.Codigo)))
            db.SignatureDocumentFields.Add(new SignatureDocumentField
            {
                Code = c.Codigo, Label = c.Nombre, Kind = c.Tipo, Source = c.Origen, ConstantValue = c.Valor, HelpText = c.Ayuda, Order = c.Orden,
            });
        foreach (var p in PlantillasBase.Plantillas.Where(p => !plantillas.Contains(p.Codigo)))
            db.SignatureDocumentTemplates.Add(new SignatureDocumentTemplate
            {
                Kind = p.Clase, Code = p.Codigo, Name = p.Nombre, Body = p.Texto, RepaymentChannels = SignatureDocumentTemplate.Texto(p.Canales.Select(c => c.ToString())),
                PrintOrder = p.Orden, IncludeFingerprint = p.Huella, SignedByCoDebtors = p.FirmanCodeudores,
            });
        await db.SaveChangesAsync(ct);
        return await sender.Send(new GetSignatureConfigurationQuery(), ct);
    }
}

// --------------------------------------------------------------------------------------------------------------- campos propios --

/// <summary><c>POST /api/lending/signature-fields</c> o <c>PUT …/{id}</c>: crea o cambia un campo propio. El código no cambia después de crearlo.</summary>
public sealed record SaveSignatureFieldCommand(Guid? PublicId, SignatureFieldInput Input) : IRequest<Result<SignatureFieldDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class SaveSignatureFieldCommandValidator : AbstractValidator<SaveSignatureFieldCommand>
{
    public SaveSignatureFieldCommandValidator()
    {
        RuleFor(x => x.Input).NotNull();
        RuleFor(x => x.Input.Code).Must(CamposDelSistema.CodigoValido)
            .WithMessage("El código empieza con una letra y lleva mayúsculas, números o guion bajo, de 2 a 40 caracteres (por ejemplo CIUDAD_PAGO).");
        RuleFor(x => x.Input.Label).NotEmpty().MaximumLength(SignatureDocumentField.LargoDelNombre).WithMessage("Escriba el nombre del campo.");
        RuleFor(x => x.Input.Kind).Must(k => Enum.TryParse<SignatureFieldKind>(k, true, out _)).WithMessage("Elija el tipo del campo.");
        RuleFor(x => x.Input.Source).Must(k => Enum.TryParse<SignatureFieldSource>(k, true, out _)).WithMessage("Elija de dónde sale el valor.");
        RuleFor(x => x.Input.ConstantValue).MaximumLength(SignatureDocumentField.LargoDelValor);
        RuleFor(x => x.Input.DefaultValue).MaximumLength(SignatureDocumentField.LargoDelValor);
        RuleFor(x => x.Input.HelpText).MaximumLength(SignatureDocumentField.LargoDeLaAyuda);
        RuleFor(x => x.Input.Order).InclusiveBetween(0, 999);
    }
}

public sealed class SaveSignatureFieldCommandHandler(IApplicationDbContext db) : IRequestHandler<SaveSignatureFieldCommand, Result<SignatureFieldDto>>
{
    public async Task<Result<SignatureFieldDto>> Handle(SaveSignatureFieldCommand request, CancellationToken ct)
    {
        var e = request.Input;
        var codigo = e.Code.Trim();
        if (CamposDelSistema.Reservados.Contains(codigo) || codigo.EndsWith(CamposDelSistema.SufijoLetras, StringComparison.Ordinal))
            return Result.Failure<SignatureFieldDto>(ErroresDeDocumentosParaFirma.CampoReservado(codigo));
        var otro = await db.SignatureDocumentFields.AsNoTracking()
            .Where(f => !f.IsDeleted && f.Code == codigo && (request.PublicId == null || f.PublicId != request.PublicId))
            .Select(f => f.Label).FirstOrDefaultAsync(ct);
        if (otro is not null) return Result.Failure<SignatureFieldDto>(ErroresDeDocumentosParaFirma.CampoDuplicado(codigo, otro));

        SignatureDocumentField campo;
        if (request.PublicId is { } id)
        {
            var existente = await db.SignatureDocumentFields.FirstOrDefaultAsync(f => f.PublicId == id && !f.IsDeleted, ct);
            if (existente is null) return Result.Failure<SignatureFieldDto>(ErroresDeDocumentosParaFirma.CampoInexistente());
            if (existente.Code != codigo) return Result.Failure<SignatureFieldDto>(ErroresDeDocumentosParaFirma.CodigoInmutable());
            campo = existente;
        }
        else
        {
            campo = new SignatureDocumentField { Code = codigo };
            db.SignatureDocumentFields.Add(campo);
        }

        campo.Label = e.Label.Trim();
        campo.Kind = Enum.Parse<SignatureFieldKind>(e.Kind, true);
        campo.Source = Enum.Parse<SignatureFieldSource>(e.Source, true);
        campo.ConstantValue = MapeoDeFirma.Recortar(e.ConstantValue, SignatureDocumentField.LargoDelValor);
        campo.DefaultValue = MapeoDeFirma.Recortar(e.DefaultValue, SignatureDocumentField.LargoDelValor);
        campo.IsRequired = campo.Source == SignatureFieldSource.Typed && e.IsRequired;
        campo.HelpText = MapeoDeFirma.Recortar(e.HelpText, SignatureDocumentField.LargoDeLaAyuda);
        campo.Order = e.Order;
        campo.IsActive = e.IsActive;

        // Un valor que no es del tipo del campo no se guarda.
        var prueba = new CamposPropios([campo]).Valores(null);
        if (prueba.IsFailure) return Result.Failure<SignatureFieldDto>(prueba.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(MapeoDeFirma.Dto(campo));
    }
}

/// <summary><c>DELETE /api/lending/signature-fields/{id}</c>: la baja de un campo propio que ninguna plantilla viva usa.</summary>
public sealed record DeleteSignatureFieldCommand(Guid PublicId) : IRequest<Result>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class DeleteSignatureFieldCommandHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<DeleteSignatureFieldCommand, Result>
{
    public async Task<Result> Handle(DeleteSignatureFieldCommand request, CancellationToken ct)
    {
        var campo = await db.SignatureDocumentFields.FirstOrDefaultAsync(f => f.PublicId == request.PublicId && !f.IsDeleted, ct);
        if (campo is null) return Result.Failure(ErroresDeDocumentosParaFirma.CampoInexistente());
        var plantillas = await db.SignatureDocumentTemplates.AsNoTracking().Where(t => !t.IsDeleted).Select(t => new { t.Name, t.Body }).ToListAsync(ct);
        var usan = plantillas.Where(p =>
        {
            var usados = PlantillaDeFirma.CamposUsados(p.Body);
            return usados.Contains(campo.Code) || usados.Contains(campo.Code + CamposDelSistema.SufijoLetras);
        }).Select(p => p.Name).ToList();
        if (usan.Count > 0) return Result.Failure(ErroresDeDocumentosParaFirma.CampoEnUso(campo.Code, usan));
        campo.IsDeleted = true;
        campo.DeletedAt = reloj.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ---------------------------------------------------------------------------------------------------------------- vista previa --

/// <summary>
/// <c>POST /api/lending/signature-templates/preview</c>: el PDF de una plantilla (guardada o no) con datos de ejemplo o, con
/// <see cref="ApplicationPublicId"/>, con los de esa solicitud (en el alcance). No guarda nada y lleva la marca «VISTA PREVIA».
/// </summary>
public sealed record PreviewSignatureTemplateQuery(
    string Name,
    string Kind,
    string Body,
    bool IncludeFingerprint,
    bool SignedByCoDebtors,
    Guid? ApplicationPublicId,
    IReadOnlyDictionary<string, string?>? Values) : IRequest<Result<byte[]>>;

public sealed class PreviewSignatureTemplateQueryHandler(
    IApplicationDbContext db,
    ArmadoDeDatosParaFirma armado,
    IDocumentosParaFirmaRenderer renderizador,
    IAlcanceDeCartera alcance,
    LendingAuditEmitter auditoria,
    IBlobStore store,
    IDateTimeService reloj,
    IAttachmentCipher? cifrado = null) : IRequestHandler<PreviewSignatureTemplateQuery, Result<byte[]>>
{
    public const string MarcaDeVistaPrevia = "VISTA PREVIA";

    public async Task<Result<byte[]>> Handle(PreviewSignatureTemplateQuery request, CancellationToken ct)
    {
        var propios = await CamposPropios.LeerAsync(db, ct);
        var errores = PlantillaDeFirma.Validar(request.Body, propios.Catalogo);
        if (errores.Count > 0) return Result.Failure<byte[]>(ErroresDeDocumentosParaFirma.PlantillaInvalida(request.Name, errores));
        var valores = propios.Valores(request.Values);
        if (valores.IsFailure) return Result.Failure<byte[]>(valores.Error);

        DatosDePlantilla datos;
        IReadOnlyList<FirmanteParaImprimir> firmantes;
        string referencia;
        if (request.ApplicationPublicId is { } solicitudId)
        {
            var leida = await SolicitudEnAlcance.LeerAsync(db, alcance, auditoria, solicitudId, seguir: false, ct);
            if (leida.IsFailure) return Result.Failure<byte[]>(leida.Error);
            var pagare = await db.PromissoryNotes.AsNoTracking()
                .Where(p => p.ApplicationId == leida.Value.Id && p.LoanId == null && !p.IsDeleted && p.Status != PromissoryNoteStatus.Voided)
                .Select(p => p.Number).FirstOrDefaultAsync(ct);
            var armados = await armado.ArmarAsync(leida.Value, pagare ?? leida.Value.Number, valores.Value, ct);
            datos = armados.Datos;
            firmantes = request.SignedByCoDebtors ? armados.Codeudores.Prepend(armados.Deudor).ToList() : [armados.Deudor];
            referencia = $"Vista previa con la solicitud {leida.Value.Number}";
        }
        else
        {
            var ejemplo = CamposDelSistema.Ejemplo();
            var campos = new Dictionary<string, string?>(ejemplo.Campos, StringComparer.Ordinal);
            foreach (var (codigo, valor) in valores.Value) campos[codigo] = valor;
            datos = ejemplo with { Campos = campos };
            var ejemplos = CamposDelSistema.Listas.Single(l => l.Codigo == CamposDelSistema.Firmantes).Ejemplo
                .Select(f => new FirmanteParaImprimir(f[CamposDelSistema.FirmanteNombre]!, f[CamposDelSistema.FirmanteTipoDocumento]!,
                    f[CamposDelSistema.FirmanteDocumento]!, f[CamposDelSistema.FirmanteCalidad]!)).ToList();
            firmantes = request.SignedByCoDebtors ? ejemplos : [ejemplos[0]];
            referencia = "Vista previa con datos de ejemplo";
        }

        var (cooperativa, nit) = await MembreteDeCartera.CooperativaAsync(db, ct);
        var logotipo = await MembreteDeCartera.ImagenVigenteAsync(db, store, cifrado, AdjuntosDeModulo.LogotipoDeCartera, ct);
        var clase = Enum.TryParse<SignatureDocumentKind>(request.Kind, true, out var k) ? k : SignatureDocumentKind.Other;
        var documento = new DocumentoParaImprimir(request.Name, clase, PlantillaDeFirma.Armar(request.Body, datos).Bloques, firmantes, request.IncludeFingerprint);
        return Result.Success(renderizador.Render(new PaqueteParaFirma(cooperativa, nit, logotipo?.Bytes, referencia, reloj.AhoraLocal, MarcaDeVistaPrevia,
            [documento])));
    }
}
