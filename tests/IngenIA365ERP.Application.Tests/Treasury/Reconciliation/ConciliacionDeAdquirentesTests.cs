using System.Text;
using FluentAssertions;
using IngenIA365ERP.Application.Common.BankFiles;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Integration.Inventory;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Reconciliation.Acquirers;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Reconciliation;

/// <summary>
/// Feature 015, T2, fase 44 (T0462; FR-133, FR-218; US22 «Independent Test»): un día con dos lotes de la red —uno cobrado en Recaudo y otro en el punto de venta— y su abono
/// con comisión, IVA y retención: dentro de la tolerancia la liquidación queda explicada y nace <c>ConciliacionDeAdquirenteRegistrada</c> con banco, comisión, IVA y
/// retenciones contra el medio por cobrar (cuadrado); fuera de ella queda por aprobar, quien la cargó no la decide y otra persona la aprueba; una línea que no suma o un
/// archivo repetido no se guardan. Prueba escrita, sin correr.
/// </summary>
public class ConciliacionDeAdquirentesTests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly ILotesDeDatafonoDeRecaudo _deRecaudo = Substitute.For<ILotesDeDatafonoDeRecaudo>();
    private readonly ILotesDeDatafonoDelPuntoDeVenta _delPuntoDeVenta = Substitute.For<ILotesDeDatafonoDelPuntoDeVenta>();
    private readonly CardAcquirer _red;
    private readonly BankFileFormat _formato;

    private static DateOnly Hoy => EscenarioDeRecaudo.Hoy;

    public ConciliacionDeAdquirentesTests()
    {
        _t.E.Como(_t.E.Cajera);
        _t.E.Parametro(ParametrosDeRecaudoYTesoreria.AdquirentesToleranciaDeConciliacion, "1000", ParametrosDeRecaudoYTesoreria.ModuloTesoreria);
        var persona = new Person { FirstName = "Red", LastName = "De Tarjetas", TaxId = "800999", IdType = "N" };
        _t.E.Db.People.Add(persona);
        _t.E.Db.SaveChanges();
        _red = new CardAcquirer { Code = "REDTAR", Name = "Red de tarjetas", PersonId = persona.Id };
        _t.E.Db.CardAcquirers.Add(_red);
        _t.E.Db.SaveChanges();
        var t1 = new CardTerminal { CardAcquirerId = _red.Id, Code = "T1" };
        var t2 = new CardTerminal { CardAcquirerId = _red.Id, Code = "T2" };
        _t.E.Db.CardTerminals.AddRange(t1, t2);
        _formato = new BankFileFormat();
        BankFileFormatDefinition.Parse("""
            { "code": "RED-PRUEBA", "name": "Liquidación de prueba", "scope": "AcquirerSettlement", "validFrom": "2026-01-01", "kind": "Delimited",
              "delimiter": ";", "quoteText": false, "encoding": "utf-8", "lineEnding": "CRLF", "uppercase": false, "stripAccents": false, "amountFormat": "Point2",
              "fileName": "LIQUIDACION.csv", "contentType": "text/csv",
              "records": {
                "header": { "enabled": false, "fields": [] },
                "detail": { "enabled": true, "fields": [
                  { "order": 1, "name": "Fecha", "source": "MovementDate", "format": "yyyy-MM-dd", "required": true },
                  { "order": 2, "name": "Datafono", "source": "TerminalCode", "required": true },
                  { "order": 3, "name": "Lote", "source": "TerminalBatchNumber" },
                  { "order": 4, "name": "Bruto", "source": "GrossAmount", "required": true },
                  { "order": 5, "name": "Comision", "source": "CommissionAmount" },
                  { "order": 6, "name": "Iva", "source": "CommissionVatAmount" },
                  { "order": 7, "name": "Retefuente", "source": "WithholdingTaxAmount" },
                  { "order": 8, "name": "Neto", "source": "SettledAmount" } ] },
                "trailer": { "enabled": false, "fields": [] } } }
            """)!.AplicarA(_formato, bankId: null);
        _t.E.Db.BankFileFormats.Add(_formato);
        _t.E.Db.SaveChanges();
        _deRecaudo.PorFechaAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<LoteDeDatafonoDto> { new(t1.PublicId, "T1", _red.PublicId, "REDTAR", "L1", Hoy, 500_000m) });
        CobradoEnElPuntoDeVenta(t2.PublicId, 300_000m);
    }

    private void CobradoEnElPuntoDeVenta(Guid terminal, decimal valor) =>
        _delPuntoDeVenta.PorFechaAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<LoteDeDatafonoDto> { new(terminal, "T2", _red.PublicId, "REDTAR", "L7", Hoy, valor) });

    private LoadAcquirerSettlementCommandHandler Cargar() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Numerador, _t.Parametros, _t.Emision,
        new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _t.E.Reloj, _deRecaudo, _delPuntoDeVenta);

    private DecideAcquirerSettlementCommandHandler Decidir() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Emision,
        new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _t.E.Reloj);

    private static byte[] Archivo(params string[] lineas) => Encoding.UTF8.GetBytes(string.Join("\r\n", lineas));

    private static byte[] DosLotes() => Archivo(
        $"{Hoy:yyyy-MM-dd};T1;L1;500000.00;10000.00;1900.00;7500.00;480600.00",
        $"{Hoy:yyyy-MM-dd};T2;L7;300000.00;6000.00;1140.00;4500.00;288360.00");

    private Task<Application.Common.Models.Result<AcquirerSettlementDto>> CargarAsync(byte[] contenido) =>
        Cargar().Handle(new LoadAcquirerSettlementCommand(_red.PublicId, _t.Pagos.PublicId, _formato.PublicId, "liquidacion.csv", contenido, Hoy)
            { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task Dentro_de_la_tolerancia_queda_explicada_y_su_hecho_desagrega_comision_iva_y_retenciones()
    {
        var r = await CargarAsync(DosLotes());

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Status.Should().Be(AcquirerSettlementState.Explained);
        (r.Value.Gross, r.Value.Commission, r.Value.Vat, r.Value.WithholdingTax, r.Value.Net).Should().Be((800_000m, 16_000m, 3_040m, 12_000m, 768_960m));
        r.Value.Collected.Should().Be(800_000m);
        r.Value.Lines.Select(l => l.Source).Should().BeEquivalentTo([AcquirerBatchSource.Receipts, AcquirerBatchSource.PointOfSale]);
        var hecho = await _t.E.Db.IntegrationMessages.SingleAsync(m => m.Type == ConciliacionDeAdquirenteRegistradaV1.Type);
        r.Value.FactMessagePublicId.Should().Be(hecho.PublicId);
    }

    [Fact]
    public async Task Sobre_la_tolerancia_queda_por_aprobar_y_la_decide_otra_persona()
    {
        var t2 = await _t.E.Db.CardTerminals.SingleAsync(t => t.Code == "T2");
        CobradoEnElPuntoDeVenta(t2.PublicId, 290_000m);

        var r = await CargarAsync(DosLotes());

        r.Value.Status.Should().Be(AcquirerSettlementState.PendingApproval);
        r.Value.Difference.Should().Be(-10_000m);
        r.Value.FactMessagePublicId.Should().BeNull();
        (await Decidir().Handle(new DecideAcquirerSettlementCommand(r.Value.PublicId, true, "Revisado") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be("Treasury.AcquirerSettlement.SelfApproval");

        _t.E.Como(_t.E.Supervisor);
        var aprobada = await Decidir().Handle(new DecideAcquirerSettlementCommand(r.Value.PublicId, true, "La red liquida el resto mañana") { OperationKey = Guid.NewGuid() },
            default);

        aprobada.IsSuccess.Should().BeTrue(aprobada.IsFailure ? $"{aprobada.Error.Code}: {aprobada.Error.Message}" : null);
        aprobada.Value.Status.Should().Be(AcquirerSettlementState.Approved);
        (await _t.E.Db.IntegrationMessages.CountAsync(m => m.Type == ConciliacionDeAdquirenteRegistradaV1.Type)).Should().Be(1);
    }

    [Fact]
    public async Task Una_linea_que_no_suma_no_guarda_nada()
    {
        var r = await CargarAsync(Archivo($"{Hoy:yyyy-MM-dd};T1;L1;500000.00;10000.00;1900.00;7500.00;490000.00"));

        r.Error.Code.Should().Be("Treasury.AcquirerSettlement.Illegible");
        (await _t.E.Db.TreasuryAcquirerSettlements.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task El_mismo_archivo_no_se_carga_dos_veces()
    {
        (await CargarAsync(DosLotes())).IsSuccess.Should().BeTrue();
        (await CargarAsync(DosLotes())).Error.Code.Should().Be("Treasury.AcquirerSettlement.AlreadyLoaded");
    }

    [Fact]
    public async Task Un_lote_que_nadie_cobro_queda_con_toda_su_diferencia()
    {
        var r = await CargarAsync(Archivo($"{Hoy:yyyy-MM-dd};T9;L99;100000.00;2000.00;380.00;0.00;97620.00"));

        r.Value.Lines.Single().Should().Match<AcquirerSettlementLineDto>(l => l.Source == null && l.Difference == -100_000m);
        r.Value.Status.Should().Be(AcquirerSettlementState.PendingApproval);
    }
}
