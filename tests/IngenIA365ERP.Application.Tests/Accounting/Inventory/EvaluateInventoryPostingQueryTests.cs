using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T455 (T515; contracts/contabilidad.md §4.2 y §4.3; FR-074; SC-021): «¿es contabilizable?». Usa el mismo
/// constructor del consumidor, no numera ni deja nada rastreado; cada hallazgo dice quién lo corrige (una fila por código de la
/// tabla de §4.3); los avisos no impiden; no evalúa informativos ni mensajes a Cartera; más de dos decimales es defecto del
/// emisor.
/// </summary>
public class EvaluateInventoryPostingQueryTests
{
    private readonly EscenarioContable E = new();

    private async Task<ResultadoDeContabilizacionDto> EvaluarAsync(IEnumerable<MensajeDeUnidad> mensajes)
    {
        var handler = new EvaluateInventoryPostingQueryHandler(E.D.Db, E.D.Poster, new ResolutorDeReglas(E.D.Db), new TiposDeComprobanteDeInventario(E.D.Db), E.D.Clock);
        var r = await handler.Handle(new EvaluateInventoryPostingQuery(mensajes.Select(m => new MensajeContableDto(m.Sobre, m.Contenido!)).ToList()), default);
        r.IsSuccess.Should().BeTrue();
        return r.Value;
    }

    [Fact]
    public async Task Lo_contabilizable_no_numera_ni_deja_nada_en_el_contexto()
    {
        var unidad = EscenarioContable.Unidad(EscenarioContable.Caso("01-venta-pos-de-mensajes-14"));
        var siguiente = (await E.D.Db.VoucherTypes.AsNoTracking().SingleAsync(v => v.Code == "FV")).NextNumber;
        E.D.Db.ChangeTracker.Clear();

        var r = await EvaluarAsync(unidad);

        r.IsPostable.Should().BeTrue();
        r.Errors.Should().BeEmpty();
        E.D.Db.ChangeTracker.Entries().Should().BeEmpty("la evaluación corre sin seguimiento dentro de la confirmación de Inventario");
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(0);
        (await E.D.Db.VoucherTypes.AsNoTracking().SingleAsync(v => v.Code == "FV")).NextNumber.Should().Be(siguiente);
    }

    [Fact]
    public async Task Sin_regla_dice_la_linea_la_regla_y_que_la_corrige_contabilidad_en_la_matriz()
    {
        var r = await EvaluarAsync(EscenarioContable.Compra(1000m, "CARNES"));

        r.IsPostable.Should().BeFalse();
        var h = r.Errors.Single();
        h.Rule.Should().Be("Accounting.InventoryRule.Missing");
        h.DocumentLines.Should().Equal(1);
        h.MessageType.Should().Be(CompraRecibidaV1.Type);
        h.WhoFixes.Should().Be(new QuienCorrigeDto("Contabilidad", "/contabilidad/inventario/matriz", "Accounting.InventoryRules.Manage"));
    }

    [Fact]
    public async Task Un_periodo_cerrado_lo_corrige_quien_reabre_periodos()
    {
        var r = await EvaluarAsync(EscenarioContable.Compra(1000m, fecha: new DateOnly(2026, 2, 10)));

        r.Errors.Single().Rule.Should().Be("Accounting.Period.Closed");
        r.Errors.Single().WhoFixes.Should().Be(new QuienCorrigeDto("Contabilidad", "/contabilidad/periodos", "Accounting.Periods.Reopen"));
    }

    [Fact]
    public async Task El_tercero_que_falta_en_una_compra_lo_pone_el_documento()
    {
        var unidad = EscenarioContable.Compra(1000m);
        unidad = [unidad[0] with { Sobre = unidad[0].Sobre with { PersonPublicId = null } }];

        var r = await EvaluarAsync(unidad);

        var h = r.Errors.Single();
        h.Rule.Should().Be("Accounting.Line.ThirdPartyRequired");
        h.AccountCode.Should().Be("22050501");
        h.DocumentLines.Should().Equal(1);
        h.WhoFixes.Should().Be(new QuienCorrigeDto("Inventario", null, "Inventory.Purchases.Create"));
    }

    [Fact]
    public async Task Un_aviso_de_impuesto_no_impide()
    {
        var caso = EscenarioContable.Caso("06-nota-debito-un-impuesto-por-renglon");
        var pago = caso["mensajes"]![0]!["payload"]!;
        pago["taxes"]![0]!["amount"] = 1902.00m;
        pago["totals"]!["taxTotal"] = 3802.00m;
        pago["totals"]!["total"] = 23802.00m;
        pago["totals"]!["amountDue"] = 23802.00m;
        pago["payments"]![0]!["amount"] = 23802.00m;

        var r = await EvaluarAsync(EscenarioContable.Unidad(caso));

        r.IsPostable.Should().BeTrue();
        r.Warnings.Should().ContainSingle().Which.Rule.Should().Be("Accounting.Line.TaxAmountDiffers");
        r.Warnings[0].DocumentLines.Should().Equal(1);
    }

