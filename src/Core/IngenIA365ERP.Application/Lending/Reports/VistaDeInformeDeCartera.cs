using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Application.Lending.Reports;

/// <summary>
/// Lo que una vista de <c>/api/reports/lending/{vista}</c> declara al registrarse (feature 013, US15, T0795, T99; contracts/api.md §17,
/// decisiones-transversales §2.15): la pantalla <c>/cartera/informes</c> arma su selector con estas entradas (<c>GET /api/reports/lending</c>)
/// y la ruta toma de aquí sus permisos. Cada entrega registra las suyas con <c>MapVistaDeCartera</c> (K1 las siete de C109; K2–K5 las
/// suyas). Molde: <c>VistaDeInformeDeInventario</c>. (nuevo)
/// </summary>
/// <param name="Key">El nombre de la vista en la ruta (<c>aging</c>, <c>placement</c>, <c>receipts</c>…).</param>
/// <param name="Name">El nombre en pantalla.</param>
/// <param name="Description">Una línea: qué muestra y cómo se lee.</param>
/// <param name="FileName">El nombre base del archivo exportado (<c>cartera-por-edades</c>…).</param>
/// <param name="Filters">Los filtros comunes que usa (nombres de <see cref="FiltrosDeInformeDeCartera"/> en camelCase), para que la pantalla muestre sólo esos.</param>
/// <param name="OwnFilters">Los filtros propios (<c>groupBy</c>, <c>dateKind</c>, <c>ranges</c>…), que la pantalla deja pasar por la query.</param>
/// <param name="PersonalData">
/// La vista trae datos del deudor (api.md §17, «PD»): consultarla exige además <c>Lending.Loans.Read</c> (<see cref="RequiredPermission"/>) y
/// exportarla, <c>Lending.Reports.ExportPersonalData</c>; sin ellos, el mismo 404 y sin auditar (C84).
/// </param>
/// <param name="AvailableFrom">La entrega de Cartera desde la que existe; el registro no ofrece una posterior a <see cref="CatalogoDeCartera.EntregaVigente"/>.</param>
/// <param name="RequiredPermission">Un permiso además de <c>Lending.Reports.View</c>; en las vistas con datos del deudor, <c>Lending.Loans.Read</c>.</param>
public sealed record VistaDeInformeDeCartera(
    string Key,
    string Name,
    string Description,
    string FileName,
    IReadOnlyList<string> Filters,
    IReadOnlyList<string>? OwnFilters = null,
    bool PersonalData = false,
    EntregaDeCartera AvailableFrom = EntregaDeCartera.K1,
    string? RequiredPermission = null)
{
    /// <summary>El permiso que exige toda vista con datos del deudor para consultarla (C84).</summary>
    public const string PermisoDeDatosDelDeudor = "Lending.Loans.Read";

    /// <summary>Una vista con datos del deudor: consultarla exige <see cref="PermisoDeDatosDelDeudor"/> (y exportarla, el de datos personales).</summary>
    public static VistaDeInformeDeCartera ConDatosDelDeudor(string key, string name, string description, string fileName, IReadOnlyList<string> filters,
        IReadOnlyList<string>? ownFilters = null, EntregaDeCartera availableFrom = EntregaDeCartera.K1) =>
        new(key, name, description, fileName, filters, ownFilters, PersonalData: true, availableFrom, PermisoDeDatosDelDeudor);
}
