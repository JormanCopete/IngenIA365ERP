// Feature 015, T0307 (2026-10-26): los dobles de prueba de la 013 se retiraron de la aplicación (FR-223). Lo que las pruebas de Cartera y Captaciones
// necesitan de ellos para producir los mensajes que Cartera consume (el ResultadoDelGiro, el envío y la respuesta de la pagaduría) y como destinos con
// implementación en el registro vive aquí, como sustitutos de prueba, sin rutas ni registro en la API. Copia del código retirado, sin cambios de lógica.
using IngenIA365ERP.Application.Lending.Integration;
using FluentValidation;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Sustitutos;

// Feature 013, K2 (US10, bloque B; T0847; api.md §20; mensajes.md §12.5, §12.6, §18): lo que el recaudador de prueba hace con el archivo de
// descuentos —confirmar su envío y devolver el resultado de la pagaduría— como lo haría Recaudo, emitiendo como REC con la persona que prueba de
// usuario de origen. Sólo sirven si la guarda DoblesDePruebaDeCarteraEnDi registró RecaudadorDePrueba (fuera de producción); si no, el 404 genérico.
// (nuevo)

/// <summary>Lo que emitió el recaudador de prueba. (nuevo)</summary>
public sealed record TestPayrollMessageDto(Guid MessagePublicId, Guid FilePublicId, string Type, int Lines);

/// <summary>
/// <c>POST /api/lending/test-doubles/collector/payroll-deduction-files/{id}/confirm-sent</c> (<c>Lending.TestDoubles.Operate</c>, con clave): emite
/// <c>EnvioDeDescuentosConfirmado</c> v1 como <c>REC</c> (origen un documento nuevo de Recaudo, clave <c>Confirmation:{archivo:N}</c>, <c>related</c> =
/// el archivo, tercero la pagaduría) con la huella y el mensaje del archivo. Lo consume <see cref="DestinoCartera"/>. (nuevo)
/// </summary>
public sealed record ConfirmTestPayrollFileSentCommand(Guid FilePublicId, string Channel = "Portal", string? Reference = null)
    : IRequest<Result<TestPayrollMessageDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ConfirmTestPayrollFileSentCommandValidator : AbstractValidator<ConfirmTestPayrollFileSentCommand>
{
    public ConfirmTestPayrollFileSentCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.Channel).Must(c => c is "Email" or "Portal" or "Physical" or "Other").WithMessage("El canal es Email, Portal, Physical u Other.");
        RuleFor(x => x.Reference).MaximumLength(60);
    }
}

