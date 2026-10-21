using FluentValidation;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.Inventory.Consultas;

/// <summary>
/// Lo que Contabilidad todavía no recibe de Inventario en un rango de fechas de operación (feature 012, T491;
/// contracts/contabilidad.md §8). Desde la 013 (T0074, T15) su lógica vive en <see cref="ListPendingIntegrationMessagesQuery"/>,
/// que filtra por módulo de origen: ésta la llama con <c>INV</c>, para que los pendientes de Cartera no cuenten como de
/// Inventario.
/// </summary>
public sealed record PendingInventoryMessagesQuery(DateOnly From, DateOnly To) : IRequest<Result<PendientesDeIntegracionDto>>;

public sealed class PendingInventoryMessagesQueryValidator : AbstractValidator<PendingInventoryMessagesQuery>
{
    public PendingInventoryMessagesQueryValidator() =>
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).WithMessage("La fecha final no puede ser anterior a la inicial.");
}

public sealed class PendingInventoryMessagesQueryHandler(IApplicationDbContext db) : IRequestHandler<PendingInventoryMessagesQuery, Result<PendientesDeIntegracionDto>>
{
    public async Task<Result<PendientesDeIntegracionDto>> Handle(PendingInventoryMessagesQuery request, CancellationToken ct) =>
        Result.Success(await ContarAsync(db, request.From, request.To, ct));

    /// <summary>Lo pendiente de Inventario, para quien la necesita dentro de su propio comando (el cierre del mes).</summary>
    public static Task<PendientesDeIntegracionDto> ContarAsync(IApplicationDbContext db, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        ListPendingIntegrationMessagesQueryHandler.ContarAsync(db, ModulosDeOrigen.Inventario, desde, hasta, ct);
}
