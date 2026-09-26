using FluentValidation;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Periods;

/// <summary>
/// Reabre el <b>último</b> mes cerrado (feature 012, T290; FR-047, US3-5; contracts/api.md §13.4), con el permiso especial
/// <c>Inventory.Periods.Reopen</c> y motivo: toma <c>INV_Setup</c> en exclusivo, deja <c>Superseded</c> el valorizado de su
/// versión (no lo borra), retrocede <c>LastClosedDate</c> al fin del mes anterior —o a nulo si era el primero— y emite
/// <c>PeriodoInventarioReabierto</c> (informativo, <c>Reopen:{closingVersion}</c>). Un mes que no es el último cerrado:
/// <c>Inventory.Period.NotLastClosed</c> nombrándolo; uno abierto: <c>.NotClosed</c>. La auditoría (con el motivo) la ponen
/// los behaviors. (nuevo)
/// </summary>
public sealed record ReopenInventoryPeriodCommand(int Year, int Month, string Reason)
    : IRequest<Result<ReopenPeriodResultDto>>, IConMotivo, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ReopenInventoryPeriodCommandValidator : ValidadorConMotivo<ReopenInventoryPeriodCommand>
{
    public ReopenInventoryPeriodCommandValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2000, 9999);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class ReopenInventoryPeriodCommandHandler(
    IApplicationDbContext db,
    ICerrojoDeInventario cerrojo,
    EmisorDeMensajes emisor,
    IActorActual actorActual,
    IDateTimeService reloj)
    : IRequestHandler<ReopenInventoryPeriodCommand, Result<ReopenPeriodResultDto>>
{
    public Task<Result<ReopenPeriodResultDto>> Handle(ReopenInventoryPeriodCommand request, CancellationToken ct) =>
        TransaccionExplicita.EjecutarAsync(db, () => ReabrirAsync(request, ct), ct);

    private async Task<Result<ReopenPeriodResultDto>> ReabrirAsync(ReopenInventoryPeriodCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Falla(ErroresDelDocumento.SinUsuario());

        await cerrojo.BloquearAsync(new PedidoDeCerrojo { Setup = ModoDeBloqueoDelSetup.Exclusivo }, ct);
        var setup = await db.InventorySetups.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (setup is null) return Falla(InventoryErrors.PeriodNotStarted());

        var periodo = await db.InventoryPeriods.FirstOrDefaultAsync(p => p.Year == request.Year && p.Month == request.Month, ct);
        if (periodo is null || periodo.Status != InventoryPeriodStatus.Closed) return Falla(InventoryErrors.PeriodNotClosed(request.Year, request.Month));
        if (setup.LastClosedDate is not { } ultimo || ultimo != periodo.Fin)
            return Falla(InventoryErrors.PeriodNotLastClosed(request.Year, request.Month,
                InventoryErrors.MesDeInventario.De(setup.LastClosedDate ?? periodo.Fin)));

        var version = periodo.CloseVersion;
        var saldos = await db.PeriodClosingBalances.Where(b => b.PeriodId == periodo.Id && b.Version == version && !b.Superseded).ToListAsync(ct);
        foreach (var saldo in saldos) saldo.Superseded = true;

        var ahora = reloj.UtcNow;
        periodo.Status = InventoryPeriodStatus.Open;
        periodo.ReopenedAt = ahora;
        periodo.ReopenedByUserId = usuario;
        periodo.ReopenReason = request.Reason.Trim();
        var anterior = periodo.Inicio.AddDays(-1);
        setup.LastClosedDate = anterior < setup.StartDate ? null : anterior;

        var emitidos = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Operation, periodo.PublicId, null, null, $"{request.Year:0000}-{request.Month:00}", periodo.Fin,
                await OperacionesDeInventario.SucursalPrincipalAsync(db, ct)),
            ClavesDeEvento.Reapertura(version),
            [new PeriodoInventarioReabiertoV1 { Year = request.Year, Month = request.Month, ReopenedClosingVersion = version, Reason = request.Reason.Trim() }],
            new ModoDeEntrega.Sellado(DeliveryMode.Online)), ct);

        await db.SaveChangesAsync(ct);
        return Result.Success(new ReopenPeriodResultDto(request.Year, request.Month, ahora, version, emitidos[0].PublicId));
    }

    private static Result<ReopenPeriodResultDto> Falla(Error error) => Result.Failure<ReopenPeriodResultDto>(error);
}

/// <summary>
/// <c>GET /api/inventory/periods?year=</c> (feature 012, T290; contracts/api.md §13.4): <c>startDate</c>, <c>lastClosedDate</c> y
/// los meses desde el de <c>startDate</c> hasta el actual (hora de Colombia) con su estado; un mes sin fila está abierto.
/// <paramref name="Year"/> filtra los meses de ese año. Sin <c>INV_Setup</c>, sin meses. (nuevo)
/// </summary>
public sealed record ListInventoryPeriodsQuery(int? Year = null) : IRequest<Result<InventoryPeriodsDto>>;

public sealed class ListInventoryPeriodsQueryValidator : AbstractValidator<ListInventoryPeriodsQuery>
{
    public ListInventoryPeriodsQueryValidator() => RuleFor(x => x.Year).InclusiveBetween(2000, 9999).When(x => x.Year is not null);
}

public sealed class ListInventoryPeriodsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListInventoryPeriodsQuery, Result<InventoryPeriodsDto>>
{
    public async Task<Result<InventoryPeriodsDto>> Handle(ListInventoryPeriodsQuery request, CancellationToken ct)
    {
        var setup = await db.InventorySetups.AsNoTracking().OrderBy(s => s.Id).FirstOrDefaultAsync(ct);
        if (setup is null) return Result.Success(new InventoryPeriodsDto(null, null, []));

        var filas = await db.InventoryPeriods.AsNoTracking().ToListAsync(ct);
        var usuarioIds = filas.SelectMany(p => new[] { p.ClosedByUserId, p.ReopenedByUserId, p.UnbilledShipmentsAcceptedByUserId }).OfType<int>().Distinct().ToList();
        var usuarios = usuarioIds.Count == 0
            ? new Dictionary<int, UsuarioDto>()
            : await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => usuarioIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => new UsuarioDto(u.PublicId, u.Username), ct);

        var hoy = reloj.HoyLocal;
        var meses = new List<InventoryPeriodDto>();
        for (var mes = new DateOnly(setup.StartDate.Year, setup.StartDate.Month, 1); mes <= hoy; mes = mes.AddMonths(1))
        {
            if (request.Year is { } anio && mes.Year != anio) continue;
            var fila = filas.FirstOrDefault(p => p.Year == mes.Year && p.Month == mes.Month);
            if (fila is null)
            {
                meses.Add(new InventoryPeriodDto(mes.Year, mes.Month, InventoryPeriodStatus.Open, null, null, null, null, null, null, null));
                continue;
            }
            meses.Add(new InventoryPeriodDto(
                mes.Year, mes.Month, fila.Status, fila.ClosedAt, Usuario(fila.ClosedByUserId), fila.ReopenedAt, Usuario(fila.ReopenedByUserId),
                fila.ReopenReason, null, fila.CloseVersion == 0 ? null : fila.CloseVersion));
        }
        return Result.Success(new InventoryPeriodsDto(setup.StartDate, setup.LastClosedDate, meses));

        UsuarioDto? Usuario(int? id) => id is int i ? usuarios.GetValueOrDefault(i) : null;
    }
}