public sealed class ConfirmTestPayrollFileSentCommandHandler(
    IApplicationDbContext db,
    EmisorDeMensajes emisor,
    EmisionDeCartera emision,
    IngenIA365ERP.Application.Common.Interfaces.Security.IActorActual actorActual,
    IDateTimeService reloj,
    IEnumerable<IDestinoDeMensajes> destinos) : IRequestHandler<ConfirmTestPayrollFileSentCommand, Result<TestPayrollMessageDto>>
{
    public async Task<Result<TestPayrollMessageDto>> Handle(ConfirmTestPayrollFileSentCommand request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos)) return Result.Failure<TestPayrollMessageDto>(RecaudadorDePruebaRegistrado.NoExiste());
        var archivo = await db.PayrollDeductionFiles.AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == request.FilePublicId && !f.IsDeleted, ct);
        if (archivo is null) return Result.Failure<TestPayrollMessageDto>(ErroresDeLibranza.ArchivoInexistente());
        var pagaduria = await PagaduriaAsync(db, archivo, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var contenido = new EnvioDeDescuentosConfirmadoV1
        {
            FileMessageId = archivo.DeliveryMessagePublicId,
            FilePublicId = archivo.PublicId,
            FileVersion = archivo.Version,
            FileSha256 = archivo.FileSha256,
            SentAt = new DateTimeOffset(DateTime.SpecifyKind(reloj.UtcNow, DateTimeKind.Utc)),
            Channel = request.Channel,
            Reference = request.Reference,
            SentBy = new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name },
        };
        var mensaje = await EmitirAsync(emisor, emision, archivo, pagaduria, "PayrollFileDelivery", request.Reference ?? archivo.Number, contenido, reloj.HoyLocal, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new TestPayrollMessageDto(mensaje.PublicId, archivo.PublicId, EnvioDeDescuentosConfirmadoV1.Type, 0));
    }

    /// <summary>La pagaduría del archivo (su <c>PublicId</c>).</summary>
    public static async Task<Guid> PagaduriaAsync(IApplicationDbContext db, PayrollDeductionFile archivo, CancellationToken ct) =>
        await (from a in db.PayrollAgreements.AsNoTracking()
               join p in db.People.AsNoTracking().IgnoreQueryFilters() on a.PaymasterPersonId equals p.Id
               where a.Id == archivo.PayrollAgreementId
               select p.PublicId).FirstAsync(ct);

    /// <summary>Emite un mensaje de Recaudo sobre el archivo: origen un documento nuevo, clave por el archivo, <c>related</c> = el archivo. Sin guardar.</summary>
    public static async Task<Domain.Entities.Integration.Transactions.IntegrationMessage> EmitirAsync(EmisorDeMensajes emisor, EmisionDeCartera emision,
        PayrollDeductionFile archivo, Guid pagaduria, string clase, string numero, object contenido, DateOnly hoy, CancellationToken ct)
    {
        var sucursal = await emision.SucursalPrincipalAsync(ct);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, Guid.NewGuid(), clase, ClavesDeRecaudador.Prueba, numero.Length > 40 ? numero[..40] : numero, hoy, sucursal,
                PersonPublicId: pagaduria),
            ClavesDeEvento.ConfirmacionPor(archivo.PublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always),
            Relacionado: new DocumentoRelacionado(archivo.PublicId, EmisionDeCartera.ClaseArchivoDeDescuentos, archivo.Number)), ct);
        return mensajes[0];
    }
}

/// <summary>
/// <c>POST /api/lending/test-doubles/collector/payroll-deduction-results</c> (<c>Lending.TestDoubles.Operate</c>, con clave; multipart con el archivo de
/// respuesta): lee la respuesta de la pagaduría con el <b>formato de vuelta</b> de la versión del convenio (<see cref="FlatFileReader"/>), casa cada fila
/// con su línea del archivo por el número del crédito, traduce el código de respuesta con los de la versión (sin código: por el valor descontado) y
/// emite <c>ResultadoDeDescuentos</c> v1 como <c>REC</c>. Un archivo que no se puede leer o que nombra créditos que no están en el archivo:
/// <c>Import.Invalid</c> con las líneas y campos. (nuevo)
/// </summary>
public sealed record EmitTestPayrollDeductionResultCommand(Guid FilePublicId, string FileName, byte[] Content)
    : IRequest<Result<TestPayrollMessageDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class EmitTestPayrollDeductionResultCommandValidator : AbstractValidator<EmitTestPayrollDeductionResultCommand>
{
    public EmitTestPayrollDeductionResultCommandValidator()
    {
        RuleFor(x => x.FilePublicId).NotEmpty();
        RuleFor(x => x.Content).NotNull().Must(c => c is { Length: > 0 }).WithMessage("Adjunte el archivo de respuesta de la pagaduría.");
        RuleFor(x => x.FileName).MaximumLength(200);
    }
}

