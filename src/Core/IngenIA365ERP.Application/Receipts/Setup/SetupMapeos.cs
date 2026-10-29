using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Setup;

/// <summary>Cómo se proyecta la configuración de Recaudo (feature 015, T0072); lo comparten consultas y comandos. (nuevo)</summary>
internal static class SetupMapeos
{
    public static CollectionPointDto Punto(CollectionPoint p, Guid oficina, string nombreDeOficina, int cajas) =>
        new(p.PublicId, oficina, nombreDeOficina, p.Code, p.Name, p.Status, p.ValidFrom, p.ValidTo, p.DocumentPrefix, p.IsMainCashBoxVault, cajas);

    public static async Task<CollectionPointDto> PuntoAsync(IApplicationDbContext db, CollectionPoint p, CancellationToken ct)
    {
        var oficina = await db.Branches.AsNoTracking().Where(b => b.Id == p.BranchId).Select(b => new { b.PublicId, b.Name }).FirstAsync(ct);
        var cajas = await db.CashBoxes.AsNoTracking().CountAsync(c => c.CollectionPointId == p.Id, ct);
        return Punto(p, oficina.PublicId, oficina.Name, cajas);
    }

    public static CashBoxDto Caja(CashBox c, CollectionPoint p, int cajeros) =>
        new(c.PublicId, p.PublicId, p.Code, c.Code, c.Name, c.BaseMode, c.BaseAmount, c.CashWarnCap, c.CashBlockCap, c.DailyCashOutCap, c.IsMainCashBox,
            c.Status, c.ValidFrom, c.ValidTo, cajeros);

    public static Task<int> CajerosVigentesAsync(IApplicationDbContext db, int caja, DateOnly hoy, CancellationToken ct) =>
        db.CashBoxCashiers.AsNoTracking().CountAsync(c => c.CashBoxId == caja && c.ValidFrom <= hoy && (c.ValidTo == null || c.ValidTo >= hoy), ct);

    /// <summary>El código de la caja principal viva de la oficina, sin contar <paramref name="excepto"/>; nulo si no hay.</summary>
    public static Task<string?> CajaPrincipalDeLaOficinaAsync(IApplicationDbContext db, int oficina, int? excepto, CancellationToken ct) =>
        (from c in db.CashBoxes.AsNoTracking()
         join p in db.CollectionPoints.AsNoTracking() on c.CollectionPointId equals p.Id
         where p.BranchId == oficina && c.IsMainCashBox && c.Status == Domain.Enums.Receipts.ReceiptsCatalogStatus.Active && c.Id != (excepto ?? 0)
         select c.Code).FirstOrDefaultAsync(ct);

    /// <summary>El nombre visible de una persona por su Id.</summary>
    public static async Task<string> NombreDePersonaAsync(IApplicationDbContext db, int persona, CancellationToken ct)
    {
        var p = await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == persona)
            .Select(x => new { x.FirstName, x.OtherNames, x.LastName, x.SecondLastName, x.BusinessName }).FirstOrDefaultAsync(ct);
        return p is null ? string.Empty : PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName);
    }
}
