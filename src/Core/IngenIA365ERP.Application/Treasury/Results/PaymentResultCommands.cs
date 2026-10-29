using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.PaymentFiles;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Results;

// Feature 015, US11 (T0158; R-18, R-19; FR-118 a FR-121, FR-212, FR-213, FR-215; contracts/api.md §10; data-model §10.6, §10.7): la respuesta del banco a un archivo
// de pago cotejada línea por línea contra el archivo generado, la revisión de las líneas que discrepan, el registro manual del resultado (uno a uno o en bloque) con
// su soporte, y el rechazo técnico del archivo. Todo lo registra una persona; el único que escribe el resultado es RegistroDeResultadosDelGiro. (nuevo)

/// <summary>Una línea de la respuesta del banco. (nuevo)</summary>
public sealed record PaymentResponseLineDto(
    Guid PublicId,
    int LineNumber,
    int? FileLineNumber,
    string Outcome,
    string? BankCause,
    DateOnly BankDate,
    decimal Amount,
    string AccountNumber,
    string BeneficiaryTaxId,
    string? BankReference,
    bool Discrepancy,
    string? DiscrepancyDetail,
    bool Informed,
    DateTime? ReviewedAt,
    bool? ReviewAccepted,
    string? ReviewNote,
    string? OrderNumber);

/// <summary>Una respuesta del banco con sus líneas. (nuevo)</summary>
public sealed record PaymentResponseDto(
    Guid PublicId,
    Guid FilePublicId,
    string Source,
    string? FormatCode,
    string? FileName,
    string? Sha256,
    Guid? AttachmentPublicId,
    DateTime LoadedAt,
    string LoadedByName,
    int LineCount,
    int DiscrepancyCount,
    int InformedCount,
    IReadOnlyList<PaymentResponseLineDto> Lines);

/// <summary>Un resultado registrado de una orden. (nuevo)</summary>
public sealed record DisbursementResultDto(
    Guid PublicId,
    Guid OrderPublicId,
    string OrderNumber,
    string Outcome,
    DateOnly OccurredOn,
    decimal Amount,
    string TreasuryReference,
    string? CheckNumber,
    string? Reason,
    string Source,
    string RecordedByName,
    DateTime RecordedAt,
    Guid? SupportAttachmentPublicId,
    Guid? EmittedMessagePublicId,
    bool Repeated);

/// <summary>Lo común de los comandos de resultados. (nuevo)</summary>
internal static class ResultadosDelGiro
{
    public static DisbursementResultDto Dto(DisbursementResult r, PaymentOrder o, bool repetido = false) =>
        new(r.PublicId, o.PublicId, o.Number, r.Outcome.ToString(), r.OccurredOn, r.Amount, r.TreasuryReference, r.CheckNumber, r.Reason, r.Source.ToString(),
            r.RecordedByName, r.RecordedAt, r.SupportAttachmentPublicId, r.EmittedMessagePublicId, repetido);

