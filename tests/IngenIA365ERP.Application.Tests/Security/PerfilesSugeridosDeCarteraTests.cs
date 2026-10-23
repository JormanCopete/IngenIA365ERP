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
         .. LendingPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}"),
         // Feature 015 (T0044): Recaudo, Tesorería y sus piezas de plataforma.
         .. ReceiptsAndTreasuryPermissionCatalogSeeder.Catalog.Select(p => $"{p.Resource}.{p.Action}")];

    private static IReadOnlyList<string> Codigos(string clave) => PerfilesSugeridos.Expandir(PerfilesSugeridos.Buscar(clave)!, Catalogo).Codigos;

    [Fact]
    public void Hay_diecisiete_plantillas_de_Cartera()
    {
        // Feature 014 (T018; api.md §2; research E07): las trece del crédito y las cuatro de captaciones y aportes.
        PerfilesSugeridos.DelModulo("Lending").Select(p => p.Key).Should().BeEquivalentTo(
        [
            "cartera.administrador", "cartera.asesor", "cartera.analista", "cartera.jefe", "cartera.comite-credito", "cartera.comite-riesgos",
            "cartera.gerente", "cartera.consejo", "cartera.gestor-cobranza", "cartera.oficial-cumplimiento", "cartera.contador",
            "cartera.auditor", "cartera.revisor-fiscal",
            "cartera.jefe-captaciones", "cartera.asesor-captaciones", "cartera.analista-aportes", "cartera.tesorero-liquidez",
        ]);
        PerfilesSugeridos.DelModulo("Lending").Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.Actor) && !string.IsNullOrWhiteSpace(p.Notes));
    }

    [Fact]
    public void El_jefe_de_captaciones_no_activa_ni_acepta_diferencias_de_la_migracion()
    {
        // Respuesta 105 A del dueño (E250): esos dos permisos se asignan a quien dirige la puesta en marcha, no a la plantilla.
        var codigos = Codigos("cartera.jefe-captaciones");
        codigos.Should().NotContain("Lending.Deposits.Activate");
        codigos.Should().NotContain("Lending.Migration.AcceptDifferences");
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
            .HaveCount(LendingPermissionCatalogSeeder.Catalog.Length - 7,
                "todo Lending.* menos los cuatro Decisions, el voto, los dobles de prueba y aprobar una tasa negociada (feature 014)");
        codigos.Should().NotContain("Lending.NegotiatedRates.Approve", "administrar no es aprobar una tasa negociada (api.md §2)");
        codigos.Where(c => !c.StartsWith("Lending.", StringComparison.Ordinal)).Should().BeEquivalentTo(["Attachments.Upload", "Attachments.Download"], "sube y elige soportes (bloque D)");
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
    /// consejo lee los créditos y las calificaciones de lo que aplica—; quien sube también lista y descarga para elegir el soporte, y nadie borra adjuntos.
    /// </summary>
    [Fact]
    public void Los_roles_de_Cartera_pueden_hacer_su_propio_trabajo_de_K3()
    {
        Codigos("cartera.analista").Should().Contain("Lending.Novelties.Read");
        Codigos("cartera.gerente").Should().Contain("Lending.Novelties.Read");
        Codigos("cartera.consejo").Should().Contain(["Lending.Loans.Read", "Lending.Ratings.Read", "Attachments.Upload"]);
        foreach (var clave in new[] { "cartera.administrador", "cartera.jefe", "cartera.consejo" })
            Codigos(clave).Should().Contain(["Attachments.Upload", "Attachments.Download"], $"«{clave}» sube y elige soportes");
        foreach (var plantilla in PerfilesSugeridos.DelModulo("Lending"))
            Codigos(plantilla.Key).Should().NotContain(["Attachments.Delete"], $"«{plantilla.Key}» no borra adjuntos");
        // El gestor de cobranza sube desde K4, bloque D, la guía de la carta del aviso previo (abajo); los soportes de K3 le siguen vedados por permiso del dueño.
        foreach (var clave in new[] { "cartera.asesor", "cartera.analista", "cartera.gerente", "cartera.auditor" })
            Codigos(clave).Should().NotContain("Attachments.Upload", $"«{clave}» no sube soportes de K3");
        Codigos("cartera.gestor-cobranza").Should().NotContain(["Lending.Novelties.Request", "Lending.WriteOffs.Prepare", "Lending.DebtorEvents.Manage"],
            "el gestor no sube soportes de novedades, castigo ni eventos del deudor");
    }

    /// <summary>
    /// K4, bloque D (decisión del dueño del 2026-10-16): el jefe aprueba acuerdos, lleva el cobro jurídico y las campañas y envía y registra avisos
    /// previos; el gestor envía avisos previos y registra la carta (sube la guía). La segregación de los acuerdos sigue en pie: quien propone (el gestor)
    /// no aprueba, y el jefe, que aprueba, no propone; aunque una cooperativa le dé los dos a la misma persona, el comando rechaza aprobar lo propio.
    /// </summary>
    [Fact]
    public void Los_roles_de_Cartera_pueden_hacer_su_propio_trabajo_de_K4()
    {
        Codigos("cartera.jefe").Should().Contain(["Lending.PaymentAgreements.Approve", "Lending.LegalProcesses.Manage", "Lending.Campaigns.Manage",
            "Lending.PriorNotices.Send", "Lending.PriorNotices.RegisterEvidence"]);
        Codigos("cartera.jefe").Should().NotContain("Lending.PaymentAgreements.Create", "quien aprueba no propone");
        Codigos("cartera.gestor-cobranza").Should().Contain(["Lending.PriorNotices.Send", "Lending.PriorNotices.RegisterEvidence", "Attachments.Upload",
            "Attachments.Download"]);
        Codigos("cartera.gestor-cobranza").Should().NotContain(["Lending.PaymentAgreements.Approve", "Lending.LegalProcesses.Manage", "Lending.Campaigns.Manage"],
            "el gestor propone y gestiona; aprobar, el cobro jurídico y las campañas son del jefe");
    }
}