public sealed class EmitTestPayrollDeductionResultCommandHandler(
    IApplicationDbContext db,
    EmisorDeMensajes emisor,
    EmisionDeCartera emision,
    IngenIA365ERP.Application.Common.Interfaces.Security.IActorActual actorActual,
    IDateTimeService reloj,
    IEnumerable<IDestinoDeMensajes> destinos) : IRequestHandler<EmitTestPayrollDeductionResultCommand, Result<TestPayrollMessageDto>>
{
    public async Task<Result<TestPayrollMessageDto>> Handle(EmitTestPayrollDeductionResultCommand request, CancellationToken ct)
    {
        if (!RecaudadorDePruebaRegistrado.Esta(destinos)) return Result.Failure<TestPayrollMessageDto>(RecaudadorDePruebaRegistrado.NoExiste());
        var archivo = await db.PayrollDeductionFiles.AsNoTracking().FirstOrDefaultAsync(f => f.PublicId == request.FilePublicId && !f.IsDeleted, ct);
        if (archivo is null) return Result.Failure<TestPayrollMessageDto>(ErroresDeLibranza.ArchivoInexistente());
        var version = await db.PayrollAgreementVersions.AsNoTracking().FirstAsync(v => v.Id == archivo.PayrollAgreementVersionId, ct);
        if (archivo.FileGroupId is { } grupoId) return await PorCodigoAsync(request, archivo, version, grupoId, ct);
        var formato = await db.BankFileFormats.AsNoTracking().Include(f => f.Fields).FirstAsync(f => f.Id == version.ResultFormatId, ct);
        var leido = FlatFileReader.Leer(formato, request.Content);
        if (!leido.IsValid) return Invalido(leido.Errors.Select(e => new { line = e.LineNumber, field = e.Field, message = e.Message }).ToList());

        var lineas = await (from l in db.PayrollDeductionFileLines.AsNoTracking()
                            join c in db.Loans.AsNoTracking().IgnoreQueryFilters() on l.LoanId equals c.Id
                            where l.PayrollDeductionFileId == archivo.Id
                            select new { Linea = l, c.PublicId, c.Number }).ToListAsync(ct);
        var porCredito = lineas.GroupBy(l => l.Number, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var codigos = await db.PayrollAgreementResultCodes.AsNoTracking().Where(c => c.PayrollAgreementVersionId == version.Id && !c.IsDeleted)
            .ToDictionaryAsync(c => c.PaymasterCode, StringComparer.OrdinalIgnoreCase, ct);

        var errores = new List<object>();
        var resultado = new List<DeductionResultLineV1>();
        foreach (var fila in leido.Rows)
        {
            var numero = fila.Texto(BankFieldSource.LoanNumber)?.Trim();
            if (numero is null || !porCredito.TryGetValue(numero, out var linea))
            {
                errores.Add(new { line = fila.LineNumber, field = nameof(BankFieldSource.LoanNumber), message = $"El crédito «{numero}» no está en el archivo {archivo.Number}." });
                continue;
            }
            var codigo = fila.Texto(BankFieldSource.ResultCode)?.Trim();
            codigos.TryGetValue(codigo ?? string.Empty, out var traducido);
            var esperado = linea.Linea.TotalAmount;
            var descontado = fila.Monto(BankFieldSource.DeductedAmount)
                             ?? (traducido?.Outcome == PayrollResultOutcome.Deducted ? esperado : traducido is null ? (decimal?)null : 0m);
            if (descontado is null)
            {
                errores.Add(new { line = fila.LineNumber, field = nameof(BankFieldSource.DeductedAmount), message = "Sin valor descontado ni un código de respuesta conocido." });
                continue;
            }
            var resultadoDeLinea = descontado.Value >= esperado ? "Deducted" : descontado.Value <= 0m ? "NotApplied" : "PartiallyDeducted";
            var fecha = fila.Fecha(BankFieldSource.DeductionDate) ?? (descontado.Value > 0m ? Pago(archivo, version) : null);
            resultado.Add(new DeductionResultLineV1
            {
                DeductionFileLinePublicId = linea.Linea.PublicId,
                Loan = new LoanRefV1 { LoanPublicId = linea.PublicId, LoanNumber = linea.Number },
                InstallmentNumber = linea.Linea.InstallmentNumber,
                ExpectedAmount = esperado,
                DeductedAmount = Math.Min(descontado.Value, esperado),
                DeductionDate = descontado.Value > 0m ? fecha : null,
                Outcome = resultadoDeLinea,
                PaymasterResultCode = codigo,
                Reason = resultadoDeLinea == "Deducted" ? null : (traducido?.NonDeductionReason is { } m ? ((MotivoDeNoDescuento)(int)m).ToString() : nameof(MotivoDeNoDescuento.Other)),
                Detail = traducido?.Description,
            });
        }
        if (errores.Count > 0) return Invalido(errores);

        var pagaduria = await ConfirmTestPayrollFileSentCommandHandler.PagaduriaAsync(db, archivo, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var contenido = new ResultadoDeDescuentosV1
        {
            FileMessageId = archivo.DeliveryMessagePublicId,
            FilePublicId = archivo.PublicId,
            FileVersion = archivo.Version,
            FileSha256 = archivo.FileSha256,
            PaymasterPersonPublicId = pagaduria,
            Period = new PayrollPeriodRefV1 { Year = archivo.PeriodYear, Month = archivo.PeriodMonth, SubPeriod = archivo.SubPeriodNumber > 1 ? archivo.SubPeriodNumber : null },
            Lines = resultado,
            Totals = new DeductionResultTotalsV1
            {
                LineCount = resultado.Count,
                Expected = resultado.Sum(l => l.ExpectedAmount),
                Deducted = resultado.Sum(l => l.DeductedAmount),
                NotApplied = resultado.Sum(l => l.ExpectedAmount - l.DeductedAmount),
            },
            ProcessedBy = new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name },
        };
        var mensaje = await ConfirmTestPayrollFileSentCommandHandler.EmitirAsync(emisor, emision, archivo, pagaduria, "PayrollDeductionResponse",
            string.IsNullOrWhiteSpace(request.FileName) ? archivo.Number : request.FileName, contenido, reloj.HoyLocal, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new TestPayrollMessageDto(mensaje.PublicId, archivo.PublicId, ResultadoDeDescuentosV1.Type, resultado.Count));
    }

    /// <summary>
    /// Feature 014 (T091; D36; mensajes.md §5.7 de la 014): la respuesta del archivo de un grupo, leída con el formato de vuelta del grupo. Cada fila trae el
    /// empleado (<c>PayerEmployeeId</c>), el código de la pagaduría (<c>PaymasterConceptCode</c>), lo descontado y, si quiere, el código de respuesta y la
    /// fecha; lo esperado es la suma de lo que el archivo le pidió a ese empleado por ese código (cero si no le pidió nada: Cartera lo deja en no
    /// aplicados). Emite <c>ResultadoDeDescuentos</c> v2 con las <c>conceptLines</c>.
    /// </summary>
    private async Task<Result<TestPayrollMessageDto>> PorCodigoAsync(EmitTestPayrollDeductionResultCommand request, PayrollDeductionFile archivo,
        PayrollAgreementVersion version, int grupoId, CancellationToken ct)
    {
        var grupo = await db.PayrollAgreementFileGroups.AsNoTracking().FirstAsync(g => g.Id == grupoId, ct);
        var formato = await db.BankFileFormats.AsNoTracking().Include(f => f.Fields).FirstAsync(f => f.Id == grupo.ResultFormatId, ct);
        var leido = FlatFileReader.Leer(formato, request.Content);
        if (!leido.IsValid) return Invalido(leido.Errors.Select(e => new { line = e.LineNumber, field = e.Field, message = e.Message }).ToList());

        var pedido = (await db.PayrollDeductionFileLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == archivo.Id)
                .Select(l => new { l.EmployeeIdAtPaymaster, l.PaymasterConceptCode, l.TotalAmount }).ToListAsync(ct))
            .Concat(await db.PayrollDeductionFileAccountLines.AsNoTracking().Where(l => l.PayrollDeductionFileId == archivo.Id)
                .Select(l => new { l.EmployeeIdAtPaymaster, PaymasterConceptCode = (string?)l.PaymasterConceptCode, l.TotalAmount }).ToListAsync(ct))
            .GroupBy(l => (Clave(l.EmployeeIdAtPaymaster), Clave(l.PaymasterConceptCode)))
            .ToDictionary(g => g.Key, g => g.Sum(l => l.TotalAmount));

        var errores = new List<object>();
        var filas = new List<DeductionConceptLineV2>();
        foreach (var fila in leido.Rows)
        {
            var empleado = fila.Texto(BankFieldSource.PayerEmployeeId)?.Trim();
            var codigo = fila.Texto(BankFieldSource.PaymasterConceptCode)?.Trim();
            if (string.IsNullOrEmpty(empleado) || string.IsNullOrEmpty(codigo))
            {
                errores.Add(new { line = fila.LineNumber, field = nameof(BankFieldSource.PaymasterConceptCode), message = "Cada fila trae el empleado y el código de la pagaduría." });
                continue;
            }
            var descontado = fila.Monto(BankFieldSource.DeductedAmount);
            if (descontado is null)
            {
                errores.Add(new { line = fila.LineNumber, field = nameof(BankFieldSource.DeductedAmount), message = "Sin valor descontado." });
                continue;
            }
            filas.Add(new DeductionConceptLineV2
            {
                PayerEmployeeId = empleado,
                PaymasterConceptCode = codigo,
                Expected = pedido.GetValueOrDefault((Clave(empleado), Clave(codigo))),
                Deducted = descontado.Value,
                PaymasterResultCode = fila.Texto(BankFieldSource.ResultCode)?.Trim(),
                DeductionDate = descontado.Value > 0m ? fila.Fecha(BankFieldSource.DeductionDate) ?? Pago(archivo, version) : null,
            });
        }
        if (errores.Count > 0) return Invalido(errores);

        var pagaduria = await ConfirmTestPayrollFileSentCommandHandler.PagaduriaAsync(db, archivo, ct);
        var actor = await actorActual.ObtenerAsync(ct);
        var contenido = new ResultadoDeDescuentosV2
        {
            FileMessageId = archivo.DeliveryMessagePublicId,
            FilePublicId = archivo.PublicId,
            FileVersion = archivo.Version,
            FileSha256 = archivo.FileSha256,
            FileGroupCode = archivo.FileGroupCode,
            PaymasterPersonPublicId = pagaduria,
            Period = new PayrollPeriodRefV1 { Year = archivo.PeriodYear, Month = archivo.PeriodMonth, SubPeriod = archivo.SubPeriodNumber > 1 ? archivo.SubPeriodNumber : null },
            ConceptLines = filas,
            Totals = new DeductionResultTotalsV1
            {
                LineCount = filas.Count,
                Expected = filas.Sum(f => f.Expected),
                Deducted = filas.Sum(f => f.Deducted),
                NotApplied = filas.Sum(f => Math.Max(0m, f.Expected - f.Deducted)),
            },
            ProcessedBy = new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name },
        };
        var mensaje = await ConfirmTestPayrollFileSentCommandHandler.EmitirAsync(emisor, emision, archivo, pagaduria, "PayrollDeductionResponse",
            string.IsNullOrWhiteSpace(request.FileName) ? archivo.Number : request.FileName, contenido, reloj.HoyLocal, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new TestPayrollMessageDto(mensaje.PublicId, archivo.PublicId, ResultadoDeDescuentosV2.Type, filas.Count));
    }

    private static string Clave(string? texto) => (texto ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>La fecha del descuento si la respuesta no la trae: el día de pago de la versión en el período del archivo.</summary>
    private static DateOnly Pago(PayrollDeductionFile archivo, PayrollAgreementVersion version) =>
        new DateOnly(archivo.PeriodYear, archivo.PeriodMonth, Math.Min(version.PaymentDay == 0 ? 1 : version.PaymentDay, DateTime.DaysInMonth(archivo.PeriodYear, archivo.PeriodMonth)));

    private static Result<TestPayrollMessageDto> Invalido(IReadOnlyList<object> errores) => Result.Failure<TestPayrollMessageDto>(new ErrorConDatos(
        ImportErrors.InvalidCode, $"El archivo de respuesta tiene {errores.Count} error(es): revise las líneas y campos señalados.", new { errors = errores }));
}
