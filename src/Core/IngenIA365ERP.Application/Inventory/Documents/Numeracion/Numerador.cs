using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Numeracion;

/// <summary>
/// El único que numera un documento de inventario no fiscal y toda nota (feature 012, T16, T139; FR-038; data-model
/// §5.9). Lo llama la confirmación <b>después</b> del cerrojo de existencias (la fila de numeración es la última del
/// orden canónico) y dentro de la misma transacción:
/// <list type="number">
/// <item>busca la secuencia vigente del tipo a la fecha de operación (sin seguirla);</item>
/// <item>la bloquea con <see cref="ICerrojoDeInventario.BloquearNumeracionAsync"/>;</item>
/// <item>la carga ya bloqueada —así lee el <c>NextValue</c> que dejó el último que confirmó—, copia su prefijo y su
/// número al documento e incrementa <c>NextValue</c> por EF; el <c>SaveChanges</c> de la confirmación lo guarda todo
/// junto: sin huecos ni repetidos, y el borrador nunca consume.</item>
/// </list>
/// Sin secuencia vigente: <c>Inventory.Numbering.SequenceMissing</c>. Los tipos fiscales con resolución numeran con
/// <c>NumeradorFiscal</c> (I4). Fuera de éste y de aquél nadie escribe <c>Number</c> ni <c>NextValue</c>
/// (<c>SoloElNumeradorNumera</c>).
/// </summary>
public sealed class Numerador(IApplicationDbContext db, ICerrojoDeInventario cerrojo)
{
    /// <summary>Asigna prefijo y número a <paramref name="documento"/> con la secuencia vigente de su tipo.</summary>
    /// <param name="documentTypeCode">El código del tipo, para el mensaje de error.</param>
    public async Task<Result> NumerarAsync(InventoryDocument documento, string documentTypeCode, CancellationToken ct = default)
    {
        if (documento.Number is not null)
            throw new InvalidOperationException($"El documento {documento.PublicId} ya tiene número.");

        var fecha = documento.OperationDate;
        var vigenteId = await db.DocumentSequences.AsNoTracking()
            .Where(s => s.DocumentTypeId == documento.DocumentTypeId && s.ValidFrom <= fecha && (s.ValidTo == null || s.ValidTo >= fecha))
            .OrderByDescending(s => s.ValidFrom)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (vigenteId is null)
            return Result.Failure(InventoryErrors.SequenceMissing(documentTypeCode, fecha));

        await cerrojo.BloquearNumeracionAsync(vigenteId.Value, ct);

        var secuencia = await db.DocumentSequences.FirstAsync(s => s.Id == vigenteId.Value, ct);
        documento.Prefix = secuencia.Prefix;
        documento.Number = secuencia.NextValue;
        secuencia.NextValue = secuencia.NextValue + 1;
        return Result.Success();
    }

    /// <summary>
    /// Mueve el siguiente número de una secuencia al cambiar de número o reabrir un prefijo desde
    /// <c>AddDocumentSequenceCommand</c> (T150). Vive aquí para que el consecutivo tenga un solo escritor
    /// (<c>SoloElNumeradorNumera</c>); quien lo llama ya comprobó que <paramref name="siguiente"/> queda por encima de lo
    /// emitido con ese prefijo (<c>Inventory.Sequence.NumberAlreadyIssued</c>).
    /// </summary>
    public static void AjustarSiguiente(DocumentSequence secuencia, long siguiente)
    {
        ArgumentNullException.ThrowIfNull(secuencia);
        if (siguiente < 1) throw new ArgumentOutOfRangeException(nameof(siguiente), siguiente, "El siguiente número es 1 o mayor.");
        secuencia.NextValue = siguiente;
    }
}