    public static async Task<PaymentResponseDto> DtoAsync(IApplicationDbContext db, PaymentResponse r, CancellationToken ct)
    {
        var archivo = await db.PaymentFiles.AsNoTracking().Where(a => a.Id == r.PaymentFileId).Select(a => a.PublicId).FirstAsync(ct);
        var formato = r.BankFileFormatId is { } f
            ? await db.BankFileFormats.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == f).Select(x => x.Code).FirstOrDefaultAsync(ct)
            : null;
        var cargo = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == r.LoadedByUserId).Select(u => u.Username).FirstOrDefaultAsync(ct);
        var lineas = await (from l in db.PaymentResponseLines.AsNoTracking()
                            where l.PaymentResponseId == r.Id
                            join fl in db.PaymentFileLines.AsNoTracking() on l.PaymentFileLineId equals fl.Id into fls
                            from fl in fls.DefaultIfEmpty()
                            join i in db.PaymentBatchItems.AsNoTracking() on fl.PaymentBatchItemId equals i.Id into its
                            from i in its.DefaultIfEmpty()
                            join o in db.PaymentOrders.AsNoTracking() on i.PaymentOrderId equals o.Id into os
                            from o in os.DefaultIfEmpty()
                            orderby l.LineNumber
                            select new PaymentResponseLineDto(l.PublicId, l.LineNumber, fl == null ? null : fl.LineNumber, l.Outcome.ToString(), l.BankCause, l.BankDate,
                                l.Amount, l.AccountNumber, l.BeneficiaryTaxId, l.BankReference, l.Discrepancy, l.DiscrepancyDetail, l.DisbursementResultId != null,
                                l.ReviewedAt, l.ReviewAccepted, l.ReviewNote, o == null ? null : o.Number)).ToListAsync(ct);
        return new PaymentResponseDto(r.PublicId, archivo, r.Source.ToString(), formato, r.FileName, r.Sha256, r.ResponseAttachmentPublicId, r.LoadedAt,
            cargo ?? string.Empty, r.LineCount, r.DiscrepancyCount, r.InformedCount, lineas);
    }

    /// <summary>La segregación del resultado de un lote (FR-213, D19): con <c>TES_Setup.ResultWithThirdPerson</c>, quien registra no preparó ni aprobó el lote.</summary>
    public static async Task<Result> SegregarAsync(IApplicationDbContext db, SegregacionDeTesoreria segregacion, AprobacionDiferidaEnTesoreria aprobacion,
        PaymentBatch? lote, int usuario, CancellationToken ct)
    {
        if (lote is null) return Result.Success();
        var tercera = await db.TreasurySetups.AsNoTracking().Select(s => s.ResultWithThirdPerson).FirstOrDefaultAsync(ct);
        if (!tercera) return Result.Success();
        var aprobaron = (await aprobacion.EstadoAsync(lote.PublicId, ApprovalSubjects.PaymentBatchApproval, ct)).Aprobadores;
        return await segregacion.RegistrarResultadoAsync(true, lote.PreparedByUserId, aprobaron, usuario, ct);
    }

    /// <summary>El lote vigente de la orden, si lo tiene (sin seguimiento).</summary>
    public static async Task<PaymentBatch?> LoteAsync(IApplicationDbContext db, int orden, CancellationToken ct) =>
        await (from i in db.PaymentBatchItems.AsNoTracking()
               join b in db.PaymentBatches.AsNoTracking() on i.PaymentBatchId equals b.Id
               where i.PaymentOrderId == orden && i.IsLive
               select b).FirstOrDefaultAsync(ct);

    /// <summary>Toma los cerrojos de las órdenes: Recaudo primero (por su puerto), después setup, cuentas de origen, lotes y órdenes de Tesorería.</summary>
    public static async Task BloquearAsync(IApplicationDbContext db, ICerrojoDeTesoreria cerrojo, RegistroDeResultadosDelGiro registro,
        IReadOnlyCollection<PaymentOrder> ordenes, CancellationToken ct)
    {
        await registro.TomarCerrojosDeRecaudoAsync(ordenes, ct);
        var ids = ordenes.Select(o => o.Id).ToList();
        var lotes = await (from i in db.PaymentBatchItems.AsNoTracking()
                           join b in db.PaymentBatches.AsNoTracking() on i.PaymentBatchId equals b.Id
                           where ids.Contains(i.PaymentOrderId) && i.IsLive
                           select new { b.Id, b.BankAccountId }).Distinct().ToListAsync(ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        if (lotes.Count > 0)
        {
            await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, lotes.Select(l => (long)l.BankAccountId).Distinct().Order().ToList(), exclusivo: false, ct);
            await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, lotes.Select(l => (long)l.Id).Distinct().Order().ToList(), ct: ct);
        }
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, ids.Select(i => (long)i).Order().ToList(), ct: ct);
    }

    public static QuienRegistra Quien(Actor actor, int usuario) => new(usuario, actor.CentralUserId, actor.Name);

    public static bool ResultadoManual(string texto, out DisbursementOutcome resultado) =>
        Enum.TryParse(texto, ignoreCase: false, out resultado) && resultado is DisbursementOutcome.Disbursed or DisbursementOutcome.Rejected or DisbursementOutcome.CheckNotCashed;

    public static async Task<bool> SoporteExisteAsync(IApplicationDbContext db, Guid soporte, CancellationToken ct) =>
        await db.Attachments.AsNoTracking().AnyAsync(a => a.PublicId == soporte && a.OwnerEntityType == AdjuntosDeModulo.SoporteDeResultadoDelGiro, ct);
}

/// <summary>
/// <c>POST /api/treasury/payment-files/{id}/responses</c> (<c>Treasury.Results.Register</c>, multipart): carga la respuesta del banco a un archivo enviado con su formato
/// de vuelta (el del banco de la cuenta en <c>TreasuryPaymentResponse = 9</c>, el pedido, o <c>RESP-PAGOS-GENERICO</c>), guarda el archivo tal como llegó (adjunto no
/// borrable) <b>antes</b> de su fila, y coteja cada línea contra la línea del archivo generado por documento, cuenta y valor (FR-212). La que casa se informa
/// (girada o rechazada, a nombre de quien carga); la que discrepa queda <c>Discrepancy</c>, alerta <c>Tesoreria.DiscrepanciaEnRespuesta</c> y no se informa hasta su
/// revisión. Una línea cuyo resultado ya estaba no cambia nada. (nuevo)
/// </summary>
public sealed record LoadPaymentResponseCommand(Guid FilePublicId, string FileName, byte[] Content, Guid? FormatPublicId = null)
    : IRequest<Result<PaymentResponseDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class LoadPaymentResponseCommandValidator : AbstractValidator<LoadPaymentResponseCommand>
{
    public LoadPaymentResponseCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotNull().Must(c => c is { Length: > 0 }).WithMessage("No se recibió el archivo de la respuesta.");
    }
}

