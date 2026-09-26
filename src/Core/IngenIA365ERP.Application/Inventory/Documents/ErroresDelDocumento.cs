using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Lo que el cuerpo de un borrador referencia y no existe (o está fuera del alcance: el mismo 404, contracts/api.md
/// §2.2). Los demás códigos del ciclo están en <c>InventoryErrors</c>. (nuevo)
/// </summary>
public static class ErroresDelDocumento
{
    public static Error ProductoInexistente() => new("Inventory.Product.NotFound", "El producto no existe.");

    public static Error UbicacionInexistente() => new("Inventory.Location.NotFound", "La ubicación no existe.");

    public static Error CausaInexistente() => new("Inventory.AdjustmentCause.NotFound", "La causa de ajuste no existe.");

    public static Error CanalInexistente() => new("Inventory.SalesChannel.NotFound", "El canal de venta no existe.");

    public static Error CentroDeCostoInexistente() => new("Core.CostCenter.NotFound", "El centro de costo no existe.");

    public static Error PersonaInexistente() => new("Core.Person.NotFound", "La persona no existe.");

    /// <summary>Sin persona resuelta en <c>SEC_Users</c> no se sabe quién registra: nunca se confunde con otro.</summary>
    public static Error SinUsuario() => new("Inventory.Document.Unauthorized",
        "No se pudo identificar a la persona que hace la operación en esta cooperativa.");
}
