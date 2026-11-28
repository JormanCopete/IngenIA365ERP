using FluentAssertions;
using IngenIA365ERP.Identity.Seed;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// 2026-11-23: <c>SEC_Permissions.Description</c> es <c>varchar(200)</c>. Una descripción más larga no falla al compilar ni en
/// las pruebas en memoria: tumba el arranque de la API en producción al sembrar (le pasó a <c>Core.Catalogs.Manage</c>).
/// </summary>
public class DescripcionesDePermisosCabenTests
{
    private const int Largo = 200;

    public static TheoryData<string, string, int> Descripciones()
    {
        var datos = new TheoryData<string, string, int>();
        foreach (var (catalogo, filas) in new (string, IEnumerable<(string Resource, string Action, string Description)>)[]
                 {
                     ("Domain", DomainPermissionCatalogSeeder.Catalog),
                     ("Core", CorePermissionCatalogSeeder.Catalog),
                     ("Payroll", PayrollPermissionCatalogSeeder.Catalog),
                     ("Accounting", AccountingPermissionCatalogSeeder.Catalog),
                     ("Lending", LendingPermissionCatalogSeeder.Catalog),
                     ("ReceiptsAndTreasury", ReceiptsAndTreasuryPermissionCatalogSeeder.Catalog),
                     ("Inventory", InventoryPermissionCatalogSeeder.Catalog),
                     ("Transport", TransportPermissionCatalogSeeder.Catalog),
                     ("Platform", PlatformPermissionCatalogSeeder.Catalog),
                     ("ElectronicInvoicing", ElectronicInvoicingPermissionCatalogSeeder.Catalog),
                     ("Channel", ChannelPermissionCatalogSeeder.Catalog),
                     ("Implantacion", ImplantacionPermissionCatalogSeeder.Catalog),
                 })
        {
            foreach (var f in filas) datos.Add(catalogo, $"{f.Resource}.{f.Action}", f.Description.Length);
        }
        return datos;
    }

    [Theory]
    [MemberData(nameof(Descripciones))]
    public void Cada_descripcion_cabe_en_la_columna(string catalogo, string codigo, int largo) =>
        largo.Should().BeLessThanOrEqualTo(Largo, $"la descripción de {codigo} ({catalogo}) no cabe en SEC_Permissions.Description");
}
