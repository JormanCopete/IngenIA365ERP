using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.Dispersion;

/// <summary>
/// Lo que Generar y Vista previa comparten (feature 010, US8): elegir el formato vigente del
/// banco pagador para la fecha de pago (o el indicado), resolver la cuenta origen y armar las
/// líneas desde la relación de pago.
/// </summary>
public sealed class PreparacionDeDispersion(IApplicationDbContext db, PayrollDisbursementLines lineas, IDateTimeService clock)
{
    public sealed record Preparado(PayrollRun Run, BankFileFormat Formato, InsumosDeDispersion Insumos, int? SourceAccountId, string? SourceAccountNumber, Bank? BancoPagador);

    public async Task<Result<Preparado>> PrepararAsync(Guid runPublicId, Guid? formatPublicId, DateOnly paymentDate, Guid? sourceAccountPublicId,
        string? reference, IReadOnlyList<Guid>? employeePublicIds, bool exigirAprobada, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == runPublicId, ct);
        if (run is null) return Result.Failure<Preparado>(DisbursementErrors.RunNotFound);
        if (exigirAprobada && run.Status != PayrollRunStatus.Approved) return Result.Failure<Preparado>(DisbursementErrors.RunNotApproved);

        // --- cuenta origen: una cuenta bancaria del plan (Contabilidad), con su banco ---
        Domain.Entities.Accounting.ChartOfAccount? cuenta = null;
        if (sourceAccountPublicId is { } cuentaId)
        {
            cuenta = await db.ChartOfAccounts.AsNoTracking().Include(c => c.Bank).FirstOrDefaultAsync(c => c.PublicId == cuentaId, ct);
            if (cuenta is null || !cuenta.EsBancaria) return Result.Failure<Preparado>(DisbursementErrors.SourceAccountNotFound);
        }

        // --- formato: el indicado, o el vigente del banco de la cuenta, o el genérico ---
        BankFileFormat? formato;
        if (formatPublicId is { } fid)
        {
            formato = await db.BankFileFormats.AsNoTracking().Include(f => f.Bank).Include(f => f.Fields).FirstOrDefaultAsync(f => f.PublicId == fid, ct);
            if (formato is null) return Result.Failure<Preparado>(DisbursementErrors.FormatNotFound);
            if (formato.Scope != BankFileScope.PayrollDisbursement) return Result.Failure<Preparado>(DisbursementErrors.FormatScopeMismatch);
            if (!formato.VigenteEn(paymentDate)) return Result.Failure<Preparado>(DisbursementErrors.FormatNotCurrent(formato.Code, formato.ValidFrom, formato.ValidTo));
        }
        else
        {
            var candidatos = await db.BankFileFormats.AsNoTracking().Include(f => f.Bank).Include(f => f.Fields)
                .Where(f => f.Scope == BankFileScope.PayrollDisbursement && f.IsActive && f.ValidFrom <= paymentDate && (f.ValidTo == null || f.ValidTo >= paymentDate))
                .ToListAsync(ct);
            var bancoId = cuenta?.BankId;
            formato = candidatos.Where(f => bancoId != null && f.BankId == bancoId).OrderByDescending(f => f.ValidFrom).FirstOrDefault()
                      ?? candidatos.Where(f => f.BankId == null).OrderByDescending(f => f.ValidFrom).FirstOrDefault();
            if (formato is null) return Result.Failure<Preparado>(DisbursementErrors.NoFormat(cuenta?.Bank?.Name));
        }

        if (cuenta is not null && formato.BankId is { } bancoDelFormato && cuenta.BankId != bancoDelFormato)
            return Result.Failure<Preparado>(DisbursementErrors.SourceAccountBankMismatch(cuenta.BankAccountNumber ?? cuenta.Code, cuenta.Bank?.Name ?? "otro banco", formato.Bank?.Name ?? formato.Code));
        var usaCuentaOrigen = formato.Fields.Any(f => !f.IsDeleted && f.Source is BankFieldSource.SourceAccountNumber or BankFieldSource.SourceAccountType);
        if (usaCuentaOrigen && cuenta is null) return Result.Failure<Preparado>(DisbursementErrors.SourceAccountRequired);

        var bancoPagador = cuenta?.Bank ?? formato.Bank;
        var generado = clock.UtcNow;
        var hoy = DateOnly.FromDateTime(generado);
        var secuencia = await db.BankDisbursementFiles.AsNoTracking().CountAsync(a => a.GeneratedAt >= hoy.ToDateTime(TimeOnly.MinValue) && a.GeneratedAt < hoy.AddDays(1).ToDateTime(TimeOnly.MinValue), ct) + 1;

