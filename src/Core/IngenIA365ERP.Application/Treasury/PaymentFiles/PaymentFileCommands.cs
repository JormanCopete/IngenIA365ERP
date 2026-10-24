using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Batches;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Treasury.PaymentFiles;

// Feature 015, US10 (T0144; R-20; FR-112, FR-213, FR-214; contracts/api.md §10; data-model §10.5): el archivo de pago de un lote aprobado —inmutable, con su
// SHA-256 por archivo y por línea, guardado como adjunto no borrable (TreasuryPaymentFile) ANTES de su fila—, su descarga por otra persona que la que lo preparó o
// aprobó (auditada), marcarlo enviado con quién lo cargó y quién lo autorizó en el banco (no emite hecho ni resultado: «girada» llega con la respuesta) y anularlo
// si no se envió (sus órdenes vuelven a la bandeja). La forma «portal del banco» no genera archivo: se marca enviada por el portal. (nuevo)

/// <summary>Una línea del archivo con lo que escribe el motor y lo que se coteja contra la respuesta. (nuevo)</summary>
public sealed record LineaDelArchivo(PaymentBatchItem Item, PaymentOrder Orden, BankFileLineValues Valores);

/// <summary>Las líneas y el contexto del archivo de un lote, para la vista previa y la generación. (nuevo)</summary>
public static class LineasDelArchivo
{
    public static async Task<IReadOnlyList<LineaDelArchivo>> CargarAsync(IApplicationDbContext db, PaymentBatch lote, CancellationToken ct)
    {
        var filas = await (from i in db.PaymentBatchItems.AsNoTracking()
                           join o in db.PaymentOrders.AsNoTracking() on i.PaymentOrderId equals o.Id
                           where i.PaymentBatchId == lote.Id && i.IsLive
                           orderby i.Id
                           select new { Item = i, Orden = o }).ToListAsync(ct);
        return filas.Select((f, n) => new LineaDelArchivo(f.Item, f.Orden, Valores(f.Orden, f.Item, n + 1))).ToList();
    }

    public static BankFileLineValues Valores(PaymentOrder o, PaymentBatchItem i, int linea)
    {
        var v = new BankFileLineValues();
        v[BankFieldSource.LineNumber] = linea;
        v[BankFieldSource.PayeeDocumentType] = o.BeneficiaryTaxIdType;
        v[BankFieldSource.PayeeDocument] = o.BeneficiaryTaxId;
        v[BankFieldSource.PayeeFullName] = o.BeneficiaryName;
        v[BankFieldSource.PayeeBankCode] = o.DestinationBankCode;
        v[BankFieldSource.PayeeAccountType] = o.DestinationAccountType;
        v[BankFieldSource.PayeeAccountNumber] = o.DestinationAccountNumber;
        v[BankFieldSource.Amount] = i.Amount;
        v[BankFieldSource.Concept] = o.Concept;
        return v;
    }

    public static async Task<BankFileContext> ContextoAsync(IApplicationDbContext db, PaymentBatch lote, BankFileFormat formato, DateTime ahora, CancellationToken ct)
    {
        var empresa = await db.Companies.AsNoTracking().Where(c => !c.IsDeleted).OrderBy(c => c.Id).FirstOrDefaultAsync(ct);
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(c => c.Id == lote.BankAccountId, ct);
        var banco = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == cuenta.BankId).Select(b => b.TransferCode).FirstOrDefaultAsync(ct);
        var version = await db.PaymentFiles.AsNoTracking().CountAsync(a => a.PaymentBatchId == lote.Id, ct) + 1;
        return new BankFileContext
        {
            CompanyNit = empresa?.TaxId ?? string.Empty,
            CompanyNitDv = empresa?.TaxIdCheckDigit,
            CompanyName = empresa?.Name ?? string.Empty,
            SourceAccountNumber = cuenta.AccountNumber,
            SourceAccountType = cuenta.AccountType == OwnBankAccountType.Checking ? "2" : "1",
            SourceBankCode = banco,
            SourceAgreementCode = formato.AgreementCode,
            PaymentDate = lote.PayDate,
            GeneratedAt = ahora,
            Sequence = version,
            BatchReference = lote.Number,
        };
    }

    /// <summary>La huella de una línea: lo que se coteja contra la respuesta (documento, banco, cuenta, valor).</summary>
    public static string HuellaDeLinea(string documento, string banco, string cuenta, decimal valor) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture,
            $"{documento.Trim()}|{banco.Trim()}|{cuenta.Trim()}|{valor:0.00}")))).ToLowerInvariant();
}

