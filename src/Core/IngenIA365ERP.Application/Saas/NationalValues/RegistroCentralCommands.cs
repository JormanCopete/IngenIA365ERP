using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Saas.NationalValues;

// ------------------------------------------------------------------------------------------------ registrar valor --

/// <summary>
/// El maestro registra un valor nacional en la central (feature 013, T37, T38; contracts/api.md §21.5; data-model parte 3 §3.1):
/// una fila nueva de <c>ADM_NationalValues</c> con su <c>Sequence</c> de la numeración común. Lo mismo (código, clave, vigencia
/// desde) que una fila vigente responde <c>NationalValues.Value.AlreadyRegistered</c>: va como corrección.
///
/// <para>No implementa <c>IOperacionIdempotente</c>: la tabla de claves de operación vive en la base de cada cooperativa y esta
/// ruta corre sin cooperativa. La ruta exige igual la <c>Idempotency-Key</c> y el duplicado lo frena la regla de arriba. (nuevo)</para>
/// </summary>
public sealed record RegisterNationalValueCommand(
    string Code,
    string? Key,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateOnly? ReferenceDate,
    decimal? Value,
    string? Unit,
    string Source,
    string? Label = null) : IRequest<Result<Guid>>
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterNationalValueCommandValidator : AbstractValidator<RegisterNationalValueCommand>
{
    public RegisterNationalValueCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código es obligatorio.").MaximumLength(30);
        RuleFor(x => x.Key).MaximumLength(60);
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("La vigencia desde es obligatoria.");
        RuleFor(x => x.Source).NotEmpty().WithMessage("Diga la norma, resolución o certificación de donde sale el valor.").MaximumLength(300);
        RuleFor(x => x.Label).MaximumLength(200);
        RuleFor(x => x.Unit).MaximumLength(12);
    }
}

public sealed class RegisterNationalValueCommandHandler(
    IAdminDbContext db,
    ICurrentCentralUserContext usuario,
    IDateTimeService reloj,
    IAuditAppendOnlyWriter? auditoria = null,
    ILogger<RegisterNationalValueCommandHandler>? logger = null)
    : IRequestHandler<RegisterNationalValueCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterNationalValueCommand request, CancellationToken ct)
    {
        var maestro = MaestroQueRegistra.De(usuario);
        if (maestro.IsFailure) return Result.Failure<Guid>(maestro.Error);

        var resultado = await NumeracionCentral.ConReintentoAsync(db, async () =>
        {
            var validado = await ReglasDelRegistroCentral.ValidarAsync(db, request.Code, request.Key, request.ValidFrom, request.ValidTo,
                request.Value, request.Unit, ct);
            if (validado.IsFailure) return Result.Failure<PlatformNationalValue>(validado.Error);
            var v = validado.Value;

            if (await ReglasDelRegistroCentral.VigenteConLaMismaLlaveAsync(db, v.Codigo, v.Clave, v.ValidFrom, ct) is not null)
                return Result.Failure<PlatformNationalValue>(ErroresDeValoresNacionales.YaRegistrado(v.Codigo, v.Clave, v.ValidFrom));

            var fila = Nueva(v, request.ReferenceDate, request.Label, request.Source, reason: null, supersedes: null,
                await NumeracionCentral.TomarAsync(db, ct), reloj.UtcNow, maestro.Value);
            db.PlatformNationalValues.Add(fila);
            await db.SaveChangesAsync(ct);
            return Result.Success(fila);
        }, logger, ct);
        if (resultado.IsFailure) return Result.Failure<Guid>(resultado.Error);

        var f = resultado.Value;
        await AuditoriaDelRegistroCentral.RegistrarAsync(auditoria, maestro.Value, AuditoriaDelRegistroCentral.ValorRegistrado,
            nameof(PlatformNationalValue), f.PublicId,
            new { f.Code, f.Key, f.ValidFrom, f.ValidTo, f.Value, f.Unit, f.Source, f.Sequence }, reloj.UtcNow, logger, ct);
        return Result.Success(f.PublicId);
    }

    /// <summary>La fila nueva de la central (alta, corrección, plantilla y semilla).</summary>
    public static PlatformNationalValue Nueva(
        ValorValidado v, DateOnly? referencia, string? etiqueta, string fuente, string? reason, Guid? supersedes,
        long sequence, DateTime ahora, MaestroQueRegistra maestro, Guid? publicId = null)
    {
        var fila = new PlatformNationalValue
        {
            Code = v.Codigo,
            Key = v.Clave,
            ValidFrom = v.ValidFrom,
            ValidTo = v.ValidTo,
            ReferenceDate = referencia,
            Value = v.Valor,
            Unit = v.Unidad,
            Label = string.IsNullOrWhiteSpace(etiqueta) ? null : etiqueta.Trim(),
            Source = fuente.Trim(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            SupersedesPublicId = supersedes,
            Sequence = sequence,
            RegisteredAt = ahora,
            RegisteredBy = maestro.Nombre,
            RegisteredByCentralUserId = maestro.CentralUserId,
            CreatedAt = ahora,
            CreatedBy = maestro.Nombre,
        };
        if (publicId is { } id && id != Guid.Empty) fila.PublicId = id;
        return fila;
    }
}

