using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Cash;

// 2026-11-23 (pedido del dueño), caja única (Caja.SesionUnica): la sesión del punto de venta vinculada a una sesión de Recaudo. Abre con base cero
// cuando abre la de Recaudo y no se cierra por su cuenta: la cierra, cuadrada, el arqueo de Recaudo (CajaUnica), que se lleva lo recibido para
// contarlo junto. Estas consultas sólo las usa ese coordinador, del lado del servidor: no tienen ruta. (nuevo)

/// <summary>Lo que recibió la sesión vinculada por un medio, con lo esperado sin ocultar (para el coordinador, nunca para la pantalla). (nuevo)</summary>
public sealed record LoDelPuntoDeVentaDto(int PaymentMeansId, Guid PaymentMeansPublicId, string Code, PaymentMeansClass Class, CashCountMethod CountMethod,
    decimal Expected);

/// <summary>La sesión del punto de venta vinculada a una de Recaudo. (nuevo)</summary>
public sealed record SesionVinculadaDto(Guid PublicId, string CashRegisterCode, string PointOfSaleName, bool Open, IReadOnlyList<LoDelPuntoDeVentaDto> Means);

/// <summary>La sesión del punto de venta vinculada a la sesión de Recaudo, la más reciente, o nada. (nuevo)</summary>
public sealed record GetLinkedCashSessionQuery(Guid ReceiptsSessionPublicId) : IRequest<Result<SesionVinculadaDto?>>;

public sealed class GetLinkedCashSessionQueryHandler(IApplicationDbContext db, SesionesDeCaja sesiones)
    : IRequestHandler<GetLinkedCashSessionQuery, Result<SesionVinculadaDto?>>
{
    public async Task<Result<SesionVinculadaDto?>> Handle(GetLinkedCashSessionQuery request, CancellationToken ct)
    {
        var sesion = await db.CashSessions.AsNoTracking().Where(s => s.LinkedSessionPublicId == request.ReceiptsSessionPublicId)
            .OrderByDescending(s => s.Id).FirstOrDefaultAsync(ct);
        return Result.Success(sesion is null ? null : await CajaVinculada.DtoAsync(db, sesiones, sesion, ct));
    }
}

/// <summary>Lo común de la sesión vinculada. (nuevo)</summary>
public static class CajaVinculada
{
    /// <remarks>
    /// Abierta, lo que recibió es su esperado en vivo. Cerrada, es lo que contó su cierre cuadrado (2026-11-25): después del cierre el retiro a la caja
    /// fuerte deja el efectivo esperado en cero, y el ingreso a Recaudo que se repetía con ese esperado entraba sin el efectivo o chocaba con su clave.
    /// </remarks>
    public static async Task<SesionVinculadaDto> DtoAsync(IApplicationDbContext db, SesionesDeCaja sesiones, CashSession sesion, CancellationToken ct)
    {
        var caja = await db.CashRegisters.AsNoTracking().Where(c => c.Id == sesion.CashRegisterId).Select(c => c.Code).FirstOrDefaultAsync(ct) ?? string.Empty;
        var punto = await db.PointsOfSale.AsNoTracking().Where(p => p.Id == sesion.PointOfSaleId).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
        if (!sesion.EstaAbierta && await LoContadoAlCerrarAsync(db, sesion.Id, ct) is { } contado)
            return new SesionVinculadaDto(sesion.PublicId, caja, punto, false, contado);
        var esperado = await sesiones.EsperadoAsync(sesion, null, ct);
        var ids = esperado.Lines.Select(l => l.Medio.PaymentMeansId).ToList();
        var publicos = await db.PaymentMeans.AsNoTracking().IgnoreQueryFilters().Where(m => ids.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.PublicId, ct);
        return new SesionVinculadaDto(sesion.PublicId, caja, punto, sesion.EstaAbierta,
            esperado.Lines.Select(l => new LoDelPuntoDeVentaDto(l.Medio.PaymentMeansId, publicos.GetValueOrDefault(l.Medio.PaymentMeansId), l.Medio.Code,
                l.Medio.Class, l.Medio.CountMethod, l.Expected)).ToList());
    }

    /// <summary>Lo contado por medio en el último arqueo de la sesión cerrada, o nulo si no tiene.</summary>
    private static async Task<IReadOnlyList<LoDelPuntoDeVentaDto>?> LoContadoAlCerrarAsync(IApplicationDbContext db, int sesionId, CancellationToken ct)
    {
        var arqueo = await db.CashCounts.AsNoTracking().Where(c => c.CashSessionId == sesionId && !c.IsDeleted).OrderByDescending(c => c.Id)
            .Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        if (arqueo is not { } id) return null;
        return await (from l in db.CashCountLines.AsNoTracking()
                      join m in db.PaymentMeans.AsNoTracking().IgnoreQueryFilters() on l.PaymentMeansId equals m.Id
                      where l.CashCountId == id && !l.IsDeleted
                      orderby m.DisplayOrder, m.Code
                      select new LoDelPuntoDeVentaDto(m.Id, m.PublicId, m.Code, m.Class, m.CountMethod, l.CountedAmount))
            .ToListAsync(ct);
    }

    /// <summary>
    /// El cierre cuadrado de la sesión vinculada: cada medio que se arquea, contado por lo esperado (no hay diferencia que tratar aquí: la diferencia la
    /// encuentra el arqueo de Recaudo, que cuenta todo junto), y el efectivo a la caja fuerte con el retiro de cierre.
    /// </summary>
    public static CloseCashSessionCommand Cierre(SesionVinculadaDto sesion, Guid clave) =>
        new(sesion.PublicId,
            sesion.Means.Where(m => m.CountMethod != CashCountMethod.None)
                .Select(m => new CashCountInput(m.PaymentMeansPublicId, CountedTotal: Math.Max(0m, m.Expected))).ToList(),
            new ClosingWithdrawalInput(CashMovementDestination.Safe))
        {
            OperationKey = clave,
            DesdeLaCajaUnica = true,
        };
}