        var insumos = await lineas.CargarAsync(run, formato, paymentDate, generado, secuencia, reference, employeePublicIds,
            cuenta?.Id, cuenta?.BankAccountNumber, cuenta is null ? null : "1", bancoPagador?.TransferCode, ct);
        if (insumos.IsFailure) return Result.Failure<Preparado>(insumos.Error);
        return Result.Success(new Preparado(run, formato, insumos.Value, cuenta?.Id, cuenta?.BankAccountNumber, bancoPagador));
    }

    public static Result<BankFileOutput> Escribir(Preparado p, IReadOnlyList<CandidatoDeDispersion> incluidos)
    {
        try
        {
            return Result.Success(FlatFileWriter.Escribir(p.Formato, p.Insumos.Contexto, incluidos.Select(c => c.Valores).ToList()));
        }
        catch (BankFileWriteException ex)
        {
            var quien = ex.LineNumber >= 1 && ex.LineNumber <= incluidos.Count ? incluidos[ex.LineNumber - 1].Nombre : "?";
            return Result.Failure<BankFileOutput>(DisbursementErrors.LineTooLong(ex.LineNumber, ex.Field, quien));
        }
    }
}

// ----------------------------------------------------------------- generar --

/// <summary>Genera el archivo de dispersión de una corrida aprobada (FR-031..FR-033) y lo deja en <c>COR_Attachments</c>.</summary>
public sealed record GenerateDisbursementFileCommand(
    Guid RunPublicId, Guid? FormatPublicId, DateOnly PaymentDate, Guid? SourceAccountPublicId = null, string? Reference = null, IReadOnlyList<Guid>? EmployeePublicIds = null)
    : IRequest<Result<DisbursementGeneratedDto>>;

public sealed class GenerateDisbursementFileCommandValidator : AbstractValidator<GenerateDisbursementFileCommand>
{
    public GenerateDisbursementFileCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.PaymentDate).NotEqual(default(DateOnly)).WithMessage("La fecha de pago es obligatoria.");
        RuleFor(x => x.Reference).MaximumLength(60);
    }
}

public sealed class GenerateDisbursementFileCommandHandler(
    IApplicationDbContext db, PreparacionDeDispersion preparacion, ISender sender, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<GenerateDisbursementFileCommand, Result<DisbursementGeneratedDto>>
{
    public const string OwnerEntityType = nameof(BankDisbursementFile);

    public async Task<Result<DisbursementGeneratedDto>> Handle(GenerateDisbursementFileCommand request, CancellationToken ct)
    {
        var prep = await preparacion.PrepararAsync(request.RunPublicId, request.FormatPublicId, request.PaymentDate, request.SourceAccountPublicId, request.Reference, request.EmployeePublicIds, exigirAprobada: true, ct);
        if (prep.IsFailure) return Result.Failure<DisbursementGeneratedDto>(prep.Error);
        var p = prep.Value;

        var incluidos = p.Insumos.Candidatos.Where(c => c.Excluido is null).ToList();
        var excluidos = p.Insumos.Candidatos.Where(c => c.Excluido is not null).Select(c => c.Excluido!).ToList();
        if (incluidos.Count == 0) return Result.Failure<DisbursementGeneratedDto>(DisbursementErrors.NothingToPay);

        var salida = PreparacionDeDispersion.Escribir(p, incluidos);
        if (salida.IsFailure) return Result.Failure<DisbursementGeneratedDto>(salida.Error);
        var archivo = salida.Value;

        var entidad = new BankDisbursementFile
        {
            PayrollRunId = p.Run.Id,
            BankId = p.BancoPagador?.Id,
            FormatId = p.Formato.Id,
            FormatCode = p.Formato.Code,
            Status = BankDisbursementFileStatus.Generated,
            GeneratedAt = p.Insumos.Contexto.GeneratedAt,
            GeneratedBy = user.UserName ?? "sistema",
            PaymentDate = request.PaymentDate,
            Sequence = p.Insumos.Contexto.Sequence,
            Reference = p.Insumos.Contexto.BatchReference,
            SourceAccountId = p.SourceAccountId,
            SourceAccountNumber = p.SourceAccountNumber,
            LineCount = archivo.LineCount,
            TotalAmount = archivo.TotalAmount,
            ExcludedCount = excluidos.Count,
            ExcludedJson = excluidos.Count == 0 ? null : JsonSerializer.Serialize(excluidos, JsonWeb),
            FileName = archivo.FileName,
            FileSha256 = archivo.Sha256,
            CreatedAt = clock.UtcNow,
            CreatedBy = user.UserName,
        };
        for (var i = 0; i < incluidos.Count; i++)
        {
            var c = incluidos[i];
            entidad.Lines.Add(new BankDisbursementFileLine
            {
                LineNumber = i + 1, PayrollRunEmployeeId = c.RunEmployeeId, EmployeeId = c.EmployeeId, DestinationBankId = c.BankId,
                AccountType = c.AccountType, AccountNumber = c.AccountNumber, Amount = c.NetPay, RecordText = archivo.DetailTexts[i],
                CreatedAt = entidad.CreatedAt, CreatedBy = user.UserName,
            });
        }

        // El archivo se guarda en COR_Attachments (cifrado, con SHA-256) ANTES de la fila: si el
        // almacén falla, no queda un archivo «generado» que nadie puede descargar.
        var subida = await sender.Send(new UploadAttachmentCommand(OwnerEntityType, entidad.PublicId, archivo.FileName, archivo.ContentType, archivo.Content), ct);
        if (subida.IsFailure) return Result.Failure<DisbursementGeneratedDto>(subida.Error);
        entidad.FileAttachmentPublicId = subida.Value;

        db.BankDisbursementFiles.Add(entidad);
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollDispersionGenerated, OwnerEntityType, entidad.PublicId, null, new
        {
            runPublicId = p.Run.PublicId, kind = p.Run.Kind.ToString(), format = p.Formato.Code, bank = p.BancoPagador?.Name,
            paymentDate = request.PaymentDate, fileName = archivo.FileName, sha256 = archivo.Sha256, lines = archivo.LineCount,
            total = archivo.TotalAmount, excluded = excluidos.Select(e => new { e.EmployeePublicId, e.ReasonCode }).ToList(),
        }, ct);

        var avisos = new List<WarningDto>();
        if (excluidos.Count > 0)
            avisos.Add(new WarningDto("Payroll.Disbursement.Excluded", $"{excluidos.Count} empleado(s) quedaron en pendientes: revise los motivos y márquelos a mano o corrija la ficha.", new { count = excluidos.Count }));
        return Result.Success(new DisbursementGeneratedDto(entidad.PublicId, archivo.FileName, archivo.LineCount, archivo.TotalAmount, excluidos, avisos));
    }

    internal static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);
}

