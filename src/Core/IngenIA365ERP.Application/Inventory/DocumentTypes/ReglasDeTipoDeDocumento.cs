using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Documents;

namespace IngenIA365ERP.Application.Inventory.DocumentTypes;

/// <summary>Las marcas del tipo que dependen de su clase. (nuevo)</summary>
public sealed record MarcasDelTipo(bool IsTaxableWithdrawal, bool VatNonDeductible, bool AllowsFutureDate);

/// <summary>
/// Las reglas que comparten el alta y la edición de un tipo (feature 012, T150; contracts/api.md §8): las marcas sólo en
/// su clase (<c>Inventory.DocumentType.FlagNotApplicable</c>), ninguna bodega de tránsito
/// (<c>Inventory.DocumentType.TransitNotAllowed</c>), bodegas y canal existentes (si no, 404), y el formato del prefijo.
/// Las reutilizará la importación de la plantilla 8 (T153). (nuevo)
/// </summary>
public static class ReglasDeTipoDeDocumento
{
    public const string PatronDePrefijo = "^[A-Za-z0-9]{0,4}$";
    public const string MensajeDePrefijo = "El prefijo tiene hasta 4 letras o dígitos, sin espacios (puede ir vacío).";

    /// <summary>El prefijo como se guarda: recortado y en mayúsculas; vacío si no viene.</summary>
    public static string Prefijo(string? prefijo) => (prefijo ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>La clase se elige al crear: debe ser operable en este despliegue.</summary>
    public static Result ClaseDisponible(DocumentClass clase) =>
        ClasesDeDocumento.De(clase).Operable() ? Result.Success() : Result.Failure(InventoryErrors.DocumentClassNotAvailable(clase));

    /// <summary>Cada marca vale sólo en su clase: retiro gravado en consumo interno, IVA al costo en compras, fecha futura en no fiscales.</summary>
    public static Result Marcas(DocumentClass clase, MarcasDelTipo marcas)
    {
        var descripcion = ClasesDeDocumento.De(clase);
        if (marcas.IsTaxableWithdrawal && clase != DocumentClass.InternalConsumption)
            return Result.Failure(InventoryErrors.FlagNotApplicable("isTaxableWithdrawal", clase));
        if (marcas.VatNonDeductible && descripcion.Group != DocumentClassGroup.Purchases)
            return Result.Failure(InventoryErrors.FlagNotApplicable("vatNonDeductible", clase));
        if (marcas.AllowsFutureDate && descripcion.IsFiscal)
            return Result.Failure(InventoryErrors.FlagNotApplicable("allowsFutureDate", clase));
        return Result.Success();
    }

    /// <summary>Las bodegas permitidas pedidas: existen (si no, 404) y ninguna es de tránsito.</summary>
    public static async Task<Result<IReadOnlyList<BodegaDelDocumento>>> BodegasAsync(
        IMaestrosDelDocumento maestros, IReadOnlyCollection<Guid> pedidas, CancellationToken ct)
    {
        var distintas = pedidas.Distinct().ToList();
        if (distintas.Count == 0) return Result.Success<IReadOnlyList<BodegaDelDocumento>>([]);
        var halladas = await maestros.BodegasAsync(distintas, ct);
        if (halladas.Count != distintas.Count) return Result.Failure<IReadOnlyList<BodegaDelDocumento>>(ErroresDeAlcance.BodegaInexistente());
        if (halladas.Any(b => b.EsTransito)) return Result.Failure<IReadOnlyList<BodegaDelDocumento>>(InventoryErrors.TypeTransitNotAllowed());
        return Result.Success(halladas);
    }

    /// <summary>El canal del tipo, si viene: existe (si no, 404).</summary>
    public static async Task<Result<int?>> CanalAsync(IMaestrosDelDocumento maestros, Guid? canal, CancellationToken ct)
    {
        if (canal is not { } publicId) return Result.Success<int?>(null);
        var hallado = await maestros.CanalDeVentaAsync(publicId, ct);
        return hallado is null ? Result.Failure<int?>(ErroresDelDocumento.CanalInexistente()) : Result.Success<int?>(hallado.Id);
    }
}
