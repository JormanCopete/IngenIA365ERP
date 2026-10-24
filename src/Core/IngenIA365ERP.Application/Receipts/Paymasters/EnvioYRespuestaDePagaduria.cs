using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Paymasters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Paymasters;

/// <summary>
/// <c>POST /api/receipts/payroll-files/{id}/sent</c> (<c>Receipts.Paymasters.ConfirmSent</c>, con clave; FR-076): confirma el envío del archivo a la pagaduría —canal,
/// fecha y radicado— y emite <c>EnvioDeDescuentosConfirmado</c> v1 a Cartera con la huella del archivo, en la misma transacción. Repetir sobre un archivo ya enviado no
/// cambia nada (devuelve el archivo); sólo se confirma un archivo <c>Received</c>. (nuevo)
/// </summary>
public sealed record ConfirmPayrollFileSentCommand(Guid FilePublicId, PayrollFileChannel Channel, DateTime? SentAt = null, string? Reference = null)
    : IRequest<Result<PayrollDeductionFileDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfirmPayrollFileSentCommandValidator : AbstractValidator<ConfirmPayrollFileSentCommand>
{
    public ConfirmPayrollFileSentCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.Channel).IsInEnum();
        RuleFor(x => x.Reference).MaximumLength(60);
    }
}

public sealed class ConfirmPayrollFileSentCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<ConfirmPayrollFileSentCommand, Result<PayrollDeductionFileDto>>
{
    public const string ClaseDeEnvio = "PayrollFileDelivery";

    public async Task<Result<PayrollDeductionFileDto>> Handle(ConfirmPayrollFileSentCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PayrollDeductionFileDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        await cerrojo.TomarSetupAsync(ct: ct);
        var f = await PagaduriasDeRecaudo.VisibleAsync(db, alcance, request.FilePublicId, seguir: true, ct);
        if (f is null) return Result.Failure<PayrollDeductionFileDto>(ErroresDeRecaudo.NoEncontrado("El archivo de descuentos"));
        if (await db.PayrollFileSendings.AnyAsync(s => s.PayrollDeductionFileId == f.Id, ct))
            return Result.Success(await PagaduriasDeRecaudo.DtoAsync(db, f, conLineas: false, ct));
        if (f.Status != PayrollDeductionFileState.Received) return Result.Failure<PayrollDeductionFileDto>(ErroresDeRecaudo.ArchivoNoEnviable(f.Status.ToString()));

        var enviado = DateTime.SpecifyKind(request.SentAt ?? reloj.UtcNow, DateTimeKind.Utc);
        var envio = new PayrollFileSending
        {
            PayrollDeductionFileId = f.Id, Channel = request.Channel, SentAt = enviado, Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim(),
            SentByUserId = usuario,
        };
        var mensaje = await emision.MensajeDeLibranzaAsync(f, envio.PublicId, ClaseDeEnvio, envio.Reference ?? f.FileNumber, new EnvioDeDescuentosConfirmadoV1
        {
            FileMessageId = f.FileMessagePublicId, FilePublicId = f.FilePublicId, FileVersion = f.FileVersion, FileSha256 = f.FileSha256,
            SentAt = new DateTimeOffset(enviado), Channel = request.Channel.ToString(), Reference = envio.Reference,
            SentBy = new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name },
        }, reloj.HoyLocal, ct);
        envio.EmittedMessagePublicId = mensaje.PublicId;
        db.PayrollFileSendings.Add(envio);
        f.Status = PayrollDeductionFileState.Sent;
        await auditoria.AgregarAsync("Receipts.PayrollFile.Sent", "PayrollDeductionFile", f.PublicId, null,
            new { file = f.FileNumber, version = f.FileVersion, channel = request.Channel.ToString(), reference = envio.Reference, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await PagaduriasDeRecaudo.DtoAsync(db, f, conLineas: false, ct));
    }
}

/// <summary>Una fila de la respuesta capturada a mano (FR-196). (nuevo)</summary>
public sealed record FilaDeRespuestaCapturada(decimal Deducted, string? ProductNumber = null, string? PayerEmployeeId = null, string? PaymasterConceptCode = null,
    DateOnly? DeductionDate = null, string? ResultCode = null);