// ------------------------------------------------------------- vista previa --

/// <summary>Escribe las primeras líneas de una corrida real con un formato (guardado o propuesto) sin persistir nada.</summary>
public sealed record PreviewDisbursementFileCommand(Guid RunPublicId, Guid? FormatPublicId, DateOnly PaymentDate, Guid? SourceAccountPublicId = null, string? Reference = null, BankFileFormatDefinition? Definition = null)
    : IRequest<Result<DisbursementPreviewDto>>;

public sealed class PreviewDisbursementFileCommandValidator : AbstractValidator<PreviewDisbursementFileCommand>
{
    public PreviewDisbursementFileCommandValidator()
    {
        RuleFor(x => x.RunPublicId).NotEmpty();
        RuleFor(x => x.PaymentDate).NotEqual(default(DateOnly));
        RuleFor(x => x).Must(x => x.FormatPublicId is not null || x.Definition is not null).WithMessage("Indique el formato guardado o la definición a probar.");
    }
}

public sealed class PreviewDisbursementFileCommandHandler(IApplicationDbContext db, PayrollDisbursementLines lineas, IDateTimeService clock)
    : IRequestHandler<PreviewDisbursementFileCommand, Result<DisbursementPreviewDto>>
{
    public const int LineasDeMuestra = 5;

    public async Task<Result<DisbursementPreviewDto>> Handle(PreviewDisbursementFileCommand request, CancellationToken ct)
    {
        var run = await db.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.PublicId == request.RunPublicId, ct);
        if (run is null) return Result.Failure<DisbursementPreviewDto>(DisbursementErrors.RunNotFound);

        BankFileFormat formato;
        if (request.Definition is { } def)
        {
            var errores = BankFileFormatValidator.Validar(def);
            if (errores.Count > 0) return Result.Success(new DisbursementPreviewDto(string.Empty, "text/plain", [], 0, 0m, [], errores));
            formato = new BankFileFormat();
            def.AplicarA(formato, null);
        }
        else
        {
            var guardado = await db.BankFileFormats.AsNoTracking().Include(f => f.Bank).Include(f => f.Fields).FirstOrDefaultAsync(f => f.PublicId == request.FormatPublicId, ct);
            if (guardado is null) return Result.Failure<DisbursementPreviewDto>(DisbursementErrors.FormatNotFound);
            formato = guardado;
        }

        Domain.Entities.Accounting.ChartOfAccount? cuenta = null;
        if (request.SourceAccountPublicId is { } cuentaId)
            cuenta = await db.ChartOfAccounts.AsNoTracking().Include(c => c.Bank).FirstOrDefaultAsync(c => c.PublicId == cuentaId && c.BankId != null, ct);

        var insumos = await lineas.CargarAsync(run, formato, request.PaymentDate, clock.UtcNow, 1, request.Reference, null,
            cuenta?.Id, cuenta?.BankAccountNumber, cuenta is null ? null : "1", cuenta?.Bank?.TransferCode ?? formato.Bank?.TransferCode, ct);
        if (insumos.IsFailure) return Result.Failure<DisbursementPreviewDto>(insumos.Error);

        var incluidos = insumos.Value.Candidatos.Where(c => c.Excluido is null).Take(LineasDeMuestra).ToList();
        var excluidos = insumos.Value.Candidatos.Where(c => c.Excluido is not null).Select(c => c.Excluido!).ToList();
        try
        {
            var salida = FlatFileWriter.Escribir(formato, insumos.Value.Contexto, incluidos.Select(c => c.Valores).ToList());
            var textoLineas = salida.Text.Split(formato.LineEnding == BankFileLineEnding.Lf ? "\n" : "\r\n", StringSplitOptions.RemoveEmptyEntries).ToList();
            return Result.Success(new DisbursementPreviewDto(salida.FileName, salida.ContentType, textoLineas, salida.LineCount, salida.TotalAmount, excluidos, []));
        }
        catch (BankFileWriteException ex)
        {
            var quien = ex.LineNumber >= 1 && ex.LineNumber <= incluidos.Count ? incluidos[ex.LineNumber - 1].Nombre : "?";
            return Result.Failure<DisbursementPreviewDto>(DisbursementErrors.LineTooLong(ex.LineNumber, ex.Field, quien));
        }
    }
}

