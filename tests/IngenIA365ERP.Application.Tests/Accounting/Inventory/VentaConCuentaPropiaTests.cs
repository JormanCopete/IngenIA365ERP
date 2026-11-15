using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 022, T048 y T049 (FR-047, Q16): la venta en v2 cuyo pago por transferencia entró a una cuenta bancaria propia se contabiliza contra
/// la cuenta contable del vínculo de esa cuenta (<c>ACC_OwnBankAccountLinks</c>, el mismo de Recaudo), no contra la regla del medio; sin cuenta
/// propia, por la regla del medio como antes; con una cuenta propia sin vínculo, la validación previa no deja confirmar
/// (<c>Accounting.OwnBankAccount.NotLinked</c>). Las ventas v1 se siguen contabilizando igual.
/// </summary>
public class VentaConCuentaPropiaTests
{
    private readonly EscenarioContable E = new();
    private static readonly Guid CuentaPropia = Guid.Parse("6d7e8f90-a1b2-4c3d-8e4f-5a6b7c8d9e01");

    public VentaConCuentaPropiaTests()
    {
        // La regla del medio (sin cuenta propia) y una cuenta bancaria habilitada para Inventario, vinculada a la cuenta propia.
        E.AgregarReglas(JsonNode.Parse("""[{ "op": "Venta", "rol": "MedioDePago", "cuenta": "11200501", "medio": "TRANSF" }]""")!.AsArray());
        var bancaria = E.D.Cuenta("11200599", AccountNature.Debit, AccountingModules.Accounting | AccountingModules.Inventory);
        bancaria.Name = "Banco de ensayo";
        E.D.Db.SaveChanges();
        E.D.Db.OwnBankAccountLinks.Add(new OwnBankAccountLink
        {
            OwnBankAccountPublicId = CuentaPropia, ChartOfAccountId = bancaria.Id, ValidFrom = new DateOnly(2026, 1, 1), CreatedBy = "test",
        });
        E.D.Db.SaveChanges();
    }

    private static PaymentLineV2 Transferencia(decimal valor, Guid? cuenta) => new()
    {
        PaymentPublicId = Guid.NewGuid(), LineNumber = 1, PaymentMeansCode = "TRANSF", PaymentMeansClass = PaymentMeansClass.Transfer,
        Direction = PaymentDirection.Received, Amount = valor, Reference = "TR-1", OwnBankAccountPublicId = cuenta,
    };

    private static IReadOnlyList<MensajeDeUnidad> VentaV2(string numero, params PaymentLineV2[] pagos)
    {
        var total = pagos.Sum(p => p.Amount);
        var venta = new VentaFacturadaV2
        {
            PointOfSaleCode = "PTO01",
            CashRegisterCode = "CAJA03",
            Lines = [new SalesAmountLineV1 { AccountingGroupCode = "ABARROTES", WarehouseCode = "B01", GrossAmount = total, NetAmount = total, DocumentLines = [1] }],
            Payments = pagos,
            Totals = new SalesTotalsV1 { Subtotal = total, Total = total, AmountDue = total },
        };
        var sobre = EscenarioContable.Sobre(VentaFacturadaV2.Type, clase: "SalesInvoice", tipoDoc: "FV", numero: numero) with { Version = 2 };
        return [new MensajeDeUnidad(sobre with { Payload = venta }, venta)];
    }

    [Fact]
    public async Task El_pago_con_cuenta_propia_va_a_la_cuenta_de_su_vinculo()
    {
        var unidad = VentaV2("FV-22", Transferencia(1000m, CuentaPropia));

        var c = ConstructorDeLineasDeInventario.Construir(unidad, await E.CatalogosAsync(unidad));

        c.Fallos.Select(f => f.Error.Code).Should().BeEmpty();
        c.Request!.Lines.Single(l => l.Debit == 1000m).AccountCode.Should().Be("11200599", "la cuenta del vínculo, no la regla del medio");
        (await E.D.Poster.ValidarAsync(c.Request, default)).Errores.Should().BeEmpty("el contrato de la 009 lo acepta");
    }

    [Fact]
    public async Task Sin_cuenta_propia_la_v2_contabiliza_por_la_regla_del_medio()
    {
        var unidad = VentaV2("FV-23", Transferencia(1000m, null));

        var c = ConstructorDeLineasDeInventario.Construir(unidad, await E.CatalogosAsync(unidad));

        c.Fallos.Should().BeEmpty();
        c.Request!.Lines.Single(l => l.Debit == 1000m).AccountCode.Should().Be("11200501");
    }

    [Fact]
    public async Task Una_cuenta_propia_sin_vinculo_no_es_contabilizable_y_la_validacion_previa_lo_dice()
    {
        var unidad = VentaV2("FV-24", Transferencia(1000m, Guid.NewGuid()));

        var c = ConstructorDeLineasDeInventario.Construir(unidad, await E.CatalogosAsync(unidad));
        c.EsValida.Should().BeFalse();
        c.Fallos.Single().Error.Code.Should().Be("Accounting.OwnBankAccount.NotLinked");

        var handler = new EvaluateInventoryPostingQueryHandler(E.D.Db, E.D.Poster, new ResolutorDeReglas(E.D.Db), new TiposDeComprobanteDeInventario(E.D.Db), E.D.Clock);
        var evaluado = await handler.Handle(new EvaluateInventoryPostingQuery(unidad.Select(m => new MensajeContableDto(m.Sobre, m.Contenido!)).ToList()), default);
        evaluado.Value.IsPostable.Should().BeFalse();
        evaluado.Value.Errors.Select(e => e.Rule).Should().Contain("Accounting.OwnBankAccount.NotLinked");
    }

    [Fact]
    public void El_contenido_anulado_de_una_venta_v2_se_lee_como_v2_con_su_cuenta_propia()
    {
        // La anulación copia el contenido del original con su tipo y su versión (§3.7): una venta v2 anulada se lee como v2.
        var original = VentaV2("FV-25", Transferencia(1000m, CuentaPropia));
        var json = System.Text.Json.JsonSerializer.SerializeToNode(original[0].Contenido, OpcionesDeMensajes.Opciones)!;

        var contenido = ConstructorDeLineasDeInventario.ContenidoTipado(VentaFacturadaV2.Type, 2, json);

        contenido.Should().BeOfType<VentaFacturadaV2>().Which.Payments.Single().OwnBankAccountPublicId.Should().Be(CuentaPropia);
        ConstructorDeLineasDeInventario.ContenidoTipado(VentaFacturadaV1.Type, 1, json).Should().BeOfType<VentaFacturadaV1>("la v1 ignora el campo nuevo");
    }
}