/// <summary>El archivo generado. (nuevo)</summary>
public sealed record PaymentFileDto(
    Guid PublicId,
    Guid BatchPublicId,
    string BatchNumber,
    int Version,
    string FormatCode,
    string FileName,
    string Sha256,
    long SizeBytes,
    int LineCount,
    decimal Total,
    DateTime GeneratedAt,
    string GeneratedByName,
    DateTime? DownloadedAt,
    DateTime? SentAt,
    string? UploadedAtBankBy,
    string? AuthorizedAtBankBy,
    DateTime? VoidedAt,
    string? VoidReason,
    DateTime? RejectedByBankAt);

internal static class ArchivosDePago
{
    public static async Task<PaymentFileDto> DtoAsync(IApplicationDbContext db, PaymentFile a, CancellationToken ct)
    {
        var lote = await db.PaymentBatches.AsNoTracking().Where(b => b.Id == a.PaymentBatchId).Select(b => new { b.PublicId, b.Number }).FirstAsync(ct);
        var formato = await db.BankFileFormats.AsNoTracking().IgnoreQueryFilters().Where(f => f.Id == a.BankFileFormatId).Select(f => f.Code).FirstAsync(ct);
        var genero = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == a.GeneratedByUserId).Select(u => u.Username).FirstOrDefaultAsync(ct);
        return new PaymentFileDto(a.PublicId, lote.PublicId, lote.Number, a.Version, formato, a.FileName, a.Sha256, a.SizeBytes, a.LineCount, a.Total, a.GeneratedAt,
            genero ?? string.Empty, a.DownloadedAt, a.SentAt, a.UploadedAtBankBy, a.AuthorizedAtBankBy, a.VoidedAt, a.VoidReason, a.RejectedByBankAt);
    }

    public static async Task<(PaymentFile Archivo, PaymentBatch Lote)?> VisibleAsync(IApplicationDbContext db, IAlcanceDeTesoreria alcance, Guid publicId, bool seguir,
        CancellationToken ct)
    {
        var archivos = seguir ? db.PaymentFiles : db.PaymentFiles.AsNoTracking();
        var archivo = await archivos.FirstOrDefaultAsync(a => a.PublicId == publicId, ct);
        if (archivo is null) return null;
        var lotes = seguir ? db.PaymentBatches : db.PaymentBatches.AsNoTracking();
        var lote = await lotes.FirstAsync(b => b.Id == archivo.PaymentBatchId, ct);
        return (await alcance.ObtenerAsync(ct)).IncluyeCuenta(lote.BankAccountId) ? (archivo, lote) : null;
    }

    /// <summary>Las órdenes vivas del lote pasan a <paramref name="estado"/>.</summary>
    public static async Task OrdenesAsync(IApplicationDbContext db, PaymentBatch lote, PaymentOrderState estado, bool soltar, CancellationToken ct)
    {
        var items = await db.PaymentBatchItems.Where(i => i.PaymentBatchId == lote.Id && i.IsLive).ToListAsync(ct);
        var ids = items.Select(i => i.PaymentOrderId).ToList();
        foreach (var o in await db.PaymentOrders.Where(o => ids.Contains(o.Id)).ToListAsync(ct)) o.Status = estado;
        if (!soltar) return;
        foreach (var i in items)
        {
            i.IsLive = false;
            i.Status = PaymentBatchItemState.Removed;
        }
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-batches/{id}/file</c> (<c>Treasury.PaymentFiles.Generate</c>): genera el archivo de un lote aprobado de forma «archivo» con el formato
/// sellado (<c>FlatFileWriter</c>, ámbito <c>TreasuryPayments</c>); el adjunto no borrable se sube <b>antes</b> de guardar la fila y cada línea guarda su huella.
/// Un lote con un archivo vigente no genera otro. (nuevo)
/// </summary>
public sealed record GeneratePaymentFileCommand(Guid BatchPublicId) : IRequest<Result<PaymentFileDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class GeneratePaymentFileCommandValidator : AbstractValidator<GeneratePaymentFileCommand>
{
    public GeneratePaymentFileCommandValidator() => RuleFor(x => x.BatchPublicId).NotEmpty();
}

public sealed class GeneratePaymentFileCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    ISender sender,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<GeneratePaymentFileCommand, Result<PaymentFileDto>>
{
    public const string TipoDeAdjunto = AdjuntosDeModulo.ArchivoDePagoDeTesoreria;

    public async Task<Result<PaymentFileDto>> Handle(GeneratePaymentFileCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var visto = await LotesDePago.VisibleAsync(db, alcance, request.BatchPublicId, seguir: false, ct);
        if (visto is null) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [visto.BankAccountId], ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [visto.Id], ct: ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == visto.Id, ct);
        if (lote.Form != PaymentBatchForm.File || lote.BankFileFormatId is not { } formatoId)
            return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.FormaDelLoteSinArchivo());
        if (lote.Status != PaymentBatchState.Approved) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.LoteNoAprobado(lote.Status.ToString()));

        var formato = await db.BankFileFormats.AsNoTracking().Include(f => f.Bank).Include(f => f.Fields).FirstAsync(f => f.Id == formatoId, ct);
        var lineas = await LineasDelArchivo.CargarAsync(db, lote, ct);
        if (lineas.Count == 0) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.LoteVacio());
        var ahora = reloj.UtcNow;
        var contexto = await LineasDelArchivo.ContextoAsync(db, lote, formato, ahora, ct);
        BankFileOutput salida;
        try
        {
            salida = FlatFileWriter.Escribir(formato, contexto, lineas.Select(l => l.Valores).ToList());
        }
        catch (BankFileWriteException ex)
        {
            var quien = ex.LineNumber >= 1 && ex.LineNumber <= lineas.Count ? lineas[ex.LineNumber - 1].Orden.BeneficiaryName : "?";
            return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.LineaDemasiadoLarga(ex.LineNumber, ex.Field, quien));
        }

        var archivo = new PaymentFile
        {
            PaymentBatchId = lote.Id, Version = contexto.Sequence, BankFileFormatId = formato.Id, FileName = salida.FileName, Sha256 = salida.Sha256,
            SizeBytes = salida.Content.LongLength, LineCount = salida.LineCount, Total = salida.TotalAmount, GeneratedByUserId = usuario, GeneratedAt = ahora,
        };
        // El archivo se guarda en COR_Attachments ANTES de la fila (FR-112): si el almacén falla, no queda un archivo «generado» que nadie puede descargar.
        var subida = await sender.Send(new UploadAttachmentCommand(TipoDeAdjunto, archivo.PublicId, salida.FileName, salida.ContentType, salida.Content), ct);
        if (subida.IsFailure) return Result.Failure<PaymentFileDto>(subida.Error);
        archivo.FileAttachmentPublicId = subida.Value;
        db.PaymentFiles.Add(archivo);
        await db.SaveChangesAsync(ct);
        for (var n = 0; n < lineas.Count; n++)
        {
            var l = lineas[n];
            db.PaymentFileLines.Add(new PaymentFileLine
            {
                PaymentFileId = archivo.Id, PaymentBatchItemId = l.Item.Id, LineNumber = n + 1, BeneficiaryTaxId = l.Orden.BeneficiaryTaxId,
                BankCode = l.Orden.DestinationBankCode ?? string.Empty, AccountNumber = l.Orden.DestinationAccountNumber ?? string.Empty, Amount = l.Item.Amount,
                LineSha256 = LineasDelArchivo.HuellaDeLinea(l.Orden.BeneficiaryTaxId, l.Orden.DestinationBankCode ?? string.Empty,
                    l.Orden.DestinationAccountNumber ?? string.Empty, l.Item.Amount),
                RecordText = n < salida.DetailTexts.Count ? Recortar(salida.DetailTexts[n], 1000) : string.Empty,
            });
        }
        lote.Status = PaymentBatchState.FileGenerated;
        await auditoria.AgregarAsync("Treasury.PaymentFile.Generated", "PaymentFile", archivo.PublicId, null,
            new { batch = lote.Number, format = formato.Code, file = salida.FileName, sha256 = salida.Sha256, lines = salida.LineCount, total = salida.TotalAmount }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ArchivosDePago.DtoAsync(db, archivo, ct));
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}