/// <summary>
/// <c>POST /api/receipts/payroll-files/{id}/responses</c> (<c>Receipts.Paymasters.LoadResponse</c>, con clave, multipart o captura; FR-077): carga una versión de la
/// respuesta de la pagaduría sobre un archivo enviado. Con archivo, lo lee con <see cref="FormatPublicId"/> (ámbito 6) o con <c>RESP-PAGADURIA-GEN</c>; sin archivo, toma
/// <see cref="Captured"/>. Casa cada fila: v1 por el número del crédito, v2 por empleado y código de la pagaduría; una fila sin equivalencia queda «no aplicada» y marcada;
/// una línea del archivo sin fila, «no aplicada» con cero. El archivo se guarda como adjunto no borrable <c>ReceiptsPaymasterResponse</c> antes de su fila. Queda
/// <c>Previewed</c>: devolverla a Cartera es otro paso. (nuevo)
/// </summary>
public sealed record LoadPaymasterResponseCommand(Guid FilePublicId, string? FileName = null, byte[]? Content = null, Guid? FormatPublicId = null,
    IReadOnlyList<FilaDeRespuestaCapturada>? Captured = null) : IRequest<Result<PaymasterResponseDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class LoadPaymasterResponseCommandValidator : AbstractValidator<LoadPaymasterResponseCommand>
{
    public LoadPaymasterResponseCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.FileName).MaximumLength(200);
        RuleFor(x => x.Captured!.Count).LessThanOrEqualTo(10000).When(x => x.Captured is not null);
        RuleForEach(x => x.Captured).ChildRules(c =>
        {
            c.RuleFor(f => f.Deducted).GreaterThanOrEqualTo(0m);
            c.RuleFor(f => f.ProductNumber).MaximumLength(30);
            c.RuleFor(f => f.PayerEmployeeId).MaximumLength(30);
            c.RuleFor(f => f.PaymasterConceptCode).MaximumLength(20);
        }).When(x => x.Captured is not null);
    }
}

