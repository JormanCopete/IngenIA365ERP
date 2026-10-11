using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Disbursements.Files;

/// <summary>
/// Genera el archivo plano de transferencias de desembolso (feature 013, US3, T0454; api.md §9.3, <c>POST /api/lending/disbursement-files</c>,
/// <c>Lending.DisbursementFiles.Generate</c>, con clave; FR-047, T78, C10, C114 —el nombre evita el choque con el
/// <c>GenerateDisbursementFileCommand</c> de Nómina—). En una transacción:
/// <list type="number">
/// <item>la preparación común (<see cref="PreparacionDeArchivoDeDesembolsos"/>): formato del ámbito, cuenta de origen del banco del formato,
/// órdenes y líneas; una orden pedida que ya va en otro archivo vivo responde <c>Lending.DisbursementOrder.AlreadyInFile</c> con
/// <c>data.orders[]</c>; sin ninguna que entre, <c>Lending.DisbursementFile.Empty</c>;</item>
/// <item>el cerrojo de los créditos de las órdenes (T34) y la relectura: otra generación no las toma a la vez;</item>
/// <item>el número del archivo al final por <see cref="NumeradorDeCartera"/> (clase <c>DisbursementFile</c>), que es el lote del archivo;</item>
/// <item>el archivo por el motor común (<see cref="FlatFileWriter"/>, con su SHA-256), subido como adjunto de módulo <c>LendingDisbursementFile</c>
/// <b>antes</b> de guardar la fila;</item>
/// <item><c>LND_DisbursementFiles</c> con la copia de la cuenta de origen, sus <c>LND_DisbursementFileLines</c> con el registro exacto y la referencia
/// del archivo en cada orden (<c>CurrentDisbursementFileId</c>). No emite ningún mensaje: el archivo no viaja a Tesorería (C10).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed record GenerateLoanDisbursementFileCommand(
    Guid FormatPublicId,
    Guid SourceAccountPublicId,
    IReadOnlyList<Guid>? OrderPublicIds = null,
    DateOnly? PaymentDate = null,
    string? Reason = null) : IRequest<Result<DisbursementFileGeneratedDto>>, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class GenerateLoanDisbursementFileCommandValidator : AbstractValidator<GenerateLoanDisbursementFileCommand>
{
    public GenerateLoanDisbursementFileCommandValidator()
    {
        RuleFor(x => x.FormatPublicId).NotEmpty().WithMessage("Elija el formato del archivo.");
        RuleFor(x => x.SourceAccountPublicId).NotEmpty().WithMessage("Elija la cuenta bancaria de origen.");
        RuleFor(x => x.Reason).MaximumLength(60);
    }
}

