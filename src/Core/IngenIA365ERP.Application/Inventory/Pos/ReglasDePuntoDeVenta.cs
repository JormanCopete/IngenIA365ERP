using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Catalog;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Pos;

/// <summary>Lo que se pide de un punto, ya resuelto (el alta una a una y la plantilla 10). (nuevo)</summary>
public sealed record DatosDePunto(string Code, string Name, Branch Sucursal, SalesChannel Canal, bool PosEnabled, Warehouse Bodega, string? Address, bool IsActive);

/// <summary>Lo que se pide de una caja, ya resuelto (el alta una a una y la plantilla 10). (nuevo)</summary>
public sealed record DatosDeCaja(
    string Code,
    string Name,
    Warehouse Bodega,
    CardTerminal? Datafono,
    CashRegisterPrintFormat Formato,
    IReadOnlyList<(CashRegisterDocumentRole Rol, InventoryDocumentType Tipo)> Tipos,
    string? Placa,
    byte? Copias,
    bool IsActive);

/// <summary>
/// La regla única de puntos de venta y cajas (feature 012, I3, T593, T595; contracts/api.md §20.1; plantilla 10; FR-058, FR-067):
/// la usan los comandos una a una y <c>ImportPointsOfSaleCommand</c>, así la importación no tiene reglas propias. El código de un
/// punto no cambia (dimensión <c>PointOfSaleCode</c> de la matriz, T27); la bodega por defecto del punto y la de cada caja son
/// operativas y de la sucursal del punto; una caja lleva a lo sumo un tipo por rol, de una clase que el rol admite
/// (<see cref="CashRegisterDocumentType.ClasesDelRol"/>) y, en el rol de venta POS y su nota, la que corresponde a si la
/// cooperativa está obligada a facturar electrónicamente (<c>Dian.ObligadaAFacturar</c>); cada tipo admite la bodega de la caja,
/// y con factura a petición la caja lleva su nota crédito. Las contingencias exigen además una resolución de contingencia con su
/// <c>BacksUpKind</c> cuando I4 registre resoluciones: hoy no existen y la regla sólo mira la clase. No guarda. (nuevo)
/// </summary>
public static class ReglasDePuntoDeVenta
{
    public const int LargoDeNombreDePunto = 80;
    public const int LargoDeNombreDeCaja = 60;
    public const int LargoDeDireccion = 120;
    public const int LargoDePlaca = 30;

    /// <summary>¿La cooperativa está obligada a facturar electrónicamente hoy? Sin vigencia, sí (el defecto del parámetro).</summary>
    public static async Task<bool> ObligadaAFacturarAsync(ILectorDeParametros parametros, DateOnly hoy, CancellationToken ct)
    {
        var r = await parametros.LeerComoAsync<bool>(ParametrosDeFacturacionElectronica.Modulo, ParametrosDeFacturacionElectronica.ObligadaAFacturar, hoy, ct: ct);
        return r.IsFailure || r.Value;
    }

    /// <summary>Las clases que admite el rol en esta cooperativa: la tabla de §20.1, estrechada por la obligación de facturar.</summary>
    public static IReadOnlyList<DocumentClass> ClasesAdmitidas(CashRegisterDocumentRole rol, bool obligada) => rol switch
    {
        CashRegisterDocumentRole.PosSale => [obligada ? DocumentClass.PosEquivalentDocument : DocumentClass.NonElectronicSalesReceipt],
        CashRegisterDocumentRole.PosAdjustmentNote => [obligada ? DocumentClass.PosAdjustmentNote : DocumentClass.NonElectronicSalesNote],
        _ => ErroresDePuntoDeVenta.ClasesDe(rol),
    };

    // ------------------------------------------------------------------------------------------------- puntos --