/// <summary>
/// <c>GET /api/treasury/payment-files/{id}/download</c> (<c>Treasury.PaymentFiles.Download</c>): un enlace firmado al archivo, para una persona distinta de quien
/// preparó o aprobó el lote (<c>Treasury.File.SameUserDownload</c>, FR-112); verifica la huella (<c>Treasury.File.Tampered</c>); la primera descarga queda en el
/// archivo y toda descarga en la auditoría. Es un comando porque cada enlace se audita. (nuevo)
/// </summary>
public sealed record DownloadPaymentFileCommand(Guid FilePublicId) : IRequest<Result<EnlaceDeDescargaDto>>;

public sealed class DownloadPaymentFileCommandValidator : AbstractValidator<DownloadPaymentFileCommand>
{
    public DownloadPaymentFileCommandValidator() => RuleFor(x => x.FilePublicId).NotEmpty();
}

public sealed class DownloadPaymentFileCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    SegregacionDeTesoreria segregacion,
    AprobacionDiferidaEnTesoreria aprobacion,
    AuditoriaDeTesoreria auditoria,
    IBlobStore store,
    IDateTimeService reloj,
    IOptions<LimitesDeAdjuntos> limites) : IRequestHandler<DownloadPaymentFileCommand, Result<EnlaceDeDescargaDto>>
{
    public async Task<Result<EnlaceDeDescargaDto>> Handle(DownloadPaymentFileCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (await ArchivosDePago.VisibleAsync(db, alcance, request.FilePublicId, seguir: true, ct) is not { } v)
            return Result.Failure<EnlaceDeDescargaDto>(ErroresDeTesoreria.NoEncontrado("El archivo de pago"));
        var (archivo, lote) = v;
        if (!archivo.Vigente) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeTesoreria.ArchivoAnulado());
        var aprobaron = (await aprobacion.EstadoAsync(lote.PublicId, ApprovalSubjects.PaymentBatchApproval, ct)).Aprobadores;
        var separada = await segregacion.DescargarArchivoAsync(lote.PreparedByUserId, aprobaron, usuario, ct);
        if (separada.IsFailure) return Result.Failure<EnlaceDeDescargaDto>(separada.Error);
        var adjunto = await db.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.PublicId == archivo.FileAttachmentPublicId, ct);
        if (adjunto is null) return Result.Failure<EnlaceDeDescargaDto>(ErroresDeTesoreria.ArchivoFaltante());
        if (!string.Equals(adjunto.Sha256Hex, archivo.Sha256, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<EnlaceDeDescargaDto>(ErroresDeTesoreria.ArchivoAlterado());

        if (archivo.DownloadedAt is null)
        {
            archivo.DownloadedAt = reloj.UtcNow;
            archivo.DownloadedByUserId = usuario;
        }
        await auditoria.AgregarAsync("Treasury.PaymentFile.Downloaded", "PaymentFile", archivo.PublicId, null,
            new { batch = lote.Number, file = archivo.FileName, sha256 = archivo.Sha256, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        if (adjunto.Format == FormatoDeAdjunto.AppEncrypted) return Result.Success(EnlaceDeDescargaDto.PorLaApi);
        var formato = await db.BankFileFormats.AsNoTracking().IgnoreQueryFilters().FirstAsync(f => f.Id == archivo.BankFileFormatId, ct);
        var tipo = formato.ContentType.Contains("charset", StringComparison.OrdinalIgnoreCase) ? formato.ContentType : $"{formato.ContentType}; charset={formato.Encoding}";
        return Result.Success(await AdjuntosDirectos.FirmarDescargaAsync(store, adjunto, reloj, limites.Value, ct, contentType: tipo, nombre: archivo.FileName));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-files/{id}/sent</c> (<c>Treasury.PaymentFiles.MarkSent</c>): marca enviado el archivo con quién lo cargó y quién lo autorizó en el
/// banco (FR-112); el lote y sus órdenes quedan enviados. <b>No</b> emite hecho ni resultado: «girada» llega cuando el banco lo confirma (FR-119). (nuevo)
/// </summary>
public sealed record MarkPaymentFileSentCommand(Guid FilePublicId, string UploadedAtBankBy, string AuthorizedAtBankBy) : IRequest<Result<PaymentFileDto>>,
    IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkPaymentFileSentCommandValidator : AbstractValidator<MarkPaymentFileSentCommand>
{
    public MarkPaymentFileSentCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.UploadedAtBankBy).NotEmpty().WithMessage("Indique quién cargó el archivo en el banco.").MaximumLength(150);
        RuleFor(x => x.AuthorizedAtBankBy).NotEmpty().WithMessage("Indique quién lo autorizó en el banco.").MaximumLength(150);
    }
}

public sealed class MarkPaymentFileSentCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    IDateTimeService reloj) : IRequestHandler<MarkPaymentFileSentCommand, Result<PaymentFileDto>>
{
    public async Task<Result<PaymentFileDto>> Handle(MarkPaymentFileSentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (await ArchivosDePago.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct) is not { } v)
            return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.NoEncontrado("El archivo de pago"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [v.Lote.BankAccountId], ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [v.Lote.Id], ct: ct);
        var archivo = await db.PaymentFiles.FirstAsync(a => a.Id == v.Archivo.Id, ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == v.Lote.Id, ct);
        if (!archivo.Vigente) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.ArchivoAnulado());
        if (archivo.SentAt is not null) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.ArchivoYaEnviado());
        var ahora = reloj.UtcNow;
        archivo.SentAt = ahora;
        archivo.SentByUserId = usuario;
        archivo.UploadedAtBankBy = request.UploadedAtBankBy.Trim();
        archivo.AuthorizedAtBankBy = request.AuthorizedAtBankBy.Trim();
        EnviosDeLote.Marcar(lote, usuario, ahora, archivo.UploadedAtBankBy, archivo.AuthorizedAtBankBy);
        await ArchivosDePago.OrdenesAsync(db, lote, PaymentOrderState.Sent, soltar: false, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ArchivosDePago.DtoAsync(db, archivo, ct));
    }
}