// ------------------------------------------------------------ marcar enviado --

/// <summary>
/// El archivo se entregó al banco: todos los de sus líneas quedan pagados en una sola acción
/// (medio <c>Transfer</c>, fecha de pago, referencia del banco), en la <b>misma transacción</b>
/// que el cambio de estado (FR-033). Quien ya tenía marca vigente se deja como estaba y se avisa.
/// </summary>
public sealed record MarkDisbursementSentCommand(Guid FilePublicId, DateTime SentAt, string? BankReference, DateTime? PaidAt = null, string? Notes = null)
    : IRequest<Result<DisbursementSentDto>>;

public sealed class MarkDisbursementSentCommandValidator : AbstractValidator<MarkDisbursementSentCommand>
{
    public MarkDisbursementSentCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.SentAt).NotEmpty().WithMessage("La fecha de envío es obligatoria.");
        RuleFor(x => x.BankReference).MaximumLength(60);
        RuleFor(x => x.Notes).MaximumLength(300);
    }
}

public sealed class MarkDisbursementSentCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<MarkDisbursementSentCommand, Result<DisbursementSentDto>>
{
    public async Task<Result<DisbursementSentDto>> Handle(MarkDisbursementSentCommand request, CancellationToken ct)
    {
        var archivo = await db.BankDisbursementFiles.Include(a => a.Lines).Include(a => a.PayrollRun).FirstOrDefaultAsync(a => a.PublicId == request.FilePublicId, ct);
        if (archivo is null) return Result.Failure<DisbursementSentDto>(DisbursementErrors.FileNotFound);
        if (archivo.Status != BankDisbursementFileStatus.Generated) return Result.Failure<DisbursementSentDto>(DisbursementErrors.NotGenerated(archivo.Status.ToString()));
        if (archivo.PayrollRun!.Status != PayrollRunStatus.Approved) return Result.Failure<DisbursementSentDto>(DisbursementErrors.RunNotApproved);

        var ids = archivo.Lines.Select(l => l.PayrollRunEmployeeId).ToList();
        var yaMarcados = await db.PayrollPayments.AsNoTracking().Where(p => ids.Contains(p.PayrollRunEmployeeId) && !p.IsReverted)
            .Select(p => p.PayrollRunEmployeeId).ToListAsync(ct);
        var empleadosPorRunEmployee = await db.PayrollRunEmployees.AsNoTracking().Where(re => ids.Contains(re.Id))
            .Join(db.Employees.AsNoTracking(), re => re.EmployeeId, e => e.Id, (re, e) => new { re.Id, e.PublicId })
            .ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);