public sealed class GenerateLoanDisbursementFileCommandHandler(
    IApplicationDbContext db,
    PreparacionDeArchivoDeDesembolsos preparacion,
    NumeradorDeCartera numerador,
    ICerrojoDeCartera cerrojo,
    ISender sender,
    IActorActual actorActual,
    IDateTimeService reloj) : IRequestHandler<GenerateLoanDisbursementFileCommand, Result<DisbursementFileGeneratedDto>>
{
    /// <summary>El prefijo del numerador de archivos de desembolso.</summary>
    public const string Prefijo = "ARD-";

    public async Task<Result<DisbursementFileGeneratedDto>> Handle(GenerateLoanDisbursementFileCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDeGobierno.SinUsuario());
        var fecha = request.PaymentDate ?? reloj.HoyLocal;

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var secuencia = await db.DisbursementFiles.AsNoTracking().CountAsync(f => f.PaymentDate == fecha, ct) + 1;
            var prep = await preparacion.PrepararAsync(request.FormatPublicId, request.SourceAccountPublicId, request.OrderPublicIds, fecha,
                PreparacionDeArchivoDeDesembolsos.LoteDeVistaPrevia, secuencia, ct);
            if (prep.IsFailure) return Falla(prep.Error);
            var p = prep.Value;

            var enOtro = p.Pendientes.Where(x => x.Reason == MotivosDePendienteDeArchivo.EnArchivo).Select(x => x.OrderNumber).ToList();
            if (request.OrderPublicIds is { Count: > 0 } && enOtro.Count > 0) return Falla(ErroresDeArchivoDeDesembolsos.OrdenEnArchivo(enOtro));
            if (p.Incluidos.Count == 0) return Falla(ErroresDeArchivoDeDesembolsos.Vacio(p.Pendientes));
            if (p.BancoId is not { } bancoId) return Falla(ErroresDeArchivoDeDesembolsos.CuentaInexistente());

            // El cerrojo de los créditos (T34) y la relectura de las órdenes ya bloqueadas: ninguna se fue a otro archivo entretanto.
            var ids = p.Incluidos.Select(c => c.Orden.Id).ToList();
            await cerrojo.TomarSetupAsync(exclusivo: false, ct);
            await cerrojo.BloquearCreditosAsync(p.Incluidos.Select(c => c.Orden.LoanId).Distinct().Order().ToList(), ModoDeEspera.Esperar, ct);
            var ordenes = await db.DisbursementOrders.Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);
            var tomadas = ordenes.Values.Where(o => !o.ElegibleParaArchivo).Select(o => o.Number).ToList();
            if (tomadas.Count > 0) return Falla(ErroresDeArchivoDeDesembolsos.OrdenEnArchivo(tomadas));

            // El número al final (T34): es el lote del archivo.
            var numero = await numerador.AsignarAsync(LendingNumberKind.DisbursementFile, string.Empty, Prefijo, ct);
            var contexto = PreparacionDeArchivoDeDesembolsos.ConLote(p.Contexto, numero);
            BankFileOutput salida;
            try
            {
                salida = FlatFileWriter.Escribir(p.Formato, contexto, p.Incluidos.Select(c => c.Valores).ToList());
            }
            catch (BankFileWriteException ex)
            {
                var orden = ex.LineNumber >= 1 && ex.LineNumber <= p.Incluidos.Count ? p.Incluidos[ex.LineNumber - 1].Orden.Number : "?";
                return Falla(ErroresDeArchivoDeDesembolsos.NoCabe(ex.LineNumber, ex.Field, orden));
            }

            // El adjunto de módulo, antes de la fila: si el almacén falla no queda un archivo «generado» que nadie puede bajar.
            var publicId = Guid.NewGuid();
            var subida = await sender.Send(new UploadAttachmentCommand(AdjuntosDeModulo.ArchivoDeDesembolsos, publicId, salida.FileName, salida.ContentType,
                salida.Content), ct);
            if (subida.IsFailure) return Falla(subida.Error);

            var archivo = new DisbursementFile
            {
                PublicId = publicId,
                Number = numero,
                BankId = bancoId,
                FormatId = p.Formato.Id,
                FormatCode = p.Formato.Code,
                PaymentDate = fecha,
                DailySequence = secuencia,
                Reference = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                SourceAccountPublicId = p.Cuenta!.AccountPublicId,
                SourceAccountNumber = p.Cuenta.AccountNumber ?? p.Cuenta.AccountCode,
                LineCount = salida.LineCount,
                TotalAmount = salida.TotalAmount,
                FileName = salida.FileName,
                FileSha256 = salida.Sha256,
                FileAttachmentPublicId = subida.Value,
                GeneratedAt = contexto.GeneratedAt,
                GeneratedByUserId = usuario,
            };
            db.DisbursementFiles.Add(archivo);
            await db.SaveChangesAsync(ct);

            for (var i = 0; i < p.Incluidos.Count; i++)
            {
                var orden = ordenes[p.Incluidos[i].Orden.Id];
                db.DisbursementFileLines.Add(new DisbursementFileLine
                {
                    FileId = archivo.Id,
                    LineNumber = i + 1,
                    OrderId = orden.Id,
                    BeneficiaryPersonId = orden.BeneficiaryPersonId,
                    DestinationBankId = orden.DestinationBankId,
                    AccountType = orden.DestinationAccountType ?? 0,
                    AccountNumber = orden.DestinationAccountNumber ?? string.Empty,
                    Amount = orden.NetAmount,
                    RecordText = salida.DetailTexts[i],
                });
                orden.AsignarArchivo(archivo.Id);
            }
            await db.SaveChangesAsync(ct);

            var dto = (await ArchivosDeDesembolsos.DtosAsync(db, [archivo], ct))[0];
            return Result.Success(new DisbursementFileGeneratedDto(dto, p.Pendientes));
        }, ct);
    }

    private static Result<DisbursementFileGeneratedDto> Falla(Error e) => Result.Failure<DisbursementFileGeneratedDto>(e);
}
