namespace IngenIA365ERP.Application.Inventory.Reports;

/// <summary>
/// Lo que una vista de <c>/api/reports/inventory/{vista}</c> declara al registrarse (feature 012, T44, T182;
/// contracts/api.md §27, decisiones-transversales §2.12): la pantalla <c>/inventario/informes</c> arma su selector con
/// estas entradas (<c>GET /api/reports/inventory</c>) y la ruta toma de aquí sus permisos. Cada historia registra las
/// suyas con <c>MapVistaDeInventario</c>; la base arranca sin ninguna. (nuevo)
/// </summary>
/// <param name="Key">El nombre de la vista en la ruta (<c>kardex</c>, <c>stock</c>, <c>sales-by-register</c>…).</param>
/// <param name="Name">El nombre en pantalla.</param>
/// <param name="Description">Una línea: qué muestra y cómo se lee.</param>
/// <param name="FileName">El nombre base del archivo exportado (<c>kardex</c>, <c>valorizado</c>…).</param>
/// <param name="Filters">Los filtros comunes que usa (nombres de <see cref="FiltrosDeInformeDeInventario"/> en camelCase), para que la pantalla muestre sólo esos.</param>
/// <param name="OwnFilters">Los filtros propios (<c>groupBy</c>, <c>count</c>, <c>days</c>…), que la pantalla deja pasar por la query.</param>
/// <param name="PersonalData">La vista trae datos de clientes: exportarla exige además <c>Inventory.Reports.ExportPersonalData</c> (§27, «(PD)»).</param>
/// <param name="PersonalDataWhen">Datos personales sólo con un filtro propio: <c>groupBy=customer</c>, <c>by=customer</c>. Nulo si <paramref name="PersonalData"/> decide.</param>
/// <param name="RequiredPermission">Un permiso además de <c>Inventory.Reports.View</c> (<c>Inventory.Costs.Read</c> en <c>valuation</c>, <c>Inventory.Reconciliation.View</c>…).</param>
public sealed record VistaDeInformeDeInventario(
    string Key,
    string Name,
    string Description,
    string FileName,
    IReadOnlyList<string> Filters,
    IReadOnlyList<string>? OwnFilters = null,
    bool PersonalData = false,
    string? PersonalDataWhen = null,
    string? RequiredPermission = null)
{
    /// <summary>
    /// Si la petición exporta datos personales: siempre con <see cref="PersonalData"/>; con <see cref="PersonalDataWhen"/>
    /// (<c>parametro=valor</c>), cuando ese parámetro de la query trae ese valor (sin distinguir mayúsculas).
    /// </summary>
    public bool TraeDatosPersonales(Func<string, string?> parametro)
    {
        if (PersonalData) return true;
        if (string.IsNullOrWhiteSpace(PersonalDataWhen)) return false;
        var partes = PersonalDataWhen.Split('=', 2, StringSplitOptions.TrimEntries);
        return partes.Length == 2 && string.Equals(parametro(partes[0])?.Trim(), partes[1], StringComparison.OrdinalIgnoreCase);
    }
}
