using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>El borrador que se está guardando: la cabecera y las líneas ya armadas, su tipo, lo pedido y la bodega. (nuevo)</summary>
public sealed record BorradorEnCurso(InventoryDocument Documento, InventoryDocumentType Tipo, SaveInventoryDraftRequest Pedido, BodegaDelDocumento? Bodega);

/// <summary>
/// Lo que agrega el grupo al guardar: avisos que sólo él conoce y la vista previa de los impuestos (compras: <c>taxLines</c>
/// del borrador, que no se guardan hasta confirmar). (nuevo)
/// </summary>
public sealed record ResultadoDelBorrador(IReadOnlyList<Error> Avisos, IReadOnlyList<DocumentTaxLineDto> ImpuestosPrevistos)
{
    public static ResultadoDelBorrador Vacio { get; } = new([], []);
}

/// <summary>
/// Lo que un grupo agrega al guardado del borrador del ciclo común (feature 012, US9, T339; contracts/api.md §9.3, §14.1):
/// el de compras completa las líneas desde su origen (recepción o factura), escribe el documento del proveedor, los vínculos
/// con sus orígenes, el municipio, los impuestos y los totales. Un grupo sin implementación no admite los campos de compras
/// (<c>Validation.Invalid</c>). <c>SaveInventoryDraftCommand</c> lo llama; ninguno guarda. (nuevo)
/// </summary>
public interface IBorradorDeGrupo
{
    DocumentClassGroup Grupo { get; }

    /// <summary>
    /// Antes de resolver las líneas: rechaza los campos que la clase no admite y completa lo que la línea toma de su origen
    /// (producto, unidad, cantidad). <paramref name="existente"/> es el borrador que se reemplaza, si lo hay.
    /// </summary>
    Task<Result<SaveInventoryDraftRequest>> PrepararAsync(InventoryDocumentType tipo, SaveInventoryDraftRequest pedido, InventoryDocument? existente, CancellationToken ct);

    /// <summary>Después de armar cabecera y líneas, antes del guardado: satélites, vínculos, impuestos y totales.</summary>
    Task<Result<ResultadoDelBorrador>> AplicarAsync(BorradorEnCurso borrador, CancellationToken ct);

    /// <summary>La violación de un índice propio (una carrera) como su error; nulo si no es suya.</summary>
    Task<Error?> TraducirColisionAsync(DbUpdateException ex, BorradorEnCurso borrador, CancellationToken ct);
}