public sealed class LoadPaymentResponseCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    SegregacionDeTesoreria segregacion,
    AprobacionDiferidaEnTesoreria aprobacion,
    RegistroDeResultadosDelGiro registro,
    IAlertas alertas,
    AuditoriaDeTesoreria auditoria,
    ISender sender,
    IDateTimeService reloj) : IRequestHandler<LoadPaymentResponseCommand, Result<PaymentResponseDto>>
{
    public const string TipoDeAdjunto = AdjuntosDeModulo.RespuestaDelBancoDeTesoreria;

    public async Task<Result<PaymentResponseDto>> Handle(LoadPaymentResponseCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (await ArchivosDePago.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct) is not { } v)
            return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.NoEncontrado("El archivo de pago"));
        if (!v.Archivo.Vigente) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.ArchivoAnulado());
        if (v.Archivo.SentAt is null) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.ArchivoNoEnviado());
        var separada = await ResultadosDelGiro.SegregarAsync(db, segregacion, aprobacion, v.Lote, usuario, ct);
        if (separada.IsFailure) return Result.Failure<PaymentResponseDto>(separada.Error);

        var hoy = reloj.HoyLocal;
        var formato = await FormatoAsync(v.Lote.BankAccountId, request.FormatPublicId, hoy, ct);
        if (formato is null) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.FormatoDeRespuestaInexistente());
        var leido = FlatFileReader.Leer(formato, request.Content);
        if (!leido.IsValid)
            return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.RespuestaIlegible(leido.Errors.Take(50)
                .Select(e => (object)new { line = e.LineNumber, field = e.Field, message = e.Message }).ToList()));
        if (leido.Rows.Count == 0) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.RespuestaVacia());
        foreach (var fila in leido.Rows)
            if (Resultado(fila.Texto(BankFieldSource.Outcome)) is null)
                return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.ResultadoDeRespuestaDesconocido(fila.LineNumber, fila.Texto(BankFieldSource.Outcome) ?? string.Empty));

        var lineasDelArchivo = await db.PaymentFileLines.AsNoTracking().Where(l => l.PaymentFileId == v.Archivo.Id).ToListAsync(ct);
        var itemIds = lineasDelArchivo.Select(l => l.PaymentBatchItemId).ToList();
        var ordenDelItem = await db.PaymentBatchItems.AsNoTracking().Where(i => itemIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.PaymentOrderId, ct);
        var ordenIds = ordenDelItem.Values.Distinct().ToList();
        var ordenesLeidas = await db.PaymentOrders.AsNoTracking().Where(o => ordenIds.Contains(o.Id)).ToListAsync(ct);
        await ResultadosDelGiro.BloquearAsync(db, cerrojo, registro, ordenesLeidas, ct);

        var respuesta = new PaymentResponse
        {
            PaymentFileId = v.Archivo.Id, Source = PaymentResponseSource.BankFormat, BankFileFormatId = formato.Id, FileName = Recortar(request.FileName, 200),
            Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(request.Content)).ToLowerInvariant(), LoadedByUserId = usuario,
            LoadedAt = reloj.UtcNow, LineCount = leido.Rows.Count,
        };
        // La respuesta tal como llegó se guarda ANTES de su fila (como el archivo generado): es la evidencia del cotejo.
        var subida = await sender.Send(new UploadAttachmentCommand(TipoDeAdjunto, respuesta.PublicId, respuesta.FileName!, formato.ContentType, request.Content), ct);
        if (subida.IsFailure) return Result.Failure<PaymentResponseDto>(subida.Error);
        respuesta.ResponseAttachmentPublicId = subida.Value;
        db.PaymentResponses.Add(respuesta);
        await db.SaveChangesAsync(ct);

        var ordenes = await db.PaymentOrders.Where(o => ordenIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);
        var quien = ResultadosDelGiro.Quien(actor, usuario);
        var discrepancias = new List<PaymentResponseLine>();
        var numero = 0;
        foreach (var fila in leido.Rows)
        {
            numero++;
            var linea = Linea(respuesta.Id, fila, numero, hoy);
            var delArchivo = Casar(lineasDelArchivo, fila, linea);
            linea.PaymentFileLineId = delArchivo?.Id;
            linea.DiscrepancyDetail = Discrepa(delArchivo, linea);
            linea.Discrepancy = linea.DiscrepancyDetail is not null;
            db.PaymentResponseLines.Add(linea);
            await db.SaveChangesAsync(ct);
            if (linea.Discrepancy)
            {
                discrepancias.Add(linea);
                continue;
            }
            var orden = ordenes[ordenDelItem[delArchivo!.PaymentBatchItemId]];
            var registrado = await registro.RegistrarAsync(orden, ResultadoDeLaLinea(linea), quien, ct);
            if (registrado.IsFailure)
            {
                linea.Discrepancy = true;
                linea.DiscrepancyDetail = Recortar(registrado.Error.Code, 300);
                discrepancias.Add(linea);
                continue;
            }
            await db.SaveChangesAsync(ct);
            linea.DisbursementResultId = registrado.Value.Resultado.Id;
            if (!registrado.Value.Repetido) respuesta.InformedCount++;
        }
        respuesta.DiscrepancyCount = discrepancias.Count;
        foreach (var d in discrepancias)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaDiscrepanciaEnRespuesta, "Respuesta del banco con discrepancia",
                string.Create(CultureInfo.InvariantCulture,
                    $"La línea {d.LineNumber} de la respuesta del archivo {v.Archivo.FileName} no coincide con el archivo generado ({d.DiscrepancyDetail}): revísela antes de informar."),
                "PaymentResponseLine", d.PublicId, DedupKey: $"Tesoreria.DiscrepanciaEnRespuesta:{d.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
        await auditoria.AgregarAsync("Treasury.PaymentResponse.Loaded", "PaymentResponse", respuesta.PublicId, null,
            new { file = v.Archivo.FileName, format = formato.Code, lines = respuesta.LineCount, discrepancies = respuesta.DiscrepancyCount, informed = respuesta.InformedCount }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ResultadosDelGiro.DtoAsync(db, respuesta, ct));
    }

    /// <summary>El formato de vuelta: el pedido, el del banco de la cuenta vigente, o el genérico.</summary>
    private async Task<BankFileFormat?> FormatoAsync(int cuentaId, Guid? pedido, DateOnly hoy, CancellationToken ct)
    {
        var banco = await db.OwnBankAccounts.AsNoTracking().Where(c => c.Id == cuentaId).Select(c => c.BankId).FirstAsync(ct);
        var candidatos = await db.BankFileFormats.AsNoTracking().Include(f => f.Fields)
            .Where(f => f.Scope == BankFileScope.TreasuryPaymentResponse && f.IsActive && f.ValidFrom <= hoy && (f.ValidTo == null || f.ValidTo >= hoy))
            .ToListAsync(ct);
        if (pedido is { } id) return candidatos.FirstOrDefault(f => f.PublicId == id && (f.BankId == null || f.BankId == banco));
        return candidatos.Where(f => f.BankId == banco).OrderByDescending(f => f.ValidFrom).FirstOrDefault()
               ?? candidatos.Where(f => f.BankId == null && f.Code == FormatosGenericosDeRecaudoYTesoreria.RespuestaDePagos).FirstOrDefault()
               ?? candidatos.Where(f => f.BankId == null).OrderByDescending(f => f.ValidFrom).FirstOrDefault();
    }

    public static PaymentResponseOutcome? Resultado(string? texto) => (texto ?? string.Empty).Trim().ToUpperInvariant() switch
    {
        "PAGADO" or "PAGADA" or "PAID" or "P" or "1" or "OK" or "APLICADO" => PaymentResponseOutcome.Paid,
        "RECHAZADO" or "RECHAZADA" or "REJECTED" or "R" or "2" => PaymentResponseOutcome.Rejected,
        _ => null,
    };

    private static PaymentResponseLine Linea(int respuesta, FlatFileRow fila, int numero, DateOnly hoy) => new()
    {
        PaymentResponseId = respuesta,
        LineNumber = (int?)fila.Entero(BankFieldSource.LineNumber) ?? numero,
        Outcome = Resultado(fila.Texto(BankFieldSource.Outcome))!.Value,
        BankCause = Nulo(fila.Texto(BankFieldSource.BankCause), 200),
        BankDate = fila.Fecha(BankFieldSource.BankDate) ?? hoy,
        Amount = fila.Monto(BankFieldSource.Amount) ?? 0m,
        AccountNumber = Recortar(fila.Texto(BankFieldSource.AccountNumber) ?? fila.Texto(BankFieldSource.PayeeAccountNumber) ?? string.Empty, 30),
        BeneficiaryTaxId = Recortar(fila.Texto(BankFieldSource.BeneficiaryTaxId) ?? fila.Texto(BankFieldSource.PayeeDocument) ?? string.Empty, 20),
        BankReference = Nulo(fila.Texto(BankFieldSource.BankReference), 60),
    };

    /// <summary>La línea del archivo que casa: por número de línea; si no, por documento y valor (una sola).</summary>
    private static PaymentFileLine? Casar(IReadOnlyList<PaymentFileLine> lineas, FlatFileRow fila, PaymentResponseLine linea)
    {
        if (fila.Entero(BankFieldSource.LineNumber) is not null && lineas.FirstOrDefault(l => l.LineNumber == linea.LineNumber) is { } porNumero) return porNumero;
        var candidatas = lineas.Where(l => l.BeneficiaryTaxId.Trim() == linea.BeneficiaryTaxId.Trim() && l.Amount == linea.Amount).ToList();
        return candidatas.Count == 1 ? candidatas[0] : null;
    }

    /// <summary>Lo que discrepa entre la línea de la respuesta y la del archivo (nulo si casa): el cotejo de FR-212 sobre la huella de la línea.</summary>
    public static string? Discrepa(PaymentFileLine? delArchivo, PaymentResponseLine linea)
    {
        if (delArchivo is null) return "sin línea en el archivo generado";
        var cuenta = string.IsNullOrWhiteSpace(linea.AccountNumber) ? delArchivo.AccountNumber : linea.AccountNumber;
        if (LineasDelArchivo.HuellaDeLinea(linea.BeneficiaryTaxId, delArchivo.BankCode, cuenta, linea.Amount) == delArchivo.LineSha256) return null;
        var partes = new List<string>(3);
        if (linea.BeneficiaryTaxId.Trim() != delArchivo.BeneficiaryTaxId.Trim()) partes.Add("documento");
        if (cuenta.Trim() != delArchivo.AccountNumber.Trim()) partes.Add("cuenta");
        if (linea.Amount != delArchivo.Amount) partes.Add("valor");
        return partes.Count == 0 ? "huella distinta" : string.Join(", ", partes);
    }

    public static ResultadoARegistrar ResultadoDeLaLinea(PaymentResponseLine l) => new(
        l.Outcome == PaymentResponseOutcome.Paid ? DisbursementOutcome.Disbursed : DisbursementOutcome.Rejected,
        l.BankDate, l.BankReference ?? string.Empty, null, l.Outcome == PaymentResponseOutcome.Rejected ? l.BankCause ?? "Rechazada por el banco" : null,
        PaymentResponseSource.BankFormat, PaymentResponseLineId: l.Id);

    private static string Recortar(string texto, int largo)
    {
        var t = texto.Trim();
        return t.Length <= largo ? t : t[..largo];
    }

    private static string? Nulo(string? texto, int largo) => string.IsNullOrWhiteSpace(texto) ? null : Recortar(texto, largo);
}

