using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Treasury;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Integration;

/// <summary>
/// Consume en Tesorería <c>ArchivoDeDesembolsosGenerado</c> v1 y <c>ArchivoDeDesembolsosAnulado</c> v1 (feature 015, US10, T0267; E-01 c; FR-108, FR-164;
/// mensajes.md §5.4): el generado guarda <c>TES_LendingDisbursementFiles</c> con sus órdenes, casa cada una con la orden de la bandeja
/// (<c>OriginOrderPublicId</c>) y le anota el archivo —Tesorería no la paga: espera su resultado—, y la cuenta de origen con la cuenta propia de Tesorería por
/// número; el anulado deja el archivo <c>Voided</c> y suelta las órdenes que siguen sin resultado, a la espera de otro archivo de Cartera. Sin ruta; deja su
/// recibo en <c>TES_InboundMessages</c> en el mismo guardado. (nuevo)
/// </summary>
public sealed record ConsumeLendingDisbursementFileCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeLendingDisbursementFileCommandValidator : AbstractValidator<ConsumeLendingDisbursementFileCommand>
{
    public ConsumeLendingDisbursementFileCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumeLendingDisbursementFileCommandHandler(IApplicationDbContext db, IMensajesEntrantes entrantes, IDateTimeService reloj)
    : IRequestHandler<ConsumeLendingDisbursementFileCommand, Result<ResultadoDeConsumo>>
{
    public const string CodigoArchivoDesconocido = "Treasury.LendingDisbursementFile.Unknown";

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeLendingDisbursementFileCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Treasury, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (await RecibosDeTesoreria.PrevioAsync(db, mensaje.MessageId, ct) is { } previo) return Result.Success(RecibosDeTesoreria.Repetido(previo));

        return await TransaccionExplicita.EjecutarAsync(db, async () =>
        {
            var ahora = reloj.UtcNow;
            switch (mensaje.Payload)
            {
                case ArchivoDeDesembolsosGeneradoV1 generado when mensaje.Version == 1:
                {
                    if (await db.LendingDisbursementFiles.AnyAsync(f => f.FilePublicId == generado.FilePublicId, ct))
                    {
                        db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(mensaje, ahora, InboundMessageOutcome.Processed, null, null, "El archivo ya estaba registrado."));
                        await db.SaveChangesAsync(ct);
                        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
                    }
                    var cuenta = generado.SourceBankAccount is { BankAccountNumber.Length: > 0 } origen
                        ? await db.OwnBankAccounts.AsNoTracking().Where(c => c.AccountNumber == origen.BankAccountNumber).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct)
                        : null;
                    var archivo = new LendingDisbursementFile
                    {
                        FileMessagePublicId = mensaje.MessageId, FilePublicId = generado.FilePublicId, FileNumber = generado.FileNumber, Version = generado.Version,
                        AttachmentPublicId = generado.File.AttachmentPublicId, FileName = Recortar(generado.File.FileName, 200), FileSha256 = generado.File.Sha256,
                        SizeBytes = generado.File.SizeBytes, FormatCode = generado.File.FormatCode, BankAccountId = cuenta, SourceBankCode = generado.SourceBankAccount?.BankCode,
                        SourceAccountNumber = generado.SourceBankAccount?.BankAccountNumber, Total = generado.Total, LineCount = generado.LineCount, ReceivedAt = ahora,
                        GeneratedByName = Recortar(generado.GeneratedBy.Name, 150), CreatedAt = ahora, CreatedBy = RecibosDeTesoreria.Actor,
                    };
                    var publicos = generado.Orders.Select(o => o.OrderPublicId).ToList();
                    var enBandeja = await db.PaymentOrders.Where(o => publicos.Contains(o.OriginOrderPublicId) && o.Origin == PaymentOrderOrigin.LendingDisbursement)
                        .ToDictionaryAsync(o => o.OriginOrderPublicId, ct);
                    foreach (var o in generado.Orders)
                        archivo.Orders.Add(new LendingDisbursementFileOrder
                        {
                            OriginOrderPublicId = o.OrderPublicId, OrderNumber = o.OrderNumber, NetAmount = o.NetAmount, PayeePersonPublicId = o.PayeePersonPublicId,
                            PaymentOrderId = enBandeja.TryGetValue(o.OrderPublicId, out var po) ? po.Id : null, CreatedAt = ahora, CreatedBy = RecibosDeTesoreria.Actor,
                        });
                    db.LendingDisbursementFiles.Add(archivo);
                    await db.SaveChangesAsync(ct);
                    foreach (var po in enBandeja.Values)
                    {
                        po.LendingDisbursementFileId = archivo.Id;
                        po.PaidByLendingFile = true;
                    }
                    db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(mensaje, ahora, InboundMessageOutcome.Processed, null, null,
                        $"Archivo {generado.FileNumber} de Cartera con {generado.Orders.Count} órdenes ({enBandeja.Count} en la bandeja)."));
                    await db.SaveChangesAsync(ct);
                    return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
                }
                case ArchivoDeDesembolsosAnuladoV1 anulado when mensaje.Version == 1:
                {
                    var archivo = await db.LendingDisbursementFiles.FirstOrDefaultAsync(f => f.FilePublicId == anulado.FilePublicId, ct);
                    if (archivo is null)
                    {
                        var detalle = $"El archivo {anulado.FileNumber} no llegó a Tesorería: no se puede anular.";
                        db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(mensaje, ahora, InboundMessageOutcome.Rejected, null, CodigoArchivoDesconocido, detalle));
                        await db.SaveChangesAsync(ct);
                        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected(CodigoArchivoDesconocido, detalle));
                    }
                    archivo.Status = LendingDisbursementFileState.Voided;
                    archivo.VoidMessagePublicId = mensaje.MessageId;
                    archivo.VoidReason = Recortar(anulado.Reason, 500);
                    archivo.VoidedAt = ahora;
                    var delArchivo = await db.PaymentOrders.Where(o => o.LendingDisbursementFileId == archivo.Id).ToListAsync(ct);
                    var ids = delArchivo.Select(o => o.Id).ToList();
                    var conResultado = (await db.DisbursementResults.AsNoTracking().Where(r => ids.Contains(r.PaymentOrderId)).Select(r => r.PaymentOrderId).ToListAsync(ct))
                        .ToHashSet();
                    var sueltas = delArchivo.Where(o => !conResultado.Contains(o.Id)).ToList();
                    foreach (var po in sueltas) po.LendingDisbursementFileId = null;
                    db.TreasuryInboundMessages.Add(RecibosDeTesoreria.Nuevo(mensaje, ahora, InboundMessageOutcome.Processed, null, null,
                        $"Archivo {anulado.FileNumber} anulado: {sueltas.Count} órdenes esperan otro archivo de Cartera."));
                    await db.SaveChangesAsync(ct);
                    return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
                }
                default:
                    return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Tesorería no acepta {mensaje.Type} v{mensaje.Version}."));
            }
        }, ct);
    }

    private static string Recortar(string? texto, int largo) => string.IsNullOrEmpty(texto) ? string.Empty : texto.Length <= largo ? texto : texto[..largo];
}