/// <summary>Marcar enviado un lote: el lote, quién lo envió y quién lo cargó y autorizó en el banco. (nuevo)</summary>
internal static class EnviosDeLote
{
    public static void Marcar(PaymentBatch lote, int usuario, DateTime ahora, string? cargo, string? autorizo)
    {
        lote.Status = PaymentBatchState.Sent;
        lote.SentAt = ahora;
        lote.SentByUserId = usuario;
        lote.UploadedAtBankBy = cargo;
        lote.AuthorizedAtBankBy = autorizo;
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-batches/{id}/sent</c> (<c>Treasury.PaymentFiles.MarkSent</c>, nuevo; FR-213): el lote de forma «portal del banco», digitado en el
/// portal sin archivo, queda enviado con quién lo cargó y quién lo autorizó allí. El resultado se registra a mano, uno a uno o en bloque (US11). (nuevo)
/// </summary>
public sealed record MarkPaymentBatchSentByPortalCommand(Guid BatchPublicId, string UploadedAtBankBy, string AuthorizedAtBankBy)
    : IRequest<Result<PaymentBatchDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkPaymentBatchSentByPortalCommandValidator : AbstractValidator<MarkPaymentBatchSentByPortalCommand>
{
    public MarkPaymentBatchSentByPortalCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEmpty();
        RuleFor(x => x.UploadedAtBankBy).NotEmpty().WithMessage("Indique quién digitó los pagos en el portal.").MaximumLength(150);
        RuleFor(x => x.AuthorizedAtBankBy).NotEmpty().WithMessage("Indique quién los autorizó en el portal.").MaximumLength(150);
    }
}

public sealed class MarkPaymentBatchSentByPortalCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    IDateTimeService reloj) : IRequestHandler<MarkPaymentBatchSentByPortalCommand, Result<PaymentBatchDto>>
{
    public async Task<Result<PaymentBatchDto>> Handle(MarkPaymentBatchSentByPortalCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var visto = await LotesDePago.VisibleAsync(db, alcance, request.BatchPublicId, seguir: false, ct);
        if (visto is null) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.NoEncontrado("El lote"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [visto.BankAccountId], ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [visto.Id], ct: ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == visto.Id, ct);
        if (lote.Form != PaymentBatchForm.BankPortal) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.FormaDelLoteSinArchivo());
        if (lote.Status != PaymentBatchState.Approved) return Result.Failure<PaymentBatchDto>(ErroresDeTesoreria.LoteNoAprobado(lote.Status.ToString()));
        EnviosDeLote.Marcar(lote, usuario, reloj.UtcNow, request.UploadedAtBankBy.Trim(), request.AuthorizedAtBankBy.Trim());
        await ArchivosDePago.OrdenesAsync(db, lote, PaymentOrderState.Sent, soltar: false, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await LotesDePago.DtoAsync(db, lote, null, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-files/{id}/void</c> (<c>Treasury.PaymentFiles.Void</c>): anula un archivo no enviado (<c>Treasury.File.AlreadySent</c> si ya se
/// envió); el lote queda anulado y sus órdenes vuelven a la bandeja, listas, <b>sin resultado</b> (lo que Tesorería anula por su cuenta no produce resultado,
/// FR-215). (nuevo)
/// </summary>
public sealed record VoidPaymentFileCommand(Guid FilePublicId, string Reason) : IRequest<Result<PaymentFileDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class VoidPaymentFileCommandValidator : AbstractValidator<VoidPaymentFileCommand>
{
    public VoidPaymentFileCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba el motivo de la anulación.").MaximumLength(500);
    }
}

public sealed class VoidPaymentFileCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    ICerrojoDeTesoreria cerrojo,
    IDateTimeService reloj) : IRequestHandler<VoidPaymentFileCommand, Result<PaymentFileDto>>
{
    public async Task<Result<PaymentFileDto>> Handle(VoidPaymentFileCommand request, CancellationToken ct)
    {
        if (await ArchivosDePago.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct) is not { } v)
            return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.NoEncontrado("El archivo de pago"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [v.Lote.BankAccountId], ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [v.Lote.Id], ct: ct);
        var ordenes = await db.PaymentBatchItems.AsNoTracking().Where(i => i.PaymentBatchId == v.Lote.Id && i.IsLive).Select(i => (long)i.PaymentOrderId).ToListAsync(ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, ordenes.Order().ToList(), ct: ct);
        var archivo = await db.PaymentFiles.FirstAsync(a => a.Id == v.Archivo.Id, ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == v.Lote.Id, ct);
        if (archivo.SentAt is not null) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.ArchivoYaEnviado());
        if (!archivo.Vigente) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.ArchivoAnulado());
        archivo.VoidedAt = reloj.UtcNow;
        archivo.VoidReason = request.Reason.Trim();
        lote.Status = PaymentBatchState.Voided;
        await ArchivosDePago.OrdenesAsync(db, lote, PaymentOrderState.Ready, soltar: true, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ArchivosDePago.DtoAsync(db, archivo, ct));
    }
}