// ------------------------------------------------------------------------------------------------ corregir valor --

/// <summary>
/// Corrección de un valor ya registrado (feature 013, T38; contracts/api.md §21.5): fila nueva con <c>SupersedesPublicId</c>,
/// nunca reescribiendo la anterior. Exige motivo. Una fila ya reemplazada no se corrige otra vez: se corrige la corrección
/// (<c>UK_ADM_NationalValues_Supersedes</c>), y así la corrección de una corrección reemplaza a la corrección. En cada
/// cooperativa, la réplica dispara lo que dependa de ella (Cartera encola la corrida <c>NationalValueCorrection</c>, T28). (nuevo)
/// </summary>
public sealed record CorrectNationalValueCommand(
    Guid NationalValuePublicId,
    decimal? Value,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    string Source,
    string Reason,
    DateOnly? ReferenceDate = null,
    string? Label = null) : IRequest<Result<Guid>>
{
    public Guid OperationKey { get; init; }
}

public sealed class CorrectNationalValueCommandValidator : AbstractValidator<CorrectNationalValueCommand>
{
    public CorrectNationalValueCommandValidator()
    {
        RuleFor(x => x.NationalValuePublicId).NotEmpty();
        RuleFor(x => x.Source).NotEmpty().WithMessage("Diga la norma o certificación que sostiene la corrección.").MaximumLength(300);
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Una corrección lleva su motivo.").MaximumLength(500);
        RuleFor(x => x.Label).MaximumLength(200);
    }
}

public sealed class CorrectNationalValueCommandHandler(
    IAdminDbContext db,
    ICurrentCentralUserContext usuario,
    IDateTimeService reloj,
    IAuditAppendOnlyWriter? auditoria = null,
    ILogger<CorrectNationalValueCommandHandler>? logger = null)
    : IRequestHandler<CorrectNationalValueCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CorrectNationalValueCommand request, CancellationToken ct)
    {
        var maestro = MaestroQueRegistra.De(usuario);
        if (maestro.IsFailure) return Result.Failure<Guid>(maestro.Error);

        var resultado = await NumeracionCentral.ConReintentoAsync(db, async () =>
        {
            var anterior = await db.PlatformNationalValues.AsNoTracking().FirstOrDefaultAsync(v => v.PublicId == request.NationalValuePublicId, ct);
            if (anterior is null) return Result.Failure<PlatformNationalValue>(ErroresDeValoresNacionales.NoEncontrado);
            if (await ReglasDelRegistroCentral.ReemplazadaPorAsync(db, anterior.PublicId, ct) is { } por)
                return Result.Failure<PlatformNationalValue>(ErroresDeValoresNacionales.YaCorregido(por));

            var desde = request.ValidFrom ?? anterior.ValidFrom;
            var hasta = request.ValidTo ?? (request.ValidFrom is null ? anterior.ValidTo : null);
            var validado = await ReglasDelRegistroCentral.ValidarAsync(db, anterior.Code, anterior.Key, desde, hasta,
                request.Value, anterior.Unit, ct);
            if (validado.IsFailure) return Result.Failure<PlatformNationalValue>(validado.Error);

            var fila = RegisterNationalValueCommandHandler.Nueva(validado.Value, request.ReferenceDate ?? anterior.ReferenceDate,
                request.Label ?? anterior.Label, request.Source, request.Reason, anterior.PublicId,
                await NumeracionCentral.TomarAsync(db, ct), reloj.UtcNow, maestro.Value);
            db.PlatformNationalValues.Add(fila);
            await db.SaveChangesAsync(ct);
            return Result.Success(fila);
        }, logger, ct);
        if (resultado.IsFailure) return Result.Failure<Guid>(resultado.Error);

        var f = resultado.Value;
        await AuditoriaDelRegistroCentral.RegistrarAsync(auditoria, maestro.Value, AuditoriaDelRegistroCentral.ValorCorregido,
            nameof(PlatformNationalValue), f.PublicId,
            new { f.Code, f.Key, f.ValidFrom, f.ValidTo, f.Value, f.Reason, f.SupersedesPublicId, f.Sequence }, reloj.UtcNow, logger, ct);
        return Result.Success(f.PublicId);
    }
}

