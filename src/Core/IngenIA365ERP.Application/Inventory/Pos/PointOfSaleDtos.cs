using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Pos;

/// <summary>
/// El formato de impresión de una caja (contracts/api.md §20.1 <c>printFormat</c>; plantilla 10 <c>impresion</c>: <c>Tirilla80</c>,
/// <c>Carta</c>). El valor es el ancho del papel en milímetros y se guarda tal cual en <c>INV_CashRegisters.ReceiptWidthMm</c>
/// (58 y 80 son tirillas; 216 es la carta). No es una columna nueva: es la lectura de la que ya existe. (nuevo)
/// </summary>
public enum CashRegisterPrintFormat
{
    Ticket58 = 58,
    Ticket80 = 80,
    Letter = 216,
}

/// <summary>Un punto de venta (§20.1). <see cref="CashRegisters"/> sólo en el detalle. (nuevo)</summary>
public sealed record PointOfSaleDto(
    Guid PointOfSalePublicId,
    string Code,
    string Name,
    Guid BranchPublicId,
    Guid SalesChannelPublicId,
    bool PosEnabled,
    Guid DefaultWarehousePublicId,
    string? Address,
    bool IsActive,
    int CashRegisterCount)
{
    public IReadOnlyList<CashRegisterDto>? CashRegisters { get; init; }
}

/// <summary>El tipo de documento de una caja en un rol (§20.1). <see cref="Resolution"/> llega con I4. (nuevo)</summary>
public sealed record CashRegisterDocumentTypeDto(
    CashRegisterDocumentRole Role,
    Guid DocumentTypePublicId,
    string DocumentTypeCode,
    DocumentClass Class,
    string? Prefix,
    object? Resolution = null);

/// <summary>La sesión abierta de una caja, si la hay (§20.1). (nuevo)</summary>
public sealed record CashRegisterOpenSessionDto(Guid CashSessionPublicId, string CashierName, DateTime OpenedAt);

/// <summary>Una caja (§20.1). (nuevo)</summary>
public sealed record CashRegisterDto(
    Guid CashRegisterPublicId,
    Guid PointOfSalePublicId,
    string Code,
    string Name,
    Guid WarehousePublicId,
    string WarehouseCode,
    Guid? DefaultCardTerminalPublicId,
    CashRegisterPrintFormat PrintFormat,
    string? DianCashRegisterPlate,
    byte PrintCopies,
    bool IsActive,
    IReadOnlyList<CashRegisterDocumentTypeDto> DocumentTypes,
    CashRegisterOpenSessionDto? OpenSession);

/// <summary>Un tipo de documento por rol, como lo pide la caja (§20.1 <c>documentTypes: [{ role, documentTypePublicId }]</c>). (nuevo)</summary>
public sealed record CashRegisterDocumentTypeInput(CashRegisterDocumentRole Role, Guid DocumentTypePublicId);

/// <summary>
/// El punto de una sesión de caja (<c>CashSessionDto.pointOfSale</c>, contracts/api.md §20.1 y §21.1; FR-058, T605): con
/// <see cref="PosEnabled"/> la pantalla muestra u oculta el enlace a <c>/pos</c>; en un punto sin POS la sesión sigue sirviendo para el
/// cobro de oficina, los movimientos y el arqueo. Lo usa la sesión de caja (T617). (nuevo)
/// </summary>
public sealed record CashSessionPointOfSaleDto(Guid PointOfSalePublicId, string Code, string Name, bool PosEnabled)
{
    public static CashSessionPointOfSaleDto De(Domain.Entities.Inventory.Pos.PointOfSale punto) => new(punto.PublicId, punto.Code, punto.Name, punto.PosEnabled);
}
