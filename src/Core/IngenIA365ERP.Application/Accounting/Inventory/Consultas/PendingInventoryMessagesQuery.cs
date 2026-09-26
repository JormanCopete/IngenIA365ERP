using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Inventory.Consultas;

/// <summary>
/// Lo que Contabilidad todavía no recibe de Inventario en un rango de fechas de operación (feature 012, T491;
/// contracts/contabilidad.md §8): cuántas entregas a <see cref="IntegrationDestinations.Accounting"/> siguen
/// <c>Pending</c>, <c>InBatch</c> o <c>Rejected</c>, la operación más antigua y los tipos de documento. Las
/// <c>NotApplicable</c> no cuentan (no pasan por decisión de la cooperativa) ni las <c>ValidationFailed</c> (no
/// llegaron a confirmarse). Lee sólo <c>COR_Integration*</c>: Contabilidad no conoce las tablas de Inventario.
/// </summary>
public sealed record PendingInventoryMessagesQuery(DateOnly From, DateOnly To) : IRequest<Result<PendientesDeInventarioDto>>;

/// <summary>El resumen que acompaña a <c>Accounting.Period.InventoryPending</c>.</summary>
public sealed record PendientesDeInventarioDto(int Pending, int InBatch, int Rejected, DateOnly? OldestOperationDate, IReadOnlyList<string> Types)
{
    public int Total => Pending + InBatch + Rejected;
    public bool HayPendientes => Total > 0;
}

public sealed class PendingInventoryMessagesQueryValidator : AbstractValidator<PendingInventoryMessagesQuery>
{
    public PendingInventoryMessagesQueryValidator() =>
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha final no puede ser anterior a la inicial.");
}

public sealed class PendingInventoryMessagesQueryHandler(IApplicationDbContext db) : IRequestHandler<PendingInventoryMessagesQuery, Result<PendientesDeInventarioDto>>
{
    public async Task<Result<PendientesDeInventarioDto>> Handle(PendingInventoryMessagesQuery request, CancellationToken ct) =>
        Result.Success(await ContarAsync(db, request.From, request.To, ct));

    /// <summary>La misma cuenta, para quien la necesita dentro de su propio comando (el cierre del mes).</summary>
    public static async Task<PendientesDeInventarioDto> ContarAsync(IApplicationDbContext db, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var filas = await db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => !d.IsDeleted
                        && d.Destination == IntegrationDestinations.Accounting
                        && (d.Status == DeliveryStatus.Pending || d.Status == DeliveryStatus.InBatch || d.Status == DeliveryStatus.Rejected)
                        && d.Message!.OperationDate >= desde && d.Message.OperationDate <= hasta)
            .Select(d => new { d.Status, d.Message!.OperationDate, Tipo = d.Message.OriginDocumentTypeCode ?? d.Message.Type })
            .ToListAsync(ct);

        return new PendientesDeInventarioDto(
            filas.Count(f => f.Status == DeliveryStatus.Pending),
            filas.Count(f => f.Status == DeliveryStatus.InBatch),
            filas.Count(f => f.Status == DeliveryStatus.Rejected),
            filas.Count == 0 ? null : filas.Min(f => f.OperationDate),
            filas.Select(f => f.Tipo).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());
    }
}
