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
        codigos.Where(c => c.StartsWith("Lending.", StringComparison.Ordinal)).Should()
            .HaveCount(LendingPermissionCatalogSeeder.Catalog.Length - 6, "todo Lending.* menos los cuatro Decisions, el voto y los dobles de prueba");
        codigos.Where(c => !c.StartsWith("Lending.", StringComparison.Ordinal)).Should().Equal(["Attachments.Upload"], "sube soportes (bloque D)");
    }

    /// <summary>
    /// Feature 013, US5, T0555 (api.md §2.5; C85): asesor y jefe cambian la preferencia del deudor, el gerente autoriza la fecha valor antes de un cierre
    /// y ninguna plantilla trae <c>Lending.TestDoubles.Operate</c> (se concede a mano, fuera de producción).
    /// </summary>
    [Fact]
    public void La_fecha_valor_antes_de_un_cierre_es_del_gerente_y_los_dobles_de_prueba_de_ninguna_plantilla()
    {
        Catalogo.Should().Contain(["Lending.PrepaymentPreferences.Manage", "Lending.Payments.BackdateBeforeClose", "Lending.TestDoubles.Operate"]);
        Codigos("cartera.gerente").Should().Contain("Lending.Payments.BackdateBeforeClose");
        foreach (var plantilla in PerfilesSugeridos.DelModulo("Lending"))
            Codigos(plantilla.Key).Should().NotContain("Lending.TestDoubles.Operate", $"«{plantilla.Key}» no trae los dobles de prueba");
    }

    /// <summary>
    /// Respuesta del dueño (2026-10-14): el asesor consulta las líneas de crédito (sólo consultar); cada cooperativa lo ajusta en los permisos del rol
    /// (Seguridad › Roles), porque el rol creado desde la plantilla es suyo y se edita.
    /// </summary>
    [Fact]
    public void El_asesor_consulta_las_lineas_y_no_las_administra()
    {
        Codigos("cartera.asesor").Should().Contain("Lending.CreditLines.View");
        Codigos("cartera.asesor").Should().NotContain("Lending.CreditLines.Manage");
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

    /// <summary>
    /// K3, bloque D (decisión del dueño del 2026-10-15): los roles sugeridos hacen su propio trabajo con mínimo privilegio —analista y gerente deciden
    /// novedades (la ruta de decisiones exige verlas), quien sube soportes de novedades, eventos del deudor y castigo trae <c>Attachments.Upload</c> y el
    /// consejo lee los créditos y las calificaciones de lo que aplica—, sin descargar ni borrar adjuntos.
    /// </summary>
    [Fact]
    public void Los_roles_de_Cartera_pueden_hacer_su_propio_trabajo_de_K3()
    {
        Codigos("cartera.analista").Should().Contain("Lending.Novelties.Read");
        Codigos("cartera.gerente").Should().Contain("Lending.Novelties.Read");
        Codigos("cartera.consejo").Should().Contain(["Lending.Loans.Read", "Lending.Ratings.Read", "Attachments.Upload"]);
        foreach (var clave in new[] { "cartera.administrador", "cartera.jefe", "cartera.consejo" })
            Codigos(clave).Should().Contain("Attachments.Upload", $"«{clave}» sube soportes");
        foreach (var plantilla in PerfilesSugeridos.DelModulo("Lending"))
            Codigos(plantilla.Key).Should().NotContain(["Attachments.Delete"], $"«{plantilla.Key}» no borra adjuntos");
        foreach (var clave in new[] { "cartera.asesor", "cartera.analista", "cartera.gerente", "cartera.gestor-cobranza", "cartera.auditor" })
            Codigos(clave).Should().NotContain("Attachments.Upload", $"«{clave}» no sube soportes de K3");
    }
}
