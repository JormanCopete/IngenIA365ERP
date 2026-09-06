using System.Net;
using FluentAssertions;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.API.IntegrationTests.Payroll;

namespace IngenIA365ERP.API.IntegrationTests.Accounting;

/// <summary>
/// El comando genérico de crear comprobantes contables contra la base real.
///
/// <para>
/// Existe porque la e2e de aprobación de nómina destapó una segunda relación entre
/// <c>ADM_Documents</c> y <c>ACC_VoucherTypes</c>: además del código, EF creó una FK sombra
/// <c>VoucherTypeId</c> (NOT NULL) para la navegación <c>AccountingDocument.VoucherType</c>,
/// que el mapeo del tipo de comprobante no enlaza. El contabilizador de nómina la esquiva
/// asignando la navegación; este comando sólo llena el código. Esta prueba dice si eso
/// falla de verdad. Reutiliza la cooperativa de las e2e de nómina, que ya tiene sucursal,
/// centro de costo, cuentas y períodos contables abiertos.
/// </para>
/// </summary>
[Collection(NominaCollection.Nombre)]
public class CreateDocumentEndpointTests(CentralIdentityApiFixture fx)
{
    [Fact]
    public async Task Un_comprobante_manual_cuadrado_se_crea_y_se_lee()
    {
        var ctx = await NominaE2E.PrepararAsync(fx);
        using var http = fx.CreateClient();
        var admin = ctx.TokenAdmin;

        var tipos = await NominaE2E.GetAsync(http, admin, "/api/accounting/voucher-types?SearchTerm=NM&PageSize=50");
        var nm = tipos.GetProperty("items").EnumerateArray().Single(v => v.GetProperty("code").GetString() == "NM");

        var cuentas = await NominaE2E.GetAsync(http, admin, "/api/accounting/chart-of-accounts?SearchTerm=50&PageSize=200");
        Guid Cuenta(string codigo) => cuentas.GetProperty("items").EnumerateArray()
            .Single(a => a.GetProperty("accountCode").GetString() == codigo).GetProperty("publicId").GetGuid();

        var creacion = await NominaE2E.EnviarAsync(http, admin, HttpMethod.Post, "/api/accounting/documents", new
        {
            voucherTypePublicId = nm.GetProperty("publicId").GetGuid(),
            documentDate = "2026-09-15",
            description = "Ajuste manual de prueba",
            lines = new object[]
            {
                new { accountPublicId = Cuenta(NominaE2E.CuentaDebito), debitAmount = 100_000, creditAmount = 0, description = "Débito" },
                new { accountPublicId = Cuenta(NominaE2E.CuentaCredito), debitAmount = 0, creditAmount = 100_000, description = "Crédito" },
            },
        });

        creacion.StatusCode.Should().Be(HttpStatusCode.Created,
            $"un comprobante cuadrado, en período abierto y con cuentas existentes tiene que crearse; respondió «{await creacion.Content.ReadAsStringAsync()}»");

        var publicId = (await NominaE2E.LeerAsync(creacion)).GetGuid();
        var leido = await NominaE2E.GetAsync(http, admin, $"/api/accounting/documents/{publicId}");
        leido.GetProperty("totalDebit").GetDecimal().Should().Be(100_000m);
        leido.GetProperty("totalCredit").GetDecimal().Should().Be(100_000m);
    }
}