/// <summary><c>GET /api/treasury/payment-files/{id}/responses/{rid}</c> (<c>Treasury.Results.Register</c> o <c>Treasury.Orders.View</c>): la respuesta con sus líneas. (nuevo)</summary>
public sealed record GetPaymentResponseQuery(Guid FilePublicId, Guid ResponsePublicId) : IRequest<Result<PaymentResponseDto>>;

public sealed class GetPaymentResponseQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance) : IRequestHandler<GetPaymentResponseQuery, Result<PaymentResponseDto>>
{
    public async Task<Result<PaymentResponseDto>> Handle(GetPaymentResponseQuery request, CancellationToken ct)
    {
        if (await ArchivosDePago.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct) is not { } v)
            return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.NoEncontrado("El archivo de pago"));
        var r = await db.PaymentResponses.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.ResponsePublicId && x.PaymentFileId == v.Archivo.Id, ct);
        return r is null
            ? Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.NoEncontrado("La respuesta"))
            : Result.Success(await ResultadosDelGiro.DtoAsync(db, r, ct));
    }
}

/// <summary><c>GET /api/treasury/payment-files/{id}/responses</c> (nuevo): las respuestas cargadas de un archivo, con sus líneas.</summary>
public sealed record ListPaymentResponsesQuery(Guid FilePublicId) : IRequest<Result<IReadOnlyList<PaymentResponseDto>>>;