public sealed class LoadPaymasterResponseCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    AuditoriaDeRecaudo auditoria,
    ISender sender,
    IDateTimeService reloj) : IRequestHandler<LoadPaymasterResponseCommand, Result<PaymasterResponseDto>>
{
    public const string TipoDeAdjunto = AdjuntosDeModulo.RespuestaDePagaduria;

    private sealed record Fila(int Linea, string? Producto, string? Empleado, string? Concepto, decimal Descontado, DateOnly? Fecha, string? Codigo);

    public async Task<Result<PaymasterResponseDto>> Handle(LoadPaymasterResponseCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        await cerrojo.TomarSetupAsync(ct: ct);
        var f = await PagaduriasDeRecaudo.VisibleAsync(db, alcance, request.FilePublicId, seguir: true, ct);
        if (f is null) return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.NoEncontrado("El archivo de descuentos"));
        if (f.Status != PayrollDeductionFileState.Sent) return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.ArchivoSinEnviar());
        var hoy = reloj.HoyLocal;

        // 1. Lectura.
        PaymasterResponseSource origen;
        BankFileFormat? formato = null;
        var filas = new List<Fila>();
        if (request.Content is { Length: > 0 } contenido)
        {
            formato = await FormatoAsync(request.FormatPublicId, hoy, ct);
            if (formato is null) return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.FormatoDeRespuestaInexistente());
            origen = formato.Code == FormatosGenericosDeRecaudoYTesoreria.RespuestaDePagaduria ? PaymasterResponseSource.GenericTemplate : PaymasterResponseSource.Format;
            var leido = FlatFileReader.Leer(formato, contenido);
            if (!leido.IsValid)
                return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.RespuestaIlegible(
                    leido.Errors.Select(e => (object)new { line = e.LineNumber, field = e.Field, message = e.Message }).ToList()));
            filas.AddRange(leido.Rows.Select(r => new Fila(r.LineNumber, r.Texto(BankFieldSource.LoanNumber)?.Trim(), r.Texto(BankFieldSource.PayerEmployeeId)?.Trim(),
                r.Texto(BankFieldSource.PaymasterConceptCode)?.Trim(), r.Monto(BankFieldSource.DeductedAmount) ?? 0m, r.Fecha(BankFieldSource.DeductionDate),
                r.Texto(BankFieldSource.ResultCode)?.Trim())));
        }
        else if (request.Captured is { Count: > 0 } capturadas)
        {
            origen = PaymasterResponseSource.Manual;
            var n = 0;
            filas.AddRange(capturadas.Select(c => new Fila(++n, c.ProductNumber?.Trim(), c.PayerEmployeeId?.Trim(), c.PaymasterConceptCode?.Trim(), c.Deducted, c.DeductionDate,
                c.ResultCode?.Trim())));
        }
        else
        {
            return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.RespuestaVacia());
        }
        if (filas.Count == 0) return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.RespuestaVacia());

        // 2. Casar con las líneas del archivo: v1 por crédito, v2 por empleado y código; lo que la pagaduría no nombra, no aplicado.
        var lineas = await db.ReceiptsPayrollDeductionFileLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == f.Id).OrderBy(l => l.Id).ToListAsync(ct);
        var fechaPorDefecto = new DateOnly(f.PeriodYear, f.PeriodMonth, DateTime.DaysInMonth(f.PeriodYear, f.PeriodMonth));
        var resultado = new List<PaymasterResponseLine>();
        var nombradas = new HashSet<long>();
        var porConcepto = f.MessageVersion >= 2;
        foreach (var fila in filas)
        {
            var candidatas = porConcepto && !string.IsNullOrEmpty(fila.Concepto)
                ? lineas.Where(l => PagaduriasDeRecaudo.Clave(l.PayerEmployeeId) == PagaduriasDeRecaudo.Clave(fila.Empleado)
                                    && PagaduriasDeRecaudo.Clave(l.PaymasterConceptCode) == PagaduriasDeRecaudo.Clave(fila.Concepto)).ToList()
                : lineas.Where(l => PagaduriasDeRecaudo.Clave(l.ProductNumber) == PagaduriasDeRecaudo.Clave(fila.Producto) && !nombradas.Contains(l.Id)).Take(1).ToList();
            if (candidatas.Count == 0)
            {
                resultado.Add(new PaymasterResponseLine
                {
                    LineNumber = fila.Linea, PayerEmployeeId = fila.Empleado, PaymasterConceptCode = fila.Concepto, ProductNumber = fila.Producto, Expected = 0m,
                    Deducted = fila.Descontado, DeductionDate = fila.Fecha, Outcome = PaymasterDeductionOutcome.NotApplied, PaymasterResultCode = fila.Codigo,
                    Reason = "Sin equivalencia en el archivo enviado.", Unmatched = true,
                });
                continue;
            }
            // Lo descontado se reparte en el orden del archivo (el de prelación que trajo Cartera): cada línea hasta su valor; el resto, a la última.
            var resto = fila.Descontado;
            for (var i = 0; i < candidatas.Count; i++)
            {
                var l = candidatas[i];
                nombradas.Add(l.Id);
                var toca = Math.Min(resto, l.Amount);
                resto -= toca;
                resultado.Add(new PaymasterResponseLine
                {
                    LineNumber = fila.Linea, DeductionFileLinePublicId = l.DeductionFileLinePublicId, PayerEmployeeId = l.PayerEmployeeId, PaymasterConceptCode = l.PaymasterConceptCode,
                    ProductNumber = l.ProductNumber, Expected = l.Amount, Deducted = toca, DeductionDate = toca > 0m ? fila.Fecha ?? fechaPorDefecto : null,
                    Outcome = toca >= l.Amount ? PaymasterDeductionOutcome.Deducted : toca <= 0m ? PaymasterDeductionOutcome.NotApplied : PaymasterDeductionOutcome.PartiallyDeducted,
                    PaymasterResultCode = fila.Codigo,
                });
            }
            if (resto > 0m)
                resultado.Add(new PaymasterResponseLine
                {
                    LineNumber = fila.Linea, PayerEmployeeId = fila.Empleado, PaymasterConceptCode = fila.Concepto, ProductNumber = fila.Producto, Expected = 0m, Deducted = resto,
                    DeductionDate = fila.Fecha, Outcome = PaymasterDeductionOutcome.NotApplied, PaymasterResultCode = fila.Codigo,
                    Reason = "Descontado de más sobre lo pedido.", Unmatched = true,
                });
        }
        var siguiente = filas.Max(x => x.Linea);
        foreach (var l in lineas.Where(l => !nombradas.Contains(l.Id)))
            resultado.Add(new PaymasterResponseLine
            {
                LineNumber = ++siguiente, DeductionFileLinePublicId = l.DeductionFileLinePublicId, PayerEmployeeId = l.PayerEmployeeId, PaymasterConceptCode = l.PaymasterConceptCode,
                ProductNumber = l.ProductNumber, Expected = l.Amount, Deducted = 0m, Outcome = PaymasterDeductionOutcome.NotApplied, Reason = "La pagaduría no la informó.",
            });

        // 3. Guardar: el adjunto antes de la fila.
        var version = (await db.PaymasterResponses.Where(r => r.PayrollDeductionFileId == f.Id).MaxAsync(r => (int?)r.Version, ct) ?? 0) + 1;
        var respuesta = new PaymasterResponse
        {
            PayrollDeductionFileId = f.Id, Version = version, Source = origen, BankFileFormatId = formato?.Id, FileName = request.FileName?.Trim(), LoadedByUserId = usuario,
            LoadedAt = reloj.UtcNow, LineCount = resultado.Count, Expected = resultado.Sum(l => l.Expected), Deducted = resultado.Sum(l => l.Deducted),
            Unmatched = resultado.Count(l => l.Unmatched),
        };
        if (request.Content is { Length: > 0 } bytes)
        {
            var subida = await sender.Send(new UploadAttachmentCommand(TipoDeAdjunto, respuesta.PublicId, respuesta.FileName ?? "respuesta-pagaduria.csv",
                formato?.ContentType ?? "text/csv", bytes), ct);
            if (subida.IsFailure) return Result.Failure<PaymasterResponseDto>(subida.Error);
            respuesta.ResponseAttachmentPublicId = subida.Value;
            respuesta.ResponseSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        }
        db.PaymasterResponses.Add(respuesta);
        await db.SaveChangesAsync(ct);
        foreach (var l in resultado) l.PaymasterResponseId = respuesta.Id;
        db.PaymasterResponseLines.AddRange(resultado);
        await auditoria.AgregarAsync("Receipts.PaymasterResponse.Loaded", "PaymasterResponse", respuesta.PublicId, null,
            new { file = f.FileNumber, version, source = origen.ToString(), lines = respuesta.LineCount, expected = respuesta.Expected, deducted = respuesta.Deducted,
                unmatched = respuesta.Unmatched, sha256 = respuesta.ResponseSha256, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await PagaduriasDeRecaudo.DtoAsync(db, respuesta, f.PublicId, ct));
    }

    /// <summary>El formato pedido del ámbito 6 o <c>RESP-PAGADURIA-GEN</c>.</summary>
    private async Task<BankFileFormat?> FormatoAsync(Guid? pedido, DateOnly hoy, CancellationToken ct)
    {
        var candidatos = await db.BankFileFormats.AsNoTracking().Include(x => x.Fields)
            .Where(x => x.Scope == BankFileScope.PayrollDeductionResult && x.IsActive && x.ValidFrom <= hoy && (x.ValidTo == null || x.ValidTo >= hoy)).ToListAsync(ct);
        return pedido is { } id
            ? candidatos.FirstOrDefault(x => x.PublicId == id)
            : candidatos.FirstOrDefault(x => x.BankId == null && x.Code == FormatosGenericosDeRecaudoYTesoreria.RespuestaDePagaduria);
    }
}