/// <summary><c>GET /api/treasury/payment-files?batch=</c> (<c>Treasury.PaymentFiles.Generate</c> o <c>.Download</c>): los archivos de los lotes al alcance. (nuevo)</summary>
public sealed record ListPaymentFilesQuery(Guid? BatchPublicId = null) : IRequest<Result<IReadOnlyList<PaymentFileDto>>>;

public sealed class ListPaymentFilesQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<ListPaymentFilesQuery, Result<IReadOnlyList<PaymentFileDto>>>
{
    public async Task<Result<IReadOnlyList<PaymentFileDto>>> Handle(ListPaymentFilesQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = from a in db.PaymentFiles.AsNoTracking()
                       join b in db.PaymentBatches.AsNoTracking() on a.PaymentBatchId equals b.Id
                       select new { Archivo = a, Lote = b };
        if (request.BatchPublicId is { } lote) consulta = consulta.Where(x => x.Lote.PublicId == lote);
        var filas = (await consulta.OrderByDescending(x => x.Archivo.Id).Take(200).ToListAsync(ct)).Where(x => mio.IncluyeCuenta(x.Lote.BankAccountId)).ToList();
        var lista = new List<PaymentFileDto>(filas.Count);
        foreach (var f in filas) lista.Add(await ArchivosDePago.DtoAsync(db, f.Archivo, ct));
        return Result.Success<IReadOnlyList<PaymentFileDto>>(lista);
    }
}
