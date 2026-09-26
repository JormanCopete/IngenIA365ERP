using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Cierra un lote que corrió (feature 012, T503; contracts/contabilidad.md §5.4). <b>Comando de proceso, sin ruta</b>: lo envía sólo
/// <c>DespachadorDeMensajes</c> cuando al lote no le queda ninguna entrega por procesar. Lee sus totales de las entregas del lote
/// (mensajes, documentos, procesados, rechazados, comprobantes distintos) y lo deja <c>Completed</c>, <c>CompletedWithRejections</c> o
/// <c>Empty</c> (sin mensajes: prueba que corrió). Débitos y créditos son de los comprobantes, que la plataforma no lee: los informa el
/// despachador con lo que devolvió el destino. Emite <c>Accounting.Inventory.BatchProcessed</c> por <see cref="AccountingAuditEmitter"/>
/// (T495) si el destino es Contabilidad. Un lote ya cerrado no se vuelve a cerrar (<c>false</c>); un choque de <c>RowVersion</c> se
/// reintenta y relee.
/// </summary>
public sealed record CloseIntegrationBatchCommand(Guid BatchPublicId, decimal TotalDebit = 0m, decimal TotalCredit = 0m)
    : IRequest<Result<bool>>, IReintentableAnteConcurrencia;

public sealed class CloseIntegrationBatchCommandValidator : AbstractValidator<CloseIntegrationBatchCommand>
{
    public CloseIntegrationBatchCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.TotalDebit).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TotalCredit).GreaterThanOrEqualTo(0);
    }
}

public sealed class CloseIntegrationBatchCommandHandler(IApplicationDbContext db, IDateTimeService reloj, AccountingAuditEmitter auditoria)
    : IRequestHandler<CloseIntegrationBatchCommand, Result<bool>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<bool>> Handle(CloseIntegrationBatchCommand request, CancellationToken ct)
    {
        var lote = await db.IntegrationBatches.FirstOrDefaultAsync(b => b.PublicId == request.BatchPublicId, ct);
        if (lote is null) return Result.Failure<bool>(ErroresDeIntegracion.LoteNoEncontrado(request.BatchPublicId));
        if (lote.EstaCerrado) return Result.Success(false);
        if (lote.Status != BatchStatus.Running) return Result.Failure<bool>(ErroresDeIntegracion.LoteNoIniciado(request.BatchPublicId));

        var entregas = await db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.BatchId == lote.Id)
            .Select(d => new
            {
                d.Status,
                d.Message!.PublicId,
                d.Message.OriginPublicId,
                d.Message.OriginNumber,
                d.ResultReference,
                d.ResultVoucherTypeCode,
                d.ResultVoucherNumber,
                d.LastErrorCode,
                d.LastErrorMessage,
            })
            .ToListAsync(ct);

        var pendientes = entregas.Count(e => e.Status is DeliveryStatus.InBatch or DeliveryStatus.Pending);
        if (pendientes > 0) return Result.Failure<bool>(ErroresDeIntegracion.LoteConEntregasPendientes(request.BatchPublicId, pendientes));

        var ahora = reloj.UtcNow;
        var procesadas = entregas.Where(e => e.Status == DeliveryStatus.Processed).ToList();
        var rechazadas = entregas.Where(e => e.Status == DeliveryStatus.Rejected).ToList();
        var comprobantes = procesadas.Where(e => e.ResultVoucherTypeCode != null)
            .GroupBy(e => e.ResultReference)
            .Select(g => new { reference = g.Key, voucherTypeCode = g.First().ResultVoucherTypeCode, number = g.First().ResultVoucherNumber, messages = g.Count() })
            .ToList();

        var resumen = JsonSerializer.Serialize(new
        {
            vouchers = comprobantes,
            rejected = rechazadas.Select(r => new { messagePublicId = r.PublicId, documentNumber = r.OriginNumber, code = r.LastErrorCode, message = r.LastErrorMessage }),
        }, Json);
        var totales = new TotalesDeLote(
            entregas.Count,
            entregas.Select(e => e.OriginPublicId).Distinct().Count(),
            procesadas.Count,
            rechazadas.Count,
            comprobantes.Count,
            request.TotalDebit,
            request.TotalCredit,
            resumen);

        if (entregas.Count == 0) lote.MarcarVacio(ahora);
        else if (rechazadas.Count > 0) lote.CompletarConRechazos(totales, ahora);
        else lote.Completar(totales, ahora);

        await db.SaveChangesAsync(ct);

        if (lote.Destination == IntegrationDestinations.Accounting)
        {
            await auditoria.EmitirLoteDeInventarioProcesadoAsync(lote.PublicId, new
            {
                number = lote.Number,
                trigger = lote.Trigger.ToString(),
                status = lote.Status.ToString(),
                messages = lote.MessageCount,
                documents = lote.DocumentCount,
                processed = lote.ProcessedCount,
                rejected = lote.RejectedCount,
                vouchers = lote.VoucherCount,
                debit = lote.TotalDebit,
                credit = lote.TotalCredit,
            }, ct);
        }

        return Result.Success(true);
    }
}