/// <summary>
/// <c>POST /api/receipts/payroll-files/{id}/responses/{rid}/emit</c> (<c>Receipts.Paymasters.LoadResponse</c>, con clave; FR-077, FR-201): devuelve la respuesta revisada
/// a Cartera como <c>ResultadoDeDescuentos</c> v1 (archivo v1: línea por línea) o v2 (archivo v2: por empleado y código de la pagaduría), por <c>EmisorDeMensajes</c> a
/// nombre de la persona, en la misma transacción. Exige que Recaudo tenga adoptada la versión del archivo (<c>Receipts.Paymaster.VersionNotAdopted</c>). La evaluación de
/// Cartera se lee después (<see cref="RefreshPaymasterResponseStatusCommand"/>). (nuevo)
/// </summary>
public sealed record EmitPaymasterResultCommand(Guid FilePublicId, Guid ResponsePublicId) : IRequest<Result<PaymasterResponseDto>>, IOperacionIdempotente, IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class EmitPaymasterResultCommandValidator : AbstractValidator<EmitPaymasterResultCommand>
{
    public EmitPaymasterResultCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.ResponsePublicId).NotEmpty();
    }
}

public sealed class EmitPaymasterResultCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeRecaudo alcance,
    IActorActual actorActual,
    ICerrojoDeRecaudo cerrojo,
    IRegistroDeIntegracion registro,
    EmisionDeRecaudo emision,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRequestHandler<EmitPaymasterResultCommand, Result<PaymasterResponseDto>>
{
    public const string ClaseDeRespuesta = "PayrollDeductionResponse";

    public async Task<Result<PaymasterResponseDto>> Handle(EmitPaymasterResultCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is null) return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        await cerrojo.TomarSetupAsync(ct: ct);
        var f = await PagaduriasDeRecaudo.VisibleAsync(db, alcance, request.FilePublicId, seguir: true, ct);
        var r = f is null ? null : await db.PaymasterResponses.FirstOrDefaultAsync(x => x.PublicId == request.ResponsePublicId && x.PayrollDeductionFileId == f.Id, ct);
        if (f is null || r is null) return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.NoEncontrado("La respuesta de la pagaduría"));
        if (r.Status != PaymasterResponseState.Previewed) return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.RespuestaNoEmitible(r.Status.ToString()));
        if (f.Status != PayrollDeductionFileState.Sent) return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.ArchivoSinEnviar());
        var hoy = reloj.HoyLocal;
        var destino = await registro.DestinoAsync(IntegrationDestinations.Receipts, hoy, ct);
        if (destino is null || !destino.Adopciones.Any(a => a.Contrato == DescuentosPorPagaduriaV1.Type && a.Version == f.MessageVersion))
            return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.VersionNoAdoptada(f.MessageVersion));

        var lineas = await db.PaymasterResponseLines.AsNoTracking().Where(l => l.PaymasterResponseId == r.Id).OrderBy(l => l.LineNumber).ToListAsync(ct);
        var deArchivo = await db.ReceiptsPayrollDeductionFileLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == f.Id)
            .ToDictionaryAsync(l => l.DeductionFileLinePublicId, ct);
        var pagaduria = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.Id == f.PaymasterPersonId).Select(p => p.PublicId).FirstAsync(ct);
        var periodo = new PayrollPeriodRefV1 { Year = f.PeriodYear, Month = f.PeriodMonth, SubPeriod = f.SubPeriod };
        var adjunto = r.ResponseAttachmentPublicId is { } a ? new DeductionResponseFileV1 { AttachmentPublicId = a, Sha256 = r.ResponseSha256 ?? string.Empty } : null;
        var usuario = new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name };
        var porLinea = lineas.Where(l => l.DeductionFileLinePublicId is { } id && deArchivo.ContainsKey(id) && deArchivo[id].Kind == PayrollDeductionLineKind.Loan)
            .Select(l => Resultado(l, deArchivo[l.DeductionFileLinePublicId!.Value])).ToList();
        var totales = new DeductionResultTotalsV1
        {
            LineCount = lineas.Count, Expected = lineas.Sum(l => l.Expected), Deducted = lineas.Sum(l => l.Deducted),
            NotApplied = lineas.Sum(l => Math.Max(0m, l.Expected - l.Deducted)),
        };
        object contenido = f.MessageVersion >= 2
            ? new ResultadoDeDescuentosV2
            {
                FileMessageId = f.FileMessagePublicId, FilePublicId = f.FilePublicId, FileVersion = f.FileVersion, FileSha256 = f.FileSha256, FileGroupCode = f.FileGroupCode ?? string.Empty,
                PaymasterPersonPublicId = pagaduria, Period = periodo, ResponseFile = adjunto, Totals = totales, ProcessedBy = usuario,
                ConceptLines = lineas.Where(l => !string.IsNullOrEmpty(l.PaymasterConceptCode))
                    .GroupBy(l => (PagaduriasDeRecaudo.Clave(l.PayerEmployeeId), PagaduriasDeRecaudo.Clave(l.PaymasterConceptCode)))
                    .Select(g => new DeductionConceptLineV2
                    {
                        PayerEmployeeId = g.First().PayerEmployeeId ?? string.Empty,
                        PersonPublicId = null,
                        PaymasterConceptCode = g.First().PaymasterConceptCode ?? string.Empty,
                        Expected = g.Sum(l => l.Expected),
                        Deducted = g.Sum(l => l.Deducted),
                        PaymasterResultCode = g.Select(l => l.PaymasterResultCode).FirstOrDefault(c => c is not null),
                        DeductionDate = g.Where(l => l.Deducted > 0m).Select(l => l.DeductionDate).FirstOrDefault(d => d is not null),
                    }).ToList(),
            }
            : new ResultadoDeDescuentosV1
            {
                FileMessageId = f.FileMessagePublicId, FilePublicId = f.FilePublicId, FileVersion = f.FileVersion, FileSha256 = f.FileSha256, PaymasterPersonPublicId = pagaduria,
                Period = periodo, ResponseFile = adjunto, Lines = porLinea, Totals = totales, ProcessedBy = usuario,
            };
        var mensaje = await emision.MensajeDeLibranzaAsync(f, r.PublicId, ClaseDeRespuesta, r.FileName ?? f.FileNumber, contenido, hoy, ct);
        r.Status = PaymasterResponseState.Emitted;
        r.EmittedMessagePublicId = mensaje.PublicId;
        r.EmittedMessageVersion = f.MessageVersion;
        await auditoria.AgregarAsync("Receipts.PaymasterResponse.Emitted", "PaymasterResponse", r.PublicId, null,
            new { file = f.FileNumber, version = r.Version, messageVersion = f.MessageVersion, deducted = r.Deducted, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await PagaduriasDeRecaudo.DtoAsync(db, r, f.PublicId, ct));
    }

    private static DeductionResultLineV1 Resultado(PaymasterResponseLine l, PayrollDeductionFileLine linea) => new()
    {
        DeductionFileLinePublicId = linea.DeductionFileLinePublicId,
        Loan = new LoanRefV1 { LoanPublicId = linea.ProductPublicId, LoanNumber = linea.ProductNumber },
        InstallmentNumber = linea.InstallmentNumber,
        ExpectedAmount = l.Expected,
        DeductedAmount = l.Deducted,
        DeductionDate = l.DeductionDate,
        Outcome = l.Outcome.ToString(),
        PaymasterResultCode = l.PaymasterResultCode,
        Reason = l.Outcome == PaymasterDeductionOutcome.Deducted ? null : nameof(MotivoDeNoDescuento.Other),
        Detail = l.Reason,
    };
}

