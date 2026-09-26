using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Counts;

/// <summary>
/// Los códigos de error de los conteos físicos (feature 012, US11; contracts/api.md §12; decisiones-transversales §2.17), con el
/// mensaje en español y el <c>data</c> que la persona necesita para corregir. <c>Inventory.Count.NotFound</c> responde 404 (igual
/// que lo que está fuera del alcance o sin permiso); los demás, 422. El bloqueo de movimientos
/// (<c>Inventory.Count.ProductsLocked</c>) está en <c>InventoryErrors</c> porque lo emite el ciclo común. (nuevo)
/// </summary>
public static class ErroresDeConteos
{
    public const string CodigoProductoFueraDelAlcance = "Inventory.Count.ProductNotInScope";
    public const string CodigoCodigoDeBarrasInexistente = "Inventory.Barcode.NotFound";

    /// <summary>El campo de los criterios del alcance, para <c>Inventory.Document.FieldRequired</c>.</summary>
    public const string CampoCriterios = "criteria";

    public static Error NotFound() => new("Inventory.Count.NotFound", "El conteo no existe.");

    /// <summary>Un producto sólo puede estar en un conteo abierto por bodega.</summary>
    public static Error Overlaps(Guid countPublicId, IReadOnlyList<string> products) => new ErrorConDatos("Inventory.Count.Overlaps",
        $"{Lista(products)} ya está en otro conteo abierto de la bodega. Cierre o descarte ése antes de abrir éste.",
        new { countPublicId, products });

    public static Error EmptyScope() => new("Inventory.Count.EmptyScope",
        "El alcance del conteo no tiene productos en la bodega: revise la categoría, la ubicación o la selección.");

    /// <summary>El conteo por clase ABC llega en I6.</summary>
    public static Error ScopeNotAvailable(CountScope scope) => new ErrorConDatos("Inventory.Count.ScopeNotAvailable",
        "El conteo por clase ABC todavía no está disponible.", new { scope = scope.ToString() });

    public static Error NotOpen() => new("Inventory.Count.NotOpen",
        "El conteo no está abierto: se abre con su foto antes de capturar, y después de cerrarlo ya no se captura.");

    /// <summary>Abrir un conteo que ya tiene foto (nuevo).</summary>
    public static Error AlreadyOpen(DateTime snapshotAt) => new ErrorConDatos("Inventory.Count.AlreadyOpen",
        "El conteo ya está abierto: su foto es fija.", new { snapshotAt });

    public static Error CounterNotAssigned() => new("Inventory.Count.CounterNotAssigned",
        "Usted no es uno de los contadores declarados en este conteo.");

    /// <summary>Una línea con diferencia fuera de tolerancia sin su reconteo (<c>data.lines[]</c>).</summary>
    public sealed record LineaPorRecontar(Guid ProductPublicId, string ProductCode, Guid LocationPublicId, string LocationCode,
        decimal Theoretical, decimal Counted, decimal Difference);

    public static Error RecountRequired(IReadOnlyList<LineaPorRecontar> lines) => new ErrorConDatos("Inventory.Count.RecountRequired",
        $"{lines.Count} línea(s) con diferencia por encima de la tolerancia: capture la ronda 2 de esas líneas y cierre otra vez.",
        new { lines });

    public static Error AdjustmentInProgress(IReadOnlyList<Guid> documents) => new ErrorConDatos("Inventory.Count.AdjustmentInProgress",
        "El conteo ya tiene su ajuste generado.", new { documents });

    /// <summary>Una ronda que no sigue a la anterior, o una ronda de reconteo para una línea que no lo pide (nuevo).</summary>
    public static Error RoundNotOpen(byte round, byte currentRound) => new ErrorConDatos("Inventory.Count.RoundNotOpen",
        round > currentRound
            ? $"La ronda {round} no está abierta: la ronda en curso es la {currentRound}."
            : $"La ronda {round} ya terminó: la ronda en curso es la {currentRound}.",
        new { round, currentRound });

    /// <summary>La lectura cuyo producto no está en el criterio del conteo (va en <c>rejected[]</c>).</summary>
    public static string MensajeFueraDelAlcance(string productCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{productCode} no está en el alcance de este conteo.");

    /// <summary>La lectura de reconteo de una línea que no lo pide.</summary>
    public static string MensajeSinReconteo(string productCode) =>
        string.Create(CultureInfo.InvariantCulture, $"{productCode} no tiene reconteo pendiente: su diferencia está dentro de la tolerancia.");

    private static string Lista(IReadOnlyList<string> productos) =>
        productos.Count <= 3 ? string.Join(", ", productos) : $"{string.Join(", ", productos.Take(3))} y {productos.Count - 3} más";
}