        var ahora = clock.UtcNow;
        var pagadoEl = request.PaidAt ?? archivo.PaymentDate.ToDateTime(TimeOnly.MinValue);
        var referencia = string.IsNullOrWhiteSpace(request.BankReference) ? (archivo.Reference ?? archivo.FileName) : request.BankReference.Trim();
        var marcados = 0;
        var omitidos = new List<Guid>();
        foreach (var linea in archivo.Lines.OrderBy(l => l.LineNumber))
        {
            if (yaMarcados.Contains(linea.PayrollRunEmployeeId)) { omitidos.Add(empleadosPorRunEmployee.GetValueOrDefault(linea.PayrollRunEmployeeId)); continue; }
            var pago = new PayrollPayment
            {
                PayrollRunEmployeeId = linea.PayrollRunEmployeeId,
                PaidAt = pagadoEl,
                PaymentMethod = PayrollPaymentMethod.Transfer,
                Reference = referencia.Length > 60 ? referencia[..60] : referencia,
                PaidBy = user.UserName ?? "sistema",
                CreatedAt = ahora,
                CreatedBy = user.UserName,
            };
            pago.BankDisbursementFileId = archivo.Id;
            db.PayrollPayments.Add(pago);
            linea.Payment = pago; // la FK la resuelve EF en el mismo SaveChanges
            marcados++;
        }

        archivo.Status = BankDisbursementFileStatus.Sent;
        archivo.SentAt = request.SentAt;
        archivo.SentBy = user.UserName ?? "sistema";
        archivo.BankReference = string.IsNullOrWhiteSpace(request.BankReference) ? null : request.BankReference.Trim();
        archivo.SentNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        archivo.UpdatedAt = ahora;
        archivo.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollDispersionSent, nameof(BankDisbursementFile), archivo.PublicId, null,
            new { runPublicId = archivo.PayrollRun.PublicId, request.SentAt, bankReference = archivo.BankReference, markedPaid = marcados, alreadyMarked = omitidos, paidAt = pagadoEl }, ct);
        await audit.EmitAsync(AuditEventTypes.PayrollPaymentsMarked, "PayrollRun", archivo.PayrollRun.PublicId, null,
            new { marked = marcados, method = PayrollPaymentMethod.Transfer.ToString(), reference = referencia, paidAt = pagadoEl, disbursementFile = archivo.PublicId }, ct);

        return Result.Success(new DisbursementSentDto(archivo.PublicId, marcados, omitidos, pagadoEl, referencia));
    }

}

// ------------------------------------------------------------------- anular --

/// <summary>Un archivo generado que no se envió se anula con motivo; nada queda pagado. Uno enviado no se anula: cada marca se retira una a una.</summary>
public sealed record CancelDisbursementFileCommand(Guid FilePublicId, string Reason) : IRequest<Result>;

public sealed class CancelDisbursementFileCommandValidator : AbstractValidator<CancelDisbursementFileCommand>
{
    public CancelDisbursementFileCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().WithMessage("El motivo es obligatorio.").MaximumLength(300);
    }
}

public sealed class CancelDisbursementFileCommandHandler(IApplicationDbContext db, IDateTimeService clock, ICurrentUserService user, PayrollAuditEmitter audit)
    : IRequestHandler<CancelDisbursementFileCommand, Result>
{
    public async Task<Result> Handle(CancelDisbursementFileCommand request, CancellationToken ct)
    {
        var archivo = await db.BankDisbursementFiles.Include(a => a.PayrollRun).FirstOrDefaultAsync(a => a.PublicId == request.FilePublicId, ct);
        if (archivo is null) return Result.Failure(DisbursementErrors.FileNotFound);
        if (archivo.Status != BankDisbursementFileStatus.Generated) return Result.Failure(DisbursementErrors.NotGenerated(archivo.Status.ToString()));

        archivo.Status = BankDisbursementFileStatus.Voided;
        archivo.VoidedAt = clock.UtcNow;
        archivo.VoidedBy = user.UserName ?? "sistema";
        archivo.VoidReason = request.Reason.Trim();
        archivo.UpdatedAt = archivo.VoidedAt;
        archivo.UpdatedBy = user.UserName;
        await db.SaveChangesAsync(ct);

        await audit.EmitAsync(AuditEventTypes.PayrollDispersionCancelled, nameof(BankDisbursementFile), archivo.PublicId, null,
            new { runPublicId = archivo.PayrollRun!.PublicId, reason = archivo.VoidReason, fileName = archivo.FileName }, ct);
        return Result.Success();
    }
}