/// <summary>
/// <c>POST /api/receipts/payroll-files/{id}/responses/{rid}/refresh</c> (<c>Receipts.Paymasters.View</c>, con clave; FR-201): lee la evaluación de Cartera del
/// <c>ResultadoDeDescuentos</c> emitido. Aceptado: la respuesta queda <c>AcceptedByLending</c>, el resultado de cada línea se escribe una sola vez en la línea del archivo y
/// el archivo queda <c>Responded</c> (listo para el giro). Rechazado (por ejemplo <c>Lending.PayrollFile.LineMismatch</c>): <c>RejectedByLending</c> con el código y los
/// datos; se corrige cargando otra versión. Pendiente: sin cambio. (nuevo)
/// </summary>
public sealed record RefreshPaymasterResponseStatusCommand(Guid FilePublicId, Guid ResponsePublicId) : IRequest<Result<PaymasterResponseDto>>, IOperacionIdempotente,
    IEscrituraDeRecaudo
{
    public Guid OperationKey { get; init; }
}

public sealed class RefreshPaymasterResponseStatusCommandHandler(IApplicationDbContext db, IAlcanceDeRecaudo alcance, ICerrojoDeRecaudo cerrojo, AuditoriaDeRecaudo auditoria)
    : IRequestHandler<RefreshPaymasterResponseStatusCommand, Result<PaymasterResponseDto>>
{
    public async Task<Result<PaymasterResponseDto>> Handle(RefreshPaymasterResponseStatusCommand request, CancellationToken ct)
    {
        await cerrojo.TomarSetupAsync(ct: ct);
        var f = await PagaduriasDeRecaudo.VisibleAsync(db, alcance, request.FilePublicId, seguir: true, ct);
        var r = f is null ? null : await db.PaymasterResponses.FirstOrDefaultAsync(x => x.PublicId == request.ResponsePublicId && x.PayrollDeductionFileId == f.Id, ct);
        if (f is null || r is null) return Result.Failure<PaymasterResponseDto>(ErroresDeRecaudo.NoEncontrado("La respuesta de la pagaduría"));
        if (r.Status == PaymasterResponseState.Emitted) await EvaluacionDeCarteraDeLaRespuesta.LeerAsync(db, auditoria, f, r, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await PagaduriasDeRecaudo.DtoAsync(db, r, f.PublicId, ct));
    }
}