    [Fact]
    public async Task No_evalua_informativos_ni_mensajes_a_cartera()
    {
        var saldo = new SaldoInicialCargadoV1();
        var informativo = new MensajeDeUnidad(EscenarioContable.Sobre(SaldoInicialCargadoV1.Type, "OpeningBalance", "SIN", "SI-1", kind: IntegrationMessageKind.Informational) with { Payload = saldo }, saldo);
        var credito = new VentaACreditoRegistradaV1();
        var cartera = new MensajeDeUnidad(EscenarioContable.Sobre(VentaACreditoRegistradaV1.Type, "PosEquivalentDocument", "DEPOS", "PV-1") with { Payload = credito }, credito);

        var r = await EvaluarAsync([informativo, cartera]);

        r.IsPostable.Should().BeTrue();
        r.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Mas_de_dos_decimales_es_defecto_del_emisor()
    {
        var r = await EvaluarAsync(EscenarioContable.Compra(100.005m));

        r.IsPostable.Should().BeFalse();
        r.Errors.Should().OnlyContain(h => h.Rule == "Accounting.Line.AmountInvalid" && h.WhoFixes.Module == "Inventario" && h.WhoFixes.Page == null);
    }

    [Theory]
    [InlineData("Accounting.InventoryRule.Missing", false, "Contabilidad", "/contabilidad/inventario/matriz", "Accounting.InventoryRules.Manage")]
    [InlineData("Accounting.InventoryRule.TaxRateMismatch", false, "Contabilidad", "/contabilidad/inventario/matriz", "Accounting.InventoryRules.Manage")]
    [InlineData("Accounting.InventoryRule.TaxRateMismatch", true, "Contabilidad", "/contabilidad/plan-de-cuentas", "Accounting.Taxes.Manage")]
    [InlineData("Accounting.VoucherType.NotFound", false, "Contabilidad", "/contabilidad/inventario/tipos-de-comprobante", "Accounting.InventoryRules.Manage")]
    [InlineData("Accounting.VoucherType.NotAllowedForModule", false, "Contabilidad", "/contabilidad/inventario/tipos-de-comprobante", "Accounting.InventoryRules.Manage")]
    [InlineData("Accounting.Line.AccountNotMovement", false, "Contabilidad", "/contabilidad/plan-de-cuentas", "Accounting.Accounts.Manage")]
    [InlineData("Accounting.Line.AccountInactive", false, "Contabilidad", "/contabilidad/plan-de-cuentas", "Accounting.Accounts.Manage")]
    [InlineData("Accounting.Line.AccountNotEnabledForModule", false, "Contabilidad", "/contabilidad/plan-de-cuentas", "Accounting.Accounts.Manage")]
    [InlineData("Accounting.Line.CostCenterNotAllowed", false, "Contabilidad", "/contabilidad/plan-de-cuentas", "Accounting.Accounts.Manage")]
    [InlineData("Accounting.Line.TaxBaseRequired", false, "Contabilidad", "/contabilidad/plan-de-cuentas", "Accounting.Accounts.Manage")]
    [InlineData("Accounting.Line.TaxAmountMismatch", false, "Contabilidad", "/contabilidad/plan-de-cuentas", "Accounting.Accounts.Manage")]
    [InlineData("Accounting.Line.CrossDocumentRequired", false, "Contabilidad", "/contabilidad/inventario/matriz", "Accounting.InventoryRules.Manage")]
    [InlineData("Accounting.Line.ThirdPartyInvalid", false, "Core", "/maestros/personas", "Core.People.Update")]
    [InlineData("Accounting.Line.CrossDocumentTypeInvalid", false, "Contabilidad", "/contabilidad/inventario/tipos-de-comprobante", "Accounting.InventoryRules.Manage")]
    [InlineData("Accounting.Period.Closed", false, "Contabilidad", "/contabilidad/periodos", "Accounting.Periods.Reopen")]
    [InlineData("Accounting.Period.NotFound", false, "Contabilidad", "/contabilidad/periodos", "Accounting.Periods.CloseYear")]
    [InlineData("Accounting.NotInitialized", false, "Contabilidad", "/contabilidad/configuracion", "Accounting.Setup.Manage")]
    [InlineData("Accounting.InventoryMessage.Unbalanced", false, "Inventario", null, null)]
    public void Quien_corrige_cada_codigo_es_el_de_la_tabla_4_3(string codigo, bool deLaCuenta, string modulo, string? pagina, string? permiso) =>
        QuienCorrige.De(codigo, DocumentClass.PurchaseReceipt, deLaCuenta).Should().Be(new QuienCorrigeDto(modulo, pagina, permiso));

    [Fact]
    public void El_tercero_y_el_centro_dependen_de_la_clase()
    {
        QuienCorrige.De("Accounting.Line.ThirdPartyRequired", DocumentClass.SalesInvoice).Should().Be(new QuienCorrigeDto("Inventario", null, "Inventory.Sales.Create"));
        QuienCorrige.De("Accounting.Line.ThirdPartyRequired", DocumentClass.PositiveAdjustment).Should().Be(new QuienCorrigeDto("Contabilidad", "/contabilidad/inventario/matriz", "Accounting.InventoryRules.Manage"));
        QuienCorrige.De("Accounting.Line.CostCenterRequired", DocumentClass.InternalConsumption).Should().Be(new QuienCorrigeDto("Inventario", null, "Inventory.Adjustments.Create"));
    }
}