public sealed class ListPaymentResponsesQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<ListPaymentResponsesQuery, Result<IReadOnlyList<PaymentResponseDto>>>
{
    public async Task<Result<IReadOnlyList<PaymentResponseDto>>> Handle(ListPaymentResponsesQuery request, CancellationToken ct)
    {
        if (await ArchivosDePago.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct) is not { } v)
            return Result.Failure<IReadOnlyList<PaymentResponseDto>>(ErroresDeTesoreria.NoEncontrado("El archivo de pago"));
        var lista = new List<PaymentResponseDto>();
        foreach (var r in await db.PaymentResponses.AsNoTracking().Where(x => x.PaymentFileId == v.Archivo.Id).OrderByDescending(x => x.Id).ToListAsync(ct))
            lista.Add(await ResultadosDelGiro.DtoAsync(db, r, ct));
        return Result.Success<IReadOnlyList<PaymentResponseDto>>(lista);
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-files/{id}/responses/{rid}/lines/{lineId}/review</c> (<c>Treasury.Results.Register</c>): una persona revisa una línea con discrepancia
/// (FR-212, US11-7): la acepta —se informa su resultado con la orden de la línea del archivo— o la descarta —la orden sigue enviada y su resultado se registra a mano—.
/// Con motivo. (nuevo)
/// </summary>
public sealed record ReviewPaymentResponseLineCommand(Guid FilePublicId, Guid ResponsePublicId, Guid LinePublicId, bool Accept, string Note)
    : IRequest<Result<PaymentResponseDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class ReviewPaymentResponseLineCommandValidator : AbstractValidator<ReviewPaymentResponseLineCommand>
{
    public ReviewPaymentResponseLineCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.ResponsePublicId).NotEmpty();
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Note).NotEmpty().WithMessage("Escriba qué revisó y por qué decide así.").MaximumLength(500);
    }
}

public sealed class ReviewPaymentResponseLineCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    SegregacionDeTesoreria segregacion,
    AprobacionDiferidaEnTesoreria aprobacion,
    RegistroDeResultadosDelGiro registro,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<ReviewPaymentResponseLineCommand, Result<PaymentResponseDto>>
{
    public async Task<Result<PaymentResponseDto>> Handle(ReviewPaymentResponseLineCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (await ArchivosDePago.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct) is not { } v)
            return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.NoEncontrado("El archivo de pago"));
        var respuesta = await db.PaymentResponses.FirstOrDefaultAsync(x => x.PublicId == request.ResponsePublicId && x.PaymentFileId == v.Archivo.Id, ct);
        if (respuesta is null) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.NoEncontrado("La respuesta"));
        var leida = await db.PaymentResponseLines.AsNoTracking().FirstOrDefaultAsync(l => l.PublicId == request.LinePublicId && l.PaymentResponseId == respuesta.Id, ct);
        if (leida is null) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.NoEncontrado("La línea"));
        if (!leida.Discrepancy) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.LineaSinDiscrepancia());
        if (leida.ReviewedAt is not null) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.LineaYaRevisada());
        var separada = await ResultadosDelGiro.SegregarAsync(db, segregacion, aprobacion, v.Lote, usuario, ct);
        if (separada.IsFailure) return Result.Failure<PaymentResponseDto>(separada.Error);

        PaymentOrder? orden = null;
        if (request.Accept)
        {
            if (leida.PaymentFileLineId is not { } fl) return Result.Failure<PaymentResponseDto>(ErroresDeTesoreria.Discrepancia(leida.LineNumber, leida.DiscrepancyDetail ?? "sin línea"));
            var ordenId = await (from l in db.PaymentFileLines.AsNoTracking()
                                 join i in db.PaymentBatchItems.AsNoTracking() on l.PaymentBatchItemId equals i.Id
                                 where l.Id == fl
                                 select i.PaymentOrderId).FirstAsync(ct);
            var leidaOrden = await db.PaymentOrders.AsNoTracking().FirstAsync(o => o.Id == ordenId, ct);
            await ResultadosDelGiro.BloquearAsync(db, cerrojo, registro, [leidaOrden], ct);
            orden = await db.PaymentOrders.FirstAsync(o => o.Id == ordenId, ct);
        }
        else await cerrojo.TomarSetupAsync(ct: ct);

        var linea = await db.PaymentResponseLines.FirstAsync(l => l.Id == leida.Id, ct);
        linea.ReviewedByUserId = usuario;
        linea.ReviewedAt = reloj.UtcNow;
        linea.ReviewAccepted = request.Accept;
        linea.ReviewNote = request.Note.Trim();
        if (orden is not null)
        {
            var registrado = await registro.RegistrarAsync(orden, LoadPaymentResponseCommandHandler.ResultadoDeLaLinea(linea), ResultadosDelGiro.Quien(actor, usuario), ct);
            if (registrado.IsFailure) return Result.Failure<PaymentResponseDto>(registrado.Error);
            await db.SaveChangesAsync(ct);
            linea.DisbursementResultId = registrado.Value.Resultado.Id;
            respuesta.InformedCount++;
        }
        await auditoria.AgregarAsync("Treasury.PaymentResponse.LineReviewed", "PaymentResponseLine", linea.PublicId, null,
            new { line = linea.LineNumber, discrepancy = linea.DiscrepancyDetail, accepted = request.Accept, note = linea.ReviewNote, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ResultadosDelGiro.DtoAsync(db, respuesta, ct));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-orders/{id}/results</c> (<c>Treasury.Results.Register</c>): registro manual del resultado de una orden enviada (girada, rechazada o
/// cheque no cobrado) <b>con su soporte</b> (<c>Treasury.Result.SupportRequired</c>, FR-213), a nombre de la persona. «Anulada» no se registra a mano: responde a la
/// anulación del origen (FR-215). Repetir el mismo resultado devuelve el que ya estaba. (nuevo)
/// </summary>
public sealed record RegisterDisbursementResultCommand(Guid OrderPublicId, string Outcome, DateOnly OccurredOn, string? TreasuryReference, string? Reason,
    string? CheckNumber, Guid? SupportAttachmentPublicId) : IRequest<Result<DisbursementResultDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RegisterDisbursementResultCommandValidator : AbstractValidator<RegisterDisbursementResultCommand>
{
    public RegisterDisbursementResultCommandValidator()
    {
        RuleFor(x => x.OrderPublicId).NotEmpty();
        RuleFor(x => x.Outcome).NotEmpty();
        RuleFor(x => x.TreasuryReference).MaximumLength(60);
        RuleFor(x => x.Reason).MaximumLength(500);
        RuleFor(x => x.CheckNumber).MaximumLength(30);
    }
}

public sealed class RegisterDisbursementResultCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    SegregacionDeTesoreria segregacion,
    AprobacionDiferidaEnTesoreria aprobacion,
    RegistroDeResultadosDelGiro registro,
    AuditoriaDeTesoreria auditoria) : IRequestHandler<RegisterDisbursementResultCommand, Result<DisbursementResultDto>>
{
    public async Task<Result<DisbursementResultDto>> Handle(RegisterDisbursementResultCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<DisbursementResultDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var r = await RegistroManual.RegistrarAsync(db, alcance, cerrojo, segregacion, aprobacion, registro, actor, usuario,
            new RegistroManual.Pedido(request.OrderPublicId, request.Outcome, request.OccurredOn, request.TreasuryReference, request.Reason, request.CheckNumber),
            request.SupportAttachmentPublicId, ct);
        if (r.IsFailure) return r;
        await auditoria.AgregarAsync("Treasury.DisbursementResult.Registered", "DisbursementResult", r.Value.PublicId, null,
            new { order = r.Value.OrderNumber, outcome = r.Value.Outcome, support = request.SupportAttachmentPublicId, repeated = r.Value.Repeated, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return r;
    }
}

/// <summary>Lo común del registro manual uno a uno y en bloque. (nuevo)</summary>
internal static class RegistroManual
{
    public sealed record Pedido(Guid OrderPublicId, string Outcome, DateOnly OccurredOn, string? TreasuryReference, string? Reason, string? CheckNumber);

    public static async Task<Result<DisbursementResultDto>> RegistrarAsync(IApplicationDbContext db, IAlcanceDeTesoreria alcance, ICerrojoDeTesoreria cerrojo,
        SegregacionDeTesoreria segregacion, AprobacionDiferidaEnTesoreria aprobacion, RegistroDeResultadosDelGiro registro, Actor actor, int usuario, Pedido p,
        Guid? soporte, CancellationToken ct)
    {
        if (soporte is not { } s || s == Guid.Empty || !await ResultadosDelGiro.SoporteExisteAsync(db, s, ct))
            return Result.Failure<DisbursementResultDto>(ErroresDeTesoreria.SoporteRequerido());
        if (!ResultadosDelGiro.ResultadoManual(p.Outcome, out var resultado)) return Result.Failure<DisbursementResultDto>(ErroresDeTesoreria.ResultadoInvalido(p.Outcome));
        if (resultado != DisbursementOutcome.Disbursed && string.IsNullOrWhiteSpace(p.Reason)) return Result.Failure<DisbursementResultDto>(ErroresDeTesoreria.CausalRequerida());
        var leida = await db.PaymentOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == p.OrderPublicId, ct);
        if (leida is null) return Result.Failure<DisbursementResultDto>(ErroresDeTesoreria.NoEncontrado("La orden"));
        var lote = await ResultadosDelGiro.LoteAsync(db, leida.Id, ct);
        if (lote is not null && !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(lote.BankAccountId))
            return Result.Failure<DisbursementResultDto>(ErroresDeTesoreria.NoEncontrado("La orden"));
        var separada = await ResultadosDelGiro.SegregarAsync(db, segregacion, aprobacion, lote, usuario, ct);
        if (separada.IsFailure) return Result.Failure<DisbursementResultDto>(separada.Error);
        await ResultadosDelGiro.BloquearAsync(db, cerrojo, registro, [leida], ct);
        var orden = await db.PaymentOrders.FirstAsync(o => o.Id == leida.Id, ct);
        var registrado = await registro.RegistrarAsync(orden,
            new ResultadoARegistrar(resultado, p.OccurredOn, p.TreasuryReference ?? orden.Number, p.CheckNumber, p.Reason, PaymentResponseSource.Manual, s),
            ResultadosDelGiro.Quien(actor, usuario), ct);
        if (registrado.IsFailure) return Result.Failure<DisbursementResultDto>(registrado.Error);
        await db.SaveChangesAsync(ct);
        return Result.Success(ResultadosDelGiro.Dto(registrado.Value.Resultado, orden, registrado.Value.Repetido));
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-orders/results/bulk</c> (<c>Treasury.Results.Register</c>; FR-213): el registro manual en bloque —típico del portal del banco— con un
/// solo soporte para todas; todo o nada: si una no cabe, no se registra ninguna. (nuevo)
/// </summary>
public sealed record RegisterDisbursementResultsBulkCommand(IReadOnlyList<RegisterDisbursementResultsBulkCommand.Item> Items, Guid? SupportAttachmentPublicId)
    : IRequest<Result<IReadOnlyList<DisbursementResultDto>>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public sealed record Item(Guid OrderPublicId, string Outcome, DateOnly OccurredOn, string? TreasuryReference, string? Reason, string? CheckNumber);

    public Guid OperationKey { get; init; }
}

public sealed class RegisterDisbursementResultsBulkCommandValidator : AbstractValidator<RegisterDisbursementResultsBulkCommand>
{
    public RegisterDisbursementResultsBulkCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty().WithMessage("Indique las órdenes y su resultado.");
        RuleFor(x => x.Items).Must(i => i is null || i.Count <= 500).WithMessage("A lo sumo 500 órdenes por registro en bloque.");
        RuleFor(x => x.Items).Must(i => i is null || i.Select(x => x.OrderPublicId).Distinct().Count() == i.Count).WithMessage("Una orden va una sola vez.");
    }
}

public sealed class RegisterDisbursementResultsBulkCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    SegregacionDeTesoreria segregacion,
    AprobacionDiferidaEnTesoreria aprobacion,
    RegistroDeResultadosDelGiro registro,
    AuditoriaDeTesoreria auditoria) : IRequestHandler<RegisterDisbursementResultsBulkCommand, Result<IReadOnlyList<DisbursementResultDto>>>
{
    public async Task<Result<IReadOnlyList<DisbursementResultDto>>> Handle(RegisterDisbursementResultsBulkCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<IReadOnlyList<DisbursementResultDto>>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        // Los cerrojos de todas las órdenes antes de registrar ninguna, en orden fijo (orden global Recaudo → Tesorería).
        var ids = request.Items.Select(i => i.OrderPublicId).ToList();
        var leidas = await db.PaymentOrders.AsNoTracking().Where(o => ids.Contains(o.PublicId)).ToListAsync(ct);
        await ResultadosDelGiro.BloquearAsync(db, cerrojo, registro, leidas, ct);
        var hechos = new List<DisbursementResultDto>(request.Items.Count);
        foreach (var i in request.Items)
        {
            var r = await RegistroManual.RegistrarAsync(db, alcance, cerrojo, segregacion, aprobacion, registro, actor, usuario,
                new RegistroManual.Pedido(i.OrderPublicId, i.Outcome, i.OccurredOn, i.TreasuryReference, i.Reason, i.CheckNumber), request.SupportAttachmentPublicId, ct);
            if (r.IsFailure) return Result.Failure<IReadOnlyList<DisbursementResultDto>>(r.Error);
            hechos.Add(r.Value);
        }
        await auditoria.AgregarAsync("Treasury.DisbursementResult.BulkRegistered", "PaymentOrder", null, null,
            new { orders = hechos.Select(h => h.OrderNumber).ToList(), support = request.SupportAttachmentPublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success<IReadOnlyList<DisbursementResultDto>>(hechos);
    }
}

/// <summary>
/// <c>POST /api/treasury/payment-files/{id}/rejected-by-bank</c> (<c>Treasury.PaymentFiles.Void</c>; FR-120): el banco rechazó el archivo entero por forma. No se informa
/// al origen: el archivo queda «rechazado por el banco», el lote también, y sus órdenes vuelven a la bandeja, listas y sin resultado, para otro archivo. Si alguna orden
/// ya tiene resultado, no se marca: ese archivo sí lo procesó el banco. (nuevo)
/// </summary>
public sealed record MarkPaymentFileRejectedByBankCommand(Guid FilePublicId, string Reason) : IRequest<Result<PaymentFileDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class MarkPaymentFileRejectedByBankCommandValidator : AbstractValidator<MarkPaymentFileRejectedByBankCommand>
{
    public MarkPaymentFileRejectedByBankCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Escriba lo que dijo el banco al rechazar el archivo.").MaximumLength(500);
    }
}

public sealed class MarkPaymentFileRejectedByBankCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    ICerrojoDeTesoreria cerrojo,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<MarkPaymentFileRejectedByBankCommand, Result<PaymentFileDto>>
{
    public const string CodigoConResultados = "Treasury.File.HasResults";

    public async Task<Result<PaymentFileDto>> Handle(MarkPaymentFileRejectedByBankCommand request, CancellationToken ct)
    {
        if (await ArchivosDePago.VisibleAsync(db, alcance, request.FilePublicId, seguir: false, ct) is not { } v)
            return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.NoEncontrado("El archivo de pago"));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [v.Lote.BankAccountId], ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Lote, [v.Lote.Id], ct: ct);
        var ordenes = await db.PaymentBatchItems.AsNoTracking().Where(i => i.PaymentBatchId == v.Lote.Id && i.IsLive).Select(i => i.PaymentOrderId).ToListAsync(ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Ordenes, ordenes.Select(o => (long)o).Order().ToList(), ct: ct);
        var archivo = await db.PaymentFiles.FirstAsync(a => a.Id == v.Archivo.Id, ct);
        var lote = await db.PaymentBatches.FirstAsync(b => b.Id == v.Lote.Id, ct);
        if (!archivo.Vigente) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.ArchivoAnulado());
        if (archivo.SentAt is null) return Result.Failure<PaymentFileDto>(ErroresDeTesoreria.ArchivoNoEnviado());
        if (await db.DisbursementResults.AsNoTracking().AnyAsync(r => ordenes.Contains(r.PaymentOrderId), ct))
            return Result.Failure<PaymentFileDto>(new Error(CodigoConResultados,
                "Algunas órdenes del archivo ya tienen resultado: el banco sí lo procesó. Registre el resultado de las demás en vez de rechazar el archivo."));
        archivo.RejectedByBankAt = reloj.UtcNow;
        archivo.RejectedByBankReason = request.Reason.Trim();
        lote.Status = PaymentBatchState.RejectedByBank;
        await ArchivosDePago.OrdenesAsync(db, lote, PaymentOrderState.Ready, soltar: true, ct);
        await auditoria.AgregarAsync("Treasury.PaymentFile.RejectedByBank", "PaymentFile", archivo.PublicId, null,
            new { batch = lote.Number, file = archivo.FileName, reason = archivo.RejectedByBankReason, orders = ordenes.Count }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ArchivosDePago.DtoAsync(db, archivo, ct));
    }
}