/// <summary>La lectura de la evaluación de Cartera de una respuesta emitida, por la entrega del mensaje a Cartera. Sin guardar. (nuevo)</summary>
public static class EvaluacionDeCarteraDeLaRespuesta
{
    public static async Task LeerAsync(IApplicationDbContext db, AuditoriaDeRecaudo auditoria, PayrollDeductionFile f, PaymasterResponse r, CancellationToken ct)
    {
        if (r.EmittedMessagePublicId is not { } mensaje) return;
        var entrega = await (from m in db.IntegrationMessages.AsNoTracking()
                             join d in db.IntegrationMessageDeliveries.AsNoTracking() on m.Id equals d.MessageId
                             where m.PublicId == mensaje && d.Destination == IntegrationDestinations.Lending
                             select new { d.Status, d.LastErrorCode, d.LastErrorDataJson }).FirstOrDefaultAsync(ct);
        if (entrega is null) return;
        if (entrega.Status == DeliveryStatus.Processed)
        {
            r.Status = PaymasterResponseState.AcceptedByLending;
            var lineas = await db.PaymasterResponseLines.AsNoTracking().Where(l => l.PaymasterResponseId == r.Id && l.DeductionFileLinePublicId != null).ToListAsync(ct);
            var porLinea = lineas.GroupBy(l => l.DeductionFileLinePublicId!.Value)
                .ToDictionary(g => g.Key, g => (Deducted: g.Sum(l => l.Deducted), Fecha: g.Select(l => l.DeductionDate).FirstOrDefault(d => d is not null)));
            var delArchivo = await db.ReceiptsPayrollDeductionFileLines.Where(l => l.PayrollDeductionFileId == f.Id).ToListAsync(ct);
            foreach (var l in delArchivo.Where(l => l.DeductionOutcome is null))
            {
                var (descontado, fecha) = porLinea.GetValueOrDefault(l.DeductionFileLinePublicId);
                l.DeductedAmount = Math.Min(descontado, l.Amount);
                l.DeductionDate = descontado > 0m ? fecha : null;
                l.DeductionOutcome = descontado >= l.Amount ? PaymasterDeductionOutcome.Deducted
                    : descontado <= 0m ? PaymasterDeductionOutcome.NotApplied : PaymasterDeductionOutcome.PartiallyDeducted;
            }
            f.Status = PayrollDeductionFileState.Responded;
            await auditoria.AgregarAsync("Receipts.PaymasterResponse.AcceptedByLending", "PaymasterResponse", r.PublicId, null, new { file = f.FileNumber, version = r.Version }, ct);
        }
        else if (entrega.Status is DeliveryStatus.Rejected or DeliveryStatus.ValidationFailed && entrega.LastErrorCode is not null)
        {
            r.Status = PaymasterResponseState.RejectedByLending;
            r.RejectionCode = entrega.LastErrorCode.Length <= 100 ? entrega.LastErrorCode : entrega.LastErrorCode[..100];
            r.RejectionDataJson = entrega.LastErrorDataJson;
            await auditoria.AgregarAsync("Receipts.PaymasterResponse.RejectedByLending", "PaymasterResponse", r.PublicId, null,
                new { file = f.FileNumber, version = r.Version, code = r.RejectionCode }, ct);
        }
    }
}
