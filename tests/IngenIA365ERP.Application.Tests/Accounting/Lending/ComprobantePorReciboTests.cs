using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// 2026-11-17 (pedido del dueño): un recibo de caja que abona a varios créditos, aportes y ahorro se contabiliza en un solo comprobante, al
/// instante, si la empresa enciende <c>Accounting.LendingVoucherPerReceipt</c>. Apagado (de fábrica), una unidad por comprobante como siempre.
/// </summary>
public class ComprobantePorReciboTests
{
    private readonly EscenarioContableDeCartera E = new();

    private static IReadOnlyList<MensajeEntrante> Pago(string recibo = "R-0001", DateOnly? fecha = null) =>
        ConRecibo(EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("04-pago-por-caja-con-anticipo")), recibo, fecha);

    private static IReadOnlyList<MensajeEntrante> ConRecibo(IReadOnlyList<MensajeEntrante> unidad, string recibo, DateOnly? fecha = null) =>
        unidad.Select(m => m.Payload is PagoAplicadoV1 p
            ? m with { Payload = p with { Collector = p.Collector with { Receipt = p.Collector.Receipt with { Number = recibo, Date = fecha ?? p.Collector.Receipt.Date } } } }
            : m).ToList();

    private static UnidadDeConsumo Unidad(IReadOnlyList<MensajeEntrante> mensajes) =>
        new(IntegrationDestinations.Accounting, mensajes[0].Envelope.Origin.PublicId, mensajes[0].Envelope.OriginEventKey,
            mensajes.Select(m => m.MessageId).ToList(), null, 0) { OriginModule = ModulosDeOrigen.Cartera };

    /// <summary>Un aporte de la misma caja: el sobre de un pago con el tipo y el contenido de un <c>AporteRecibido</c>.</summary>
    private static IReadOnlyList<MensajeEntrante> Aporte(string recibo)
    {
        var pago = Pago(recibo)[0];
        var recaudo = ((PagoAplicadoV1)pago.Payload!).Collector;
        return [pago with { Envelope = pago.Envelope with { Type = AporteRecibidoV1.Type }, Payload = new AporteRecibidoV1 { Collector = recaudo } }];
    }

    [Fact]
    public void Las_unidades_del_mismo_recibo_van_juntas_y_las_demas_solas()
    {
        var a = Pago();
        var b = Pago();
        var otro = Pago("R-0002");
        var aporte = Aporte("R-0001");
        var unidades = new[] { a, otro, b, aporte }.Select(u => (Unidad(u), u)).ToList();

        var trabajos = ComprobantePorRecibo.Planear(unidades);

        trabajos.Should().HaveCount(2);
        trabajos[0].ClaveDeGrupo.Should().StartWith(ComprobantePorRecibo.PrefijoDeGrupo).And.Contain("|REC|R-0001|");
        trabajos[0].Unidades.Should().Equal(unidades[0].Item1, unidades[2].Item1, unidades[3].Item1);
        trabajos[1].ClaveDeGrupo.Should().BeNull("un recibo con una sola unidad va como siempre");
        trabajos[1].Unidades.Should().Equal(unidades[1].Item1);
    }

    [Fact]
    public void Sin_numero_de_recibo_o_en_otra_fecha_contable_no_se_juntan()
    {
        ComprobantePorRecibo.ClaveDe(Pago("")).Should().BeNull();
        var hoy = Pago();
        var otraFecha = EscenarioContableDeCartera.Copia(Pago(), fecha: new DateOnly(2026, 11, 21));
        ComprobantePorRecibo.ClaveDe(hoy).Should().NotBe(ComprobantePorRecibo.ClaveDe(otraFecha));
        ComprobantePorRecibo.ClaveDe(EscenarioContableDeCartera.UnidadDe("03-causacion-del-dia")).Should().BeNull("la causación no trae recibo");
    }

    [Fact]
    public async Task Apagado_el_planeador_deja_una_unidad_por_trabajo_y_encendido_junta_el_recibo()
    {
        var a = Pago();
        var b = Pago();
        E.Emitir(a);
        E.Emitir(b);
        var unidades = new[] { Unidad(a), Unidad(b) };
        var planeador = new PlaneadorEnLineaDeCartera(E.D.Db, new MensajesEntrantes(E.D.Db));

        (await planeador.PlanearAsync(unidades, IntegrationDestinations.Accounting, default)).Should().HaveCount(2).And.OnlyContain(t => t.ClaveDeGrupo == null);

        E.D.Db.SystemSettings.Add(new SystemSetting { SettingKey = ComprobantePorRecibo.Clave, SettingValue = "true", ValueType = "Bool", ModulePrefix = "ACC", CreatedBy = "test" });
        E.D.Db.SaveChanges();
        var juntos = await planeador.PlanearAsync(unidades, IntegrationDestinations.Accounting, default);
        juntos.Should().ContainSingle().Which.Unidades.Should().HaveCount(2);
    }

    [Fact]
    public async Task El_recibo_sale_en_un_solo_RC_con_las_lineas_de_cada_credito_y_sin_lote()
    {
        var a = Pago();
        var b = Pago();
        E.Emitir(a);
        E.Emitir(b);
        var clave = ComprobantePorRecibo.ClaveDe(a)!;

        var r = await E.Resumido().Handle(new PostLendingSummaryGroupCommand(Guid.Empty, clave, a.Concat(b).Select(m => m.MessageId).ToList()), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Should().HaveCount(2).And.OnlyContain(x => x.Resultado is ResultadoDeConsumo.Processed);
        E.D.Db.ChangeTracker.Clear();
        var documento = await E.D.Db.AccountingDocuments.Include(d => d.Lines).Include(d => d.VoucherType).SingleAsync();
        documento.VoucherType!.Code.Should().Be("RC");
        documento.SourceType.Should().Be(OrigenesDeCartera.Documento);
        documento.SourcePublicId.Should().Be(a[0].Envelope.Origin.PublicId);
        documento.Description.Should().StartWith("Recibo R-0001 · 2026-11-20");

        documento.Lines.Should().HaveCount(2 * ConstructorDeLineasDeCartera.Construir(a.Select(HechoDeCartera.De).ToList(), await E.CatalogosAsync(a)).Request!.Lines.Count,
            "cada crédito conserva sus líneas: no se suman");
        documento.TotalDebit.Should().Be(documento.TotalCredit);
        (await E.D.Db.LendingPostings.ToListAsync()).Should().HaveCount(a.Count + b.Count)
            .And.OnlyContain(p => p.AccountingDocumentId == documento.Id && p.BatchPublicId == null);
    }
}