/// <summary><c>GET /api/treasury/payment-orders/{id}/results</c> (<c>Treasury.Orders.View</c>, nuevo): los resultados registrados de una orden. (nuevo)</summary>
public sealed record ListDisbursementResultsQuery(Guid OrderPublicId) : IRequest<Result<IReadOnlyList<DisbursementResultDto>>>;

public sealed class ListDisbursementResultsQueryHandler(IApplicationDbContext db) : IRequestHandler<ListDisbursementResultsQuery, Result<IReadOnlyList<DisbursementResultDto>>>
{
    public async Task<Result<IReadOnlyList<DisbursementResultDto>>> Handle(ListDisbursementResultsQuery request, CancellationToken ct)
    {
        var orden = await db.PaymentOrders.AsNoTracking().FirstOrDefaultAsync(o => o.PublicId == request.OrderPublicId, ct);
        if (orden is null) return Result.Failure<IReadOnlyList<DisbursementResultDto>>(ErroresDeTesoreria.NoEncontrado("La orden"));
        var lista = await db.DisbursementResults.AsNoTracking().Where(r => r.PaymentOrderId == orden.Id).OrderBy(r => r.Id).ToListAsync(ct);
        return Result.Success<IReadOnlyList<DisbursementResultDto>>(lista.Select(r => ResultadosDelGiro.Dto(r, orden)).ToList());
    }
}
