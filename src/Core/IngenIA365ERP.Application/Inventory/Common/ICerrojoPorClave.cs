namespace IngenIA365ERP.Application.Inventory.Common;

/// <summary>
/// Un candado exclusivo por clave de texto, dentro de la transacción en curso (feature 012, I3, T597; data-model §14
/// «el comando lo verifica tomando bloqueo de actualización sobre las listas de ese <c>ScopeKey</c>»). Sirve donde la regla
/// es «no se cruzan en el tiempo» y el índice único sólo protege el mismo <c>ValidFrom</c>: dos altas del mismo ámbito con
/// fechas distintas se serializan en la clave y la segunda ve la primera. La implementación vive en
/// <c>Persistence/Inventory/CerrojoPorClave</c> (<c>sp_getapplock</c> en SQL Server, <c>pg_advisory_xact_lock</c> en
/// PostgreSQL); sin transacción no hay nada que proteger y no bloquea (la revisión de una plantilla). (nuevo)
/// </summary>
public interface ICerrojoPorClave
{
    /// <summary>Toma el candado de <paramref name="clave"/> hasta el fin de la transacción.</summary>
    Task BloquearAsync(string clave, CancellationToken ct = default);
}

/// <summary>Las claves de los candados, con su espacio para que no se pisen entre tablas. (nuevo)</summary>
public static class ClavesDeCerrojo
{
    public static string AmbitoDeLista(string scopeKey) => $"INV_PriceLists:{scopeKey}";

    public static string TopeDelRol(int roleId) => $"INV_DiscountCaps:{roleId}";

    /// <summary>La fila del punto de venta: abrir una sesión y cerrar el día se serializan por punto (I3, T617, T620). (nuevo)</summary>
    public static string PuntoDeVenta(int pointOfSaleId) => $"INV_PointsOfSale:{pointOfSaleId}";
}
