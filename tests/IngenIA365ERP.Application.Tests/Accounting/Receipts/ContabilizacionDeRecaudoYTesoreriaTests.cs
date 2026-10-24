using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Application.Accounting.Integration;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Accounting.Receipts.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Treasury.Contabilizacion;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using static IngenIA365ERP.Application.Tests.Accounting.Receipts.MatricesDePrueba;

namespace IngenIA365ERP.Application.Tests.Accounting.Receipts;

/// <summary>
/// Feature 015, US8, T0251 (FR-140 a FR-145; contabilidad.md §3, §4, §7): las ramas <c>REC</c> y <c>TES</c> del enrutador; el comprobante de una unidad con la
/// cuenta de cada rol, el tercero y el cruce sólo donde la cuenta los exige y el tipo del hecho principal; las dos puntas en la misma cuenta no dejan comprobante; los
/// recibos de caja por lote se juntan por día y punto y su resumen suma por cuenta y lado; lo de Recaudo y Tesorería no se reversa desde Comprobantes. Prueba
/// escrita, sin correr.
/// </summary>
public class ContabilizacionDeRecaudoYTesoreriaTests
{
    private static readonly Guid Sucursal = Guid.NewGuid();
    private static readonly Guid Pagador = Guid.NewGuid();

    private static ReciboEmitidoV1 Recibo(decimal valor, string caja = "C1", string punto = "P1") => new()
    {
        ReceiptPublicId = Guid.NewGuid(), Number = "RC-1", Channel = "CashDesk", ValueDate = Hoy, PayerPersonPublicId = Pagador, TotalApplied = valor,
        Lines =
        [
            new FactLineV1 { Role = RolesDeRecaudo.Caja, Side = RolesDeRecaudo.Debito, Amount = valor, CrossDocument = "RC-1",
                Dimensions = new ReceiptsFactDimensionsV1 { BranchCode = "01", CollectionPointCode = punto, CashBoxCode = caja, MeansCode = "EF", CollectorClass = "Receipts" } },
            new FactLineV1 { Role = RolesDeRecaudo.PuenteDelModulo, Side = RolesDeRecaudo.Credito, Amount = valor, ThirdPartyPersonPublicId = Pagador,
                Dimensions = new ReceiptsFactDimensionsV1 { BranchCode = "01", CollectionPointCode = punto, TargetModule = "CAR", TargetNature = "Loan", CollectorClass = "Receipts" } },
        ],
    };