    /// <summary>El alta de un punto: código único, bodega operativa y de la misma sucursal. Deja el punto en el contexto.</summary>
    public static async Task<Result<PointOfSale>> AltaDePuntoAsync(IApplicationDbContext db, DatosDePunto d, CancellationToken ct)
    {
        var codigo = CodigoDeCatalogo.Normalizar(d.Code)!;
        var local = db.PointsOfSale.Local.FirstOrDefault(p => p.Code == codigo && !p.IsDeleted);
        if (local is not null) return Result.Failure<PointOfSale>(CodigoDeCatalogo.Duplicado("un punto de venta", codigo, local.Name, local.PublicId));
        var otro = await db.PointsOfSale.Where(p => p.Code == codigo && !p.IsDeleted).Select(p => new { p.PublicId, p.Name }).FirstOrDefaultAsync(ct);
        if (otro is not null) return Result.Failure<PointOfSale>(CodigoDeCatalogo.Duplicado("un punto de venta", codigo, otro.Name, otro.PublicId));
        if (BodegaDelPunto(d.Bodega, d.Sucursal.Id) is { } e) return Result.Failure<PointOfSale>(e);

        var punto = new PointOfSale { Code = codigo };
        Asignar(punto, d);
        db.PointsOfSale.Add(punto);
        return Result.Success(punto);
    }

    /// <summary>
    /// La edición de un punto: nombre, canal, POS, bodega, dirección y activo. La sucursal y el código no cambian; con una sesión
    /// abierta no se desactiva (<c>Inventory.PointOfSale.HasOpenSessions</c>).
    /// </summary>
    public static async Task<Result> ActualizarPuntoAsync(IApplicationDbContext db, PointOfSale punto, DatosDePunto d, CancellationToken ct)
    {
        if (BodegaDelPunto(d.Bodega, punto.BranchId) is { } e) return Result.Failure(e);
        if (punto.IsActive && !d.IsActive && punto.Id != 0)
        {
            var abiertas = await db.CashSessions.CountAsync(s => s.PointOfSaleId == punto.Id && s.Status == CashSessionStatus.Open && !s.IsDeleted, ct);
            if (abiertas > 0) return Result.Failure(ErroresDePuntoDeVenta.HasOpenSessions(abiertas));
        }
        Asignar(punto, d);
        return Result.Success();
    }

    private static Error? BodegaDelPunto(Warehouse bodega, int sucursalId)
    {
        if (bodega.EsTransito) return ErroresDePuntoDeVenta.TransitWarehouse(bodega.Code);
        return bodega.BranchId != sucursalId ? ErroresDePuntoDeVenta.PointWarehouseBranchMismatch(bodega.Code) : null;
    }

    private static void Asignar(PointOfSale punto, DatosDePunto d)
    {
        if (punto.Id == 0) punto.BranchId = d.Sucursal.Id;
        punto.Name = d.Name.Trim();
        punto.SalesChannelId = d.Canal.Id;
        punto.SalesChannel = d.Canal;
        punto.PosEnabled = d.PosEnabled;
        punto.DefaultWarehouseId = d.Bodega.Id;
        punto.DefaultWarehouse = d.Bodega;
        punto.Address = string.IsNullOrWhiteSpace(d.Address) ? null : d.Address.Trim();
        punto.IsActive = d.IsActive;
    }

    // -------------------------------------------------------------------------------------------------- cajas --