/// <summary>
/// El enrutador de las unidades de Tesorería con clave <c>Confirmation:{x}</c> (feature 015, T0267): la anulación de una orden de giro y el archivo de desembolsos de
/// Cartera comparten la forma de clave, así que lee el tipo y manda al comando que corresponde. Sin ruta. (nuevo)
/// </summary>
public sealed record ConsumeKeyedTreasuryUnitCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>;

public sealed class ConsumeKeyedTreasuryUnitCommandValidator : AbstractValidator<ConsumeKeyedTreasuryUnitCommand>
{
    public ConsumeKeyedTreasuryUnitCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumeKeyedTreasuryUnitCommandHandler(IMensajesEntrantes entrantes, ISender sender)
    : IRequestHandler<ConsumeKeyedTreasuryUnitCommand, Result<ResultadoDeConsumo>>
{
    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeKeyedTreasuryUnitCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Treasury, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        if (leidos.Value.Any(m => m.Type is ArchivoDeDesembolsosGeneradoV1.Type or ArchivoDeDesembolsosAnuladoV1.Type))
            return await sender.Send(new ConsumeLendingDisbursementFileCommand(request.MessagePublicIds), ct);
        // Feature 015, T2 (fase 41): la dispersión de Nómina; (fase 42) las facturas de proveedor de Comercial.
        if (leidos.Value.Any(m => m.Type == IngenIA365ERP.Application.Common.Integration.Contracts.Payroll.DispersionDeNominaGeneradaV1.Type))
            return await sender.Send(new ConsumePayrollDispersionCommand(request.MessagePublicIds), ct);
        return await sender.Send(new ConsumePaymentOrderVoidCommand(request.MessagePublicIds), ct);
    }
}