    private static (CatalogosDeModulo Catalogos, ChartOfAccount Caja, ChartOfAccount Puente) Catalogos(bool puenteConTercero = false, bool conMapeo = true)
    {
        var caja = Cuenta("110505");
        var puente = Cuenta("249595", tercero: puenteConTercero, cruce: puenteConTercero);
        var matriz = Matriz(MatricesDeRecaudoYTesoreria.Recaudo,
            [Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, caja), Regla<ReceiptsPostingRule>(RolesDeRecaudo.PuenteDelModulo, puente, dimensiones: r => r.TargetModule = "CAR")],
            [caja, puente]);
        var mapeos = new Dictionary<string, (string, string?)>(StringComparer.Ordinal);
        if (conMapeo) mapeos[ReciboEmitidoV1.Type] = ("RR", null);
        return (new CatalogosDeModulo(matriz, mapeos, new Dictionary<Guid, int> { [Pagador] = 9 }, new Dictionary<Guid, int> { [Sucursal] = 1 }), caja, puente);
    }

    private static HechoContableDeModulo Hecho(object contenido, string tipo, string? scheduleKey = null, string modulo = "REC") =>
        HechoContableDeModulo.De(Mensaje(modulo, contenido, tipo, sucursal: Sucursal, persona: Pagador, scheduleKey: scheduleKey));

    [Fact]
    public void El_recibo_arma_su_comprobante_con_la_cuenta_de_cada_rol_y_el_tipo_del_hecho()
    {
        var (c, _, _) = Catalogos();
        var r = ConstructorDeComprobanteDeModulo.Construir(MatricesDeRecaudoYTesoreria.Recaudo, [Hecho(Recibo(80_000m), ReciboEmitidoV1.Type)], c);

        r.EsValida.Should().BeTrue();
        r.Request!.VoucherTypeCode.Should().Be("RR");
        r.Request.Origin.Module.Should().Be(ModuloContable.Recaudo);
        r.Request.Lines.Should().HaveCount(2);
        r.Request.Lines.Single(l => l.AccountCode == "110505").Debit.Should().Be(80_000m);
        r.Request.Lines.Single(l => l.AccountCode == "249595").Credit.Should().Be(80_000m);
        r.Request.Lines.Should().OnlyContain(l => l.PersonId == null && l.CrossDocumentType == null, "ninguna de las dos cuentas exige tercero ni cruce");
        r.Request.Lines.Should().OnlyContain(l => l.BranchId == 1);
    }

    [Fact]
    public void El_tercero_y_el_cruce_van_solo_donde_la_cuenta_los_exige()
    {
        var (c, _, _) = Catalogos(puenteConTercero: true);
        var r = ConstructorDeComprobanteDeModulo.Construir(MatricesDeRecaudoYTesoreria.Recaudo, [Hecho(Recibo(80_000m), ReciboEmitidoV1.Type)], c);

        var puente = r.Request!.Lines.Single(l => l.AccountCode == "249595");
        puente.PersonId.Should().Be(9);
        puente.CrossDocumentType.Should().Be(ConstructorDeComprobanteDeModulo.CruceOtro);
        r.Request.Lines.Single(l => l.AccountCode == "110505").PersonId.Should().BeNull();
    }

    [Fact]
    public void Sin_regla_o_sin_tipo_de_comprobante_la_unidad_se_rechaza_con_su_hecho_y_su_rol()
    {
        var (sinMapeo, _, _) = Catalogos(conMapeo: false);
        ConstructorDeComprobanteDeModulo.Construir(MatricesDeRecaudoYTesoreria.Recaudo, [Hecho(Recibo(1m), ReciboEmitidoV1.Type)], sinMapeo)
            .Fallos.Should().Contain(f => f.Error.Code == "Accounting.ReceiptsRule.VoucherTypeMissing");

        var vacia = new CatalogosDeModulo(Matriz(MatricesDeRecaudoYTesoreria.Recaudo, [], []),
            new Dictionary<string, (string, string?)> { [ReciboEmitidoV1.Type] = ("RR", null) }, new Dictionary<Guid, int>(), new Dictionary<Guid, int>());
        var r = ConstructorDeComprobanteDeModulo.Construir(MatricesDeRecaudoYTesoreria.Recaudo, [Hecho(Recibo(1m), ReciboEmitidoV1.Type)], vacia);
        r.Fallos.Select(f => f.Role).Should().BeEquivalentTo([RolesDeRecaudo.Caja, RolesDeRecaudo.PuenteDelModulo]);
        ConsumoDeModulo.RechazoDe(r).Code.Should().Be("Accounting.ReceiptsRule.Missing");
    }

    [Fact]
    public void Las_dos_puntas_en_la_misma_cuenta_no_dejan_comprobante()
    {
        var caja = Cuenta("110505");
        var matriz = Matriz(MatricesDeRecaudoYTesoreria.Recaudo, [Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, caja), Regla<ReceiptsPostingRule>(RolesDeRecaudo.Boveda, caja)], [caja]);
        var c = new CatalogosDeModulo(matriz, new Dictionary<string, (string, string?)> { [MovimientoDeCajaRegistradoV1.Type] = ("MC", null) }, new Dictionary<Guid, int>(),
            new Dictionary<Guid, int> { [Sucursal] = 1 });
        var movimiento = new MovimientoDeCajaRegistradoV1
        {
            MovementPublicId = Guid.NewGuid(), Number = "MV-1", Kind = "SupplyFromVault",
            Lines =
            [
                new FactLineV1 { Role = RolesDeRecaudo.Caja, Side = RolesDeRecaudo.Debito, Amount = 50m, Dimensions = new ReceiptsFactDimensionsV1 { BranchCode = "01" } },
                new FactLineV1 { Role = RolesDeRecaudo.Boveda, Side = RolesDeRecaudo.Credito, Amount = 50m, Dimensions = new ReceiptsFactDimensionsV1 { BranchCode = "01" } },
            ],
        };

        var r = ConstructorDeComprobanteDeModulo.Construir(MatricesDeRecaudoYTesoreria.Recaudo, [Hecho(movimiento, MovimientoDeCajaRegistradoV1.Type)], c);

        r.SinLineas.Should().BeTrue();
        r.SinComprobante.Should().Be(RecepcionContableDeModulo.SinComprobanteMismasCuentas);
    }

    [Fact]
    public void Los_recibos_por_lote_se_juntan_por_dia_y_punto_y_su_resumen_suma_por_cuenta()
    {
        var clave = ClavesDeLote.Horario("REC.RCE", ClavesDeLote.CierreDeTurno, null, ClavesDeLote.GranularidadPorDia);
        var a = Mensaje("REC", Recibo(10m, punto: "P1"), ReciboEmitidoV1.Type, sucursal: Sucursal, scheduleKey: clave);
        var b = Mensaje("REC", Recibo(20m, caja: "C2", punto: "P1"), ReciboEmitidoV1.Type, sucursal: Sucursal, scheduleKey: clave);
        var otroPunto = Mensaje("REC", Recibo(30m, punto: "P2"), ReciboEmitidoV1.Type, sucursal: Sucursal, scheduleKey: clave);
        var enLinea = Mensaje("REC", Recibo(40m), ReciboEmitidoV1.Type, sucursal: Sucursal);

        var trabajos = AgrupadorDeRecaudo.Planear([a, b, otroPunto, enLinea]);

        trabajos.Where(t => t.ClaveDeGrupo is not null).Should().HaveCount(2);
        trabajos.Single(t => t.ClaveDeGrupo?.EndsWith("|P1", StringComparison.Ordinal) == true).Unidades.Should().HaveCount(2);
        trabajos.Count(t => t.ClaveDeGrupo is null).Should().Be(1, "lo que pasa en línea va solo");

        var (c, _, _) = Catalogos();
        var construcciones = new[] { a, b }.Select(m => ConstructorDeComprobanteDeModulo.Construir(MatricesDeRecaudoYTesoreria.Recaudo, [HechoContableDeModulo.De(m)], c)).ToList();
        var resumen = ConstructorDeComprobanteDeModulo.Resumen(MatricesDeRecaudoYTesoreria.Recaudo, construcciones, Guid.NewGuid(), 7);
        resumen.Lines.Should().HaveCount(2);
        resumen.Lines.Single(l => l.AccountCode == "110505").Debit.Should().Be(30m);
        resumen.Origin.SourceType.Should().Be("ReceiptsPostingBatch");
    }

    [Fact]
    public async Task El_enrutador_manda_lo_de_Recaudo_y_lo_de_Tesoreria_a_sus_comandos()
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<PostReceiptsFactsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(Guid.NewGuid(), "RR", "1")));
        sender.Send(Arg.Any<PostTreasuryFactsCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(Guid.NewGuid(), "TP", "1")));
        var destino = new DestinoContabilidad(sender, NullLogger<DestinoContabilidad>.Instance);
        var rec = new UnidadDeConsumo(IntegrationDestinations.Accounting, Guid.NewGuid(), ClavesDeEvento.Confirmacion, [Guid.NewGuid()], null, 0) { OriginModule = "REC" };
        var tes = new UnidadDeConsumo(IntegrationDestinations.Accounting, Guid.NewGuid(), ClavesDeEvento.Confirmacion, [Guid.NewGuid()], null, 0) { OriginModule = "TES" };

        var r = await destino.ConsumirAsync(new TrabajoDeConsumo(null, [rec, tes]), default);

        r.Select(x => ((ResultadoDeConsumo.Processed)x.Resultado).VoucherTypeCode).Should().Equal("RR", "TP");
        VersionesAceptadas.Acepta(ReciboEmitidoV1.Type, 1).Should().BeTrue();
        VersionesAceptadas.Acepta(PagoDeOrdenGiradoV1.Type, 1).Should().BeTrue();
    }

    [Fact]
    public void Lo_de_Recaudo_y_Tesoreria_se_corrige_desde_su_modulo()
    {
        var error = AccountingErrors.DocumentReceiptsOrTreasuryOwned(ModuloContable.Recaudo, "Receipt", Guid.NewGuid());
        error.Code.Should().Be("Accounting.Document.ModuleOwned");
        error.Message.Should().Contain("se corrige desde Recaudo");
        AccountingErrors.DocumentReceiptsOrTreasuryOwned(ModuloContable.Tesoreria, null, null).Message.Should().Contain("se corrige desde Tesorería");
    }
}