    /// <summary>
    /// Deja la caja (nueva, o la existente modificada) y sus tipos por rol en el contexto. El código de una caja es único en la
    /// cooperativa (la llave de la plantilla 10). Cambiar el tipo de un rol lo cambia en su fila (el diff de auditoría guarda el
    /// anterior; los documentos ya emitidos guardan su propio tipo); un rol que ya no viene se da de baja.
    /// </summary>
    public static async Task<Result<CashRegister>> AplicarCajaAsync(
        IApplicationDbContext db, PointOfSale punto, CashRegister? existente, DatosDeCaja d, bool obligada, DateTime ahora, CancellationToken ct)
    {
        if (existente is null)
        {
            var codigo = CodigoDeCatalogo.Normalizar(d.Code)!;
            var local = db.CashRegisters.Local.FirstOrDefault(c => c.Code == codigo && !c.IsDeleted);
            if (local is not null) return Result.Failure<CashRegister>(CodigoDeCatalogo.Duplicado("una caja", codigo, local.Name, local.PublicId));
            var otra = await db.CashRegisters.Where(c => c.Code == codigo && !c.IsDeleted).Select(c => new { c.PublicId, c.Name }).FirstOrDefaultAsync(ct);
            if (otra is not null) return Result.Failure<CashRegister>(CodigoDeCatalogo.Duplicado("una caja", codigo, otra.Name, otra.PublicId));
        }

        if (d.Bodega.EsTransito) return Result.Failure<CashRegister>(ErroresDePuntoDeVenta.TransitWarehouse(d.Bodega.Code));
        if (d.Bodega.BranchId != punto.BranchId) return Result.Failure<CashRegister>(ErroresDePuntoDeVenta.WarehouseBranchMismatch(d.Bodega.Code));

        var roles = new HashSet<CashRegisterDocumentRole>();
        foreach (var (rol, tipo) in d.Tipos)
        {
            if (!roles.Add(rol)) return Result.Failure<CashRegister>(ErroresDePuntoDeVenta.RoleDuplicate(rol));
            var admitidas = ClasesAdmitidas(rol, obligada);
            if (!admitidas.Contains(tipo.Class)) return Result.Failure<CashRegister>(ErroresDePuntoDeVenta.RoleClassMismatch(rol, tipo.Code, tipo.Class, admitidas));
            if (!tipo.AllWarehouses && d.Bodega.Id != 0 && tipo.Warehouses.Where(w => !w.IsDeleted).All(w => w.WarehouseId != d.Bodega.Id))
                return Result.Failure<CashRegister>(InventoryErrors.WarehouseNotAllowedForType(d.Bodega.Code, tipo.Code));
        }
        if (roles.Contains(CashRegisterDocumentRole.InvoiceOnRequest) && !roles.Contains(CashRegisterDocumentRole.InvoiceCreditNote))
            return Result.Failure<CashRegister>(ErroresDePuntoDeVenta.RoleRequired(CashRegisterDocumentRole.InvoiceCreditNote, CashRegisterDocumentRole.InvoiceOnRequest));

        var caja = existente ?? new CashRegister { Code = CodigoDeCatalogo.Normalizar(d.Code)! };
        caja.PointOfSale = punto;
        if (punto.Id != 0) caja.PointOfSaleId = punto.Id;
        caja.Name = d.Name.Trim();
        caja.WarehouseId = d.Bodega.Id;
        caja.DefaultCardTerminalId = d.Datafono?.Id;
        caja.ReceiptWidthMm = (short)d.Formato;
        caja.DianCashRegisterPlate = string.IsNullOrWhiteSpace(d.Placa) ? null : d.Placa.Trim();
        if (d.Copias is { } copias) caja.PrintCopies = copias;
        caja.IsActive = d.IsActive;
        if (existente is null) db.CashRegisters.Add(caja);

        var vigentes = caja.Id == 0
            ? []
            : await db.CashRegisterDocumentTypes.Where(t => t.CashRegisterId == caja.Id && !t.IsDeleted).ToListAsync(ct);
        foreach (var fila in vigentes)
        {
            var pedido = d.Tipos.FirstOrDefault(t => t.Rol == fila.Role);
            if (pedido.Tipo is null)
            {
                fila.IsDeleted = true;
                fila.DeletedAt = ahora;
            }
            else if (fila.DocumentTypeId != pedido.Tipo.Id)
            {
                fila.DocumentTypeId = pedido.Tipo.Id;
                fila.DocumentType = pedido.Tipo;
            }
        }
        foreach (var (rol, tipo) in d.Tipos.Where(t => vigentes.All(v => v.Role != t.Rol)))
            db.CashRegisterDocumentTypes.Add(new CashRegisterDocumentType { CashRegister = caja, Role = rol, DocumentTypeId = tipo.Id, DocumentType = tipo });
        return Result.Success(caja);
    }

    /// <summary>El formato de impresión guardado (el ancho del papel); uno desconocido se lee como tirilla de 80.</summary>
    public static CashRegisterPrintFormat FormatoDe(short anchoMm) =>
        Enum.IsDefined(typeof(CashRegisterPrintFormat), (int)anchoMm) ? (CashRegisterPrintFormat)anchoMm : CashRegisterPrintFormat.Ticket80;
}
