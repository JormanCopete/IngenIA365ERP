using FluentAssertions;
using IngenIA365ERP.Application.Security.Roles.Plantillas;
using IngenIA365ERP.Identity.Seed;

namespace IngenIA365ERP.Application.Tests.Security;

/// <summary>
/// Feature 013, T0138 (T61; DT §2.14; contracts/api.md §2.5; C85, C86; quickstart §1.5): las trece plantillas <c>cartera.*</c> de
/// <see cref="PerfilesSugeridos"/>, servidas por <c>GET /api/admin/roles/templates?module=Lending</c>. Ninguna nombra un código que el
/// catálogo no tenga; <c>cartera.administrador</c> es todo <c>Lending.*</c> salvo decidir y votar (C86); asesor y jefe cambian la
/// preferencia del deudor (C85); los de sólo lectura no escriben.
/// </summary>
public class PerfilesSugeridosDeCarteraTests
{
    private static readonly string[] Catalogo =
        [.. DomainPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. CorePermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. PayrollPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. AccountingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. InventoryPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. ElectronicInvoicingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         .. LendingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}")];

    private static IReadOnlyList<string> Codigos(string clave) => PerfilesSugeridos.Expandir(PerfilesSugeridos.Buscar(clave)!, Catalogo).Codigos;

    [Fact]
    public void Hay_trece_plantillas_de_Cartera()
    {
        PerfilesSugeridos.DelModulo("Lending").Select(p => p.Key).Should().BeEquivalentTo(
        [
            "cartera.administrador", "cartera.asesor", "cartera.analista", "cartera.jefe", "cartera.comite-credito", "cartera.comite-riesgos",
            "cartera.gerente", "cartera.consejo", "cartera.gestor-cobranza", "cartera.oficial-cumplimiento", "cartera.contador",
            "cartera.auditor", "cartera.revisor-fiscal",
        ]);
        PerfilesSugeridos.DelModulo("Lending").Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Actor) && !string.IsNullOrWhiteSpace(p.Notes));
    }

    [Fact]
    public void Ninguna_plantilla_de_Cartera_nombra_codigos_inexistentes()
    {
        foreach (var plantilla in PerfilesSugeridos.DelModulo("Lending"))
        {
            var (codigos, omitidos) = PerfilesSugeridos.Expandir(plantilla, Catalogo);
            omitidos.Should().BeEmpty($"«{plantilla.Key}» sólo nombra códigos del catálogo sembrado");
            codigos.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void El_administrador_de_Cartera_no_decide_ni_vota()
    {
        var codigos = Codigos("cartera.administrador");

        codigos.Should().NotContain(c => c.StartsWith("Lending.Decisions.", StringComparison.Ordinal));
        codigos.Should().NotContain("Lending.Sessions.Vote");
        codigos.Should().HaveCount(LendingPermissionCatalogSeeder.Catalog.Length - 5, "todo Lending.* menos los cuatro Decisions y el voto");
    }

    [Fact]
    public void Asesor_y_jefe_cambian_la_preferencia_del_deudor()
    {
        Codigos("cartera.asesor").Should().Contain("Lending.PrepaymentPreferences.Manage");
        Codigos("cartera.jefe").Should().Contain("Lending.PrepaymentPreferences.Manage");
        Codigos("cartera.analista").Should().NotContain("Lending.PrepaymentPreferences.Manage");
    }

    [Theory]
    [InlineData("cartera.auditor")]
    [InlineData("cartera.revisor-fiscal")]
    public void Auditor_y_revisor_fiscal_no_escriben(string clave)
    {
        string[] lectura = [".Read", ".View", ".Export", ".Verify", ".VerifyIntegrity", ".AllOffices", ".AllLines"];
        Codigos(clave).Should().OnlyContain(c => lectura.Any(s => c.EndsWith(s, StringComparison.Ordinal)), "sin escritura");
        Codigos(clave).Should().Contain(["Lending.Sessions.Read", "Lending.Integrity.Verify", "AuditLog.VerifyIntegrity"]);
    }

    [Fact]
    public void Las_decisiones_estan_repartidas_entre_analista_jefe_y_gerente()
    {
        Codigos("cartera.analista").Should().Contain("Lending.Decisions.Analyst");
        Codigos("cartera.jefe").Should().Contain("Lending.Decisions.Coordinator");
        Codigos("cartera.gerente").Should().Contain(["Lending.Decisions.Management", "Lending.Decisions.Exceptions"]);
    }
}