// ------------------------------------------------------------------------------------------- registrar documento --

/// <summary>
/// El maestro registra un documento normativo (feature 013, T37, T45; contracts/api.md §21.5; data-model parte 3 §3.2): el
/// contenido pasa por el <see cref="IValidadorDeDocumentoNormativo"/> de su clase (inválido → <c>NationalValues.Document.Invalid</c>
/// con <c>data.errors[] { path, message }</c>), se guarda en forma canónica con su SHA-256 recalculado y toma su <c>Sequence</c> de
/// la numeración común. <c>SarcAnexo2</c> no se admite antes de K6a (<c>NationalValues.DocumentKindNotAvailable</c>), ni una clase
/// cuyo validador no se registró (los de Cartera llegan con sus fases). Un código ya registrado y vigente no se repite: una
/// versión nueva lleva otro código, y una corrección dice a quién reemplaza y por qué. (nuevo)
/// </summary>
public sealed record RegisterRegulatoryDocumentCommand(
    string Kind,
    string Code,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    short SchemaVersion,
    string Content,
    bool Verified,
    string Source,
    Guid? SupersedesPublicId = null,
    string? Reason = null) : IRequest<Result<Guid>>
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterRegulatoryDocumentCommandValidator : AbstractValidator<RegisterRegulatoryDocumentCommand>
{
    public RegisterRegulatoryDocumentCommandValidator()
    {
        RuleFor(x => x.Kind).NotEmpty().WithMessage("La clase del documento es obligatoria.")
            .Must(k => Enum.TryParse<RegulatoryDocumentKind>(k, ignoreCase: false, out var c) && Enum.IsDefined(c) && !int.TryParse(k, out _))
            .WithMessage("La clase del documento no existe (CatalogoDeModalidades, TiposDeOrganizacion, SarcAnexo1, SarcAnexo2, ReglaDePerdidaEsperada).");
        RuleFor(x => x.Code).NotEmpty().WithMessage("El código de la versión es obligatorio.").MaximumLength(60);
        RuleFor(x => x.ValidFrom).NotEqual(default(DateOnly)).WithMessage("La vigencia desde es obligatoria.");
        RuleFor(x => x.ValidTo).GreaterThanOrEqualTo(x => x.ValidFrom).When(x => x.ValidTo is not null)
            .WithMessage("La vigencia termina antes de empezar.");
        RuleFor(x => x.SchemaVersion).GreaterThan((short)0).WithMessage("La versión de esquema es obligatoria.");
        RuleFor(x => x.Content).NotEmpty().WithMessage("El contenido del documento es obligatorio.");
        RuleFor(x => x.Source).NotEmpty().WithMessage("Diga la circular, anexo y fecha de donde sale.").MaximumLength(300);
        RuleFor(x => x.Reason).NotEmpty().When(x => x.SupersedesPublicId is not null).WithMessage("Una corrección lleva su motivo.");
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class RegisterRegulatoryDocumentCommandHandler(
    IAdminDbContext db,
    ICurrentCentralUserContext usuario,
    IDateTimeService reloj,
    IEnumerable<IValidadorDeDocumentoNormativo> validadores,
    IAuditAppendOnlyWriter? auditoria = null,
    ILogger<RegisterRegulatoryDocumentCommandHandler>? logger = null)
    : IRequestHandler<RegisterRegulatoryDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(RegisterRegulatoryDocumentCommand request, CancellationToken ct)
    {
        var maestro = MaestroQueRegistra.De(usuario);
        if (maestro.IsFailure) return Result.Failure<Guid>(maestro.Error);

        var resultado = await NumeracionCentral.ConReintentoAsync(db,
            () => RegistrarAsync(db, validadores, request, maestro.Value, reloj.UtcNow, ct), logger, ct);
        if (resultado.IsFailure) return Result.Failure<Guid>(resultado.Error);

        var f = resultado.Value;
        await AuditoriaDelRegistroCentral.RegistrarAsync(auditoria, maestro.Value, AuditoriaDelRegistroCentral.DocumentoRegistrado,
            nameof(PlatformRegulatoryDocument), f.PublicId,
            new { f.Kind, f.Code, f.ValidFrom, f.SchemaVersion, f.Verified, f.ContentSha256, f.Sequence, f.SupersedesPublicId }, reloj.UtcNow, logger, ct);
        return Result.Success(f.PublicId);
    }

    /// <summary>Valida, numera, agrega y guarda (el alta y la semilla de la central).</summary>
    public static async Task<Result<PlatformRegulatoryDocument>> RegistrarAsync(
        IAdminDbContext db, IEnumerable<IValidadorDeDocumentoNormativo> validadores, RegisterRegulatoryDocumentCommand request,
        MaestroQueRegistra maestro, DateTime ahora, CancellationToken ct, Guid? publicId = null, bool guardar = true)
    {
        var preparado = await PrepararAsync(db, validadores, request, ct);
        if (preparado.IsFailure) return Result.Failure<PlatformRegulatoryDocument>(preparado.Error);
        var (clase, canonico) = preparado.Value;

        var fila = new PlatformRegulatoryDocument
        {
            Kind = clase.ToString(),
            Code = request.Code.Trim(),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            Content = canonico,
            ContentSha256 = ContenidoCanonico.Sha256(canonico),
            SchemaVersion = request.SchemaVersion,
            Verified = request.Verified,
            Source = request.Source.Trim(),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            SupersedesPublicId = request.SupersedesPublicId,
            Sequence = await NumeracionCentral.TomarAsync(db, ct),
            RegisteredAt = ahora,
            RegisteredBy = maestro.Nombre,
            RegisteredByCentralUserId = maestro.CentralUserId,
            CreatedAt = ahora,
            CreatedBy = maestro.Nombre,
        };
        if (publicId is { } id && id != Guid.Empty) fila.PublicId = id;
        db.PlatformRegulatoryDocuments.Add(fila);
        if (guardar) await db.SaveChangesAsync(ct);
        return Result.Success(fila);
    }

    /// <summary>Las reglas del documento sin escribir: clase disponible, validador, contenido válido, código y reemplazo.</summary>
    public static async Task<Result<(RegulatoryDocumentKind Clase, string Canonico)>> PrepararAsync(
        IAdminDbContext db, IEnumerable<IValidadorDeDocumentoNormativo> validadores, RegisterRegulatoryDocumentCommand request, CancellationToken ct)
    {
        if (!Enum.TryParse<RegulatoryDocumentKind>(request.Kind, ignoreCase: false, out var clase) || !Enum.IsDefined(clase))
            return Result.Failure<(RegulatoryDocumentKind, string)>(ErroresDeValoresNacionales.ClaseNoDisponible(request.Kind, "no es una clase de documento normativo"));
        if (clase == RegulatoryDocumentKind.SarcAnexo2)
            return Result.Failure<(RegulatoryDocumentKind, string)>(ErroresDeValoresNacionales.ClaseNoDisponible(request.Kind, "el Anexo 2 llega con la entrega K6a"));
        var validador = validadores.FirstOrDefault(v => v.Clase == clase);
        if (validador is null)
            return Result.Failure<(RegulatoryDocumentKind, string)>(ErroresDeValoresNacionales.ClaseNoDisponible(request.Kind, "su validador todavía no está registrado"));

        var errores = validador.Validar(request.Content, request.SchemaVersion);
        if (errores.Count > 0) return Result.Failure<(RegulatoryDocumentKind, string)>(ErroresDeValoresNacionales.DocumentoInvalido(errores));
        var canonico = ContenidoCanonico.Canonizar(request.Content);

        var nombre = clase.ToString();
        var codigo = request.Code.Trim();
        if (request.SupersedesPublicId is { } reemplaza)
        {
            var anterior = await db.PlatformRegulatoryDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.PublicId == reemplaza, ct);
            if (anterior is null || anterior.Kind != nombre)
                return Result.Failure<(RegulatoryDocumentKind, string)>(ErroresDeValoresNacionales.DocumentoNoEncontrado);
            var ya = await db.PlatformRegulatoryDocuments.AsNoTracking().Where(d => d.SupersedesPublicId == reemplaza).Select(d => (Guid?)d.PublicId).FirstOrDefaultAsync(ct);
            if (ya is { } por) return Result.Failure<(RegulatoryDocumentKind, string)>(ErroresDeValoresNacionales.YaCorregido(por));
        }
        else
        {
            var mismos = await db.PlatformRegulatoryDocuments.AsNoTracking()
                .Where(d => d.Kind == nombre && d.Code == codigo).Select(d => d.PublicId).ToListAsync(ct);
            var vigentes = mismos.Count == 0 ? [] : mismos.Except(await db.PlatformRegulatoryDocuments.AsNoTracking()
                .Where(d => d.SupersedesPublicId != null && mismos.Contains(d.SupersedesPublicId.Value))
                .Select(d => d.SupersedesPublicId!.Value).ToListAsync(ct)).ToList();
            if (vigentes.Count > 0 || db.PlatformRegulatoryDocuments.Local.Any(d => d.Kind == nombre && d.Code == codigo))
                return Result.Failure<(RegulatoryDocumentKind, string)>(ErroresDeValoresNacionales.DocumentoYaRegistrado(nombre, codigo));
        }
        return Result.Success((clase, canonico));
    }
}
