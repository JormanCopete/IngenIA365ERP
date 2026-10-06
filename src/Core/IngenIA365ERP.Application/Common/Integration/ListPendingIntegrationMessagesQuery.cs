using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Lo que Contabilidad todavía no recibe de <b>un módulo</b> en un rango de fechas de operación (feature 013, T0074, T15; antes era
/// <c>PendingInventoryMessagesQuery</c> de la 012, T491, que ahora la llama con <c>INV</c>): cuántas entregas a
/// <see cref="IntegrationDestinations.Accounting"/> de mensajes de <see cref="OriginModule"/> siguen <c>Pending</c>, <c>InBatch</c>
/// o <c>Rejected</c>, la operación más antigua y los tipos de documento. Las <c>NotApplicable</c> no cuentan (no pasan por decisión
/// de la cooperativa) ni las <c>ValidationFailed</c>. Sin el filtro por módulo, el aviso del cierre contable de Inventario contaría
/// los pendientes de Cartera como suyos. Lee sólo <c>COR_Integration*</c>. Sin ruta: la usan el cierre contable y la conciliación.
/// </summary>
public sealed record ListPendingIntegrationMessagesQuery(string OriginModule, DateOnly From, DateOnly To) : IRequest<Result<PendientesDeIntegracionDto>>;

/// <summary>El resumen de lo pendiente de un módulo (el que acompaña a <c>Accounting.Period.InventoryPending</c> para Inventario).</summary>
public sealed record PendientesDeIntegracionDto(int Pending, int InBatch, int Rejected, DateOnly? OldestOperationDate, IReadOnlyList<string> Types)
{
    public int Total => Pending + InBatch + Rejected;

    public bool HayPendientes => Total > 0;
}

public sealed class ListPendingIntegrationMessagesQueryValidator : AbstractValidator<ListPendingIntegrationMessagesQuery>
{
    public ListPendingIntegrationMessagesQueryValidator()
    {
        RuleFor(x => x.OriginModule).NotEmpty().MaximumLength(3);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha final no puede ser anterior a la inicial.");
    }
}

public sealed class ListPendingIntegrationMessagesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListPendingIntegrationMessagesQuery, Result<PendientesDeIntegracionDto>>
{
    public async Task<Result<PendientesDeIntegracionDto>> Handle(ListPendingIntegrationMessagesQuery request, CancellationToken ct) =>
        Result.Success(await ContarAsync(db, request.OriginModule, request.From, request.To, ct));

    /// <summary>La misma cuenta, para quien la necesita dentro de su propio comando (el cierre del mes).</summary>
    public static async Task<PendientesDeIntegracionDto> ContarAsync(
        IApplicationDbContext db, string modulo, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var filas = await db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => !d.IsDeleted
                        && d.Destination == IntegrationDestinations.Accounting
                        && d.Message!.OriginModule == modulo
                        && (d.Status == DeliveryStatus.Pending || d.Status == DeliveryStatus.InBatch || d.Status == DeliveryStatus.Rejected)
                        && d.Message.OperationDate >= desde && d.Message.OperationDate <= hasta)
            .Select(d => new { d.Status, d.Message!.OperationDate, Tipo = d.Message.OriginDocumentTypeCode ?? d.Message.Type })
            .ToListAsync(ct);

        return new PendientesDeIntegracionDto(
            filas.Count(f => f.Status == DeliveryStatus.Pending),
            filas.Count(f => f.Status == DeliveryStatus.InBatch),
            filas.Count(f => f.Status == DeliveryStatus.Rejected),
            filas.Count == 0 ? null : filas.Min(f => f.OperationDate),
            filas.Select(f => f.Tipo).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList());
    }
}
