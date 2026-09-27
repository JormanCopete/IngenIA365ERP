using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T454 (T513; contracts/contabilidad.md §5.3): el comprobante resumido. Un documento que falla queda
/// <c>Rejected</c> y sus relacionados del lote esperan sin tumbar el grupo; las líneas válidas se suman por cuenta, lado,
/// sucursal y centro, débitos y créditos por separado (sin netear); las que exigen tercero, cruce o base conservan su detalle;
/// un comprobante fechado en la fecha del grupo, con origen <c>InventoryPostingBatch</c> y sin usuario de origen, y una fila de
/// recibo por mensaje con el mismo comprobante y el lote.
/// </summary>
public class PostInventorySummaryGroupCommandHandlerTests
{
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private readonly EscenarioContable E = new();

    private PostInventorySummaryGroupCommandHandler Handler() => new(
        E.D.Db, new MensajesEntrantes(E.D.Db), new ConsumoDeInventario(E.D.Db, E.ActorActual, E.D.Clock, E.Emisor), E.D.Poster,
        new ResolutorDeReglas(E.D.Db), new TiposDeComprobanteDeInventario(E.D.Db), E.D.Clock);

    private async Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(Guid lote, params IReadOnlyList<MensajeDeUnidad>[] unidades)
    {
        var r = await Handler().Handle(new PostInventorySummaryGroupCommand(lote, "grupo",
            unidades.SelectMany(u => u.Select(m => m.Sobre.MessageId)).ToList()), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        E.D.Db.ChangeTracker.Clear();
        return r.Value;
    }

    [Fact]
    public async Task Un_documento_malo_no_tumba_el_resumen()
    {
        var lote = E.Lote(7);
        var horario = EscenarioContable.Horario("CO", resumido: true);
        var abarrotes = EscenarioContable.Compra(1000m, fecha: Fecha, numero: "REC-1");
        var aseo = EscenarioContable.Compra(500m, "ASEO", fecha: Fecha, numero: "REC-2");
        var malo = EscenarioContable.Compra(300m, "CARNES", fecha: Fecha, numero: "REC-3");
        var relacionado = EscenarioContable.Compra(200m, fecha: Fecha, numero: "REC-4",
            relacionado: new DocumentRefV1 { PublicId = malo[0].Sobre.Origin.PublicId, DocumentClass = DocumentClass.PurchaseReceipt, Number = "REC-3" });
        foreach (var u in new[] { abarrotes, aseo, malo, relacionado }) E.Emitir(u, DeliveryStatus.InBatch, lote, horario);

        var resultados = await ConsumirAsync(lote.PublicId, abarrotes, aseo, malo, relacionado);

        resultados.Should().HaveCount(4);
        var procesado = resultados[0].Resultado.Should().BeOfType<ResultadoDeConsumo.Processed>().Subject;
        resultados[1].Resultado.Should().Be(procesado, "los dos válidos salen en el mismo comprobante");
        resultados[2].Resultado.Should().BeOfType<ResultadoDeConsumo.Rejected>().Which.Code.Should().Be("Accounting.InventoryRule.Missing");
        resultados[3].Resultado.Should().BeOfType<ResultadoDeConsumo.Retry>().Which.Code.Should().Be("Accounting.InventoryMessage.WaitingForOriginal");

        var documento = await E.D.Db.AccountingDocuments.Include(d => d.Lines).ThenInclude(l => l.Account).Include(d => d.VoucherType).SingleAsync();
        documento.PublicId.Should().Be(procesado.AccountingDocumentPublicId!.Value);
        documento.VoucherType!.Code.Should().Be("EI");
        documento.Date.Should().Be(Fecha, "la fecha del grupo, nunca la del lote");
        documento.SourceType.Should().Be(OrigenesDeInventario.LoteResumido);
        documento.SourcePublicId.Should().Be(lote.PublicId);
        documento.RegisteredBy.Should().Be(E.D.User.UserName, "un resumido no tiene un usuario de origen: registra el actor");
        documento.Description.Should().Be("Lote 7 · CO · Principal · 2 documentos");
        documento.Lines.Where(l => l.Account!.Code == "14350501").Should().ContainSingle().Which.Debit.Should().Be(1000m);
        documento.Lines.Where(l => l.Account!.Code == "22050501").Should().HaveCount(2, "la mercancía por facturar exige tercero: conserva el detalle por documento")
            .And.OnlyContain(l => l.PersonId != null);

        var recibos = await E.D.Db.InventoryPostings.ToListAsync();
        recibos.Should().HaveCount(2).And.OnlyContain(p => p.AccountingDocumentId == documento.Id && p.BatchPublicId == lote.PublicId);
    }

    [Fact]
    public async Task Debitos_y_creditos_de_la_misma_cuenta_no_se_netean()
    {
        var lote = E.Lote();
        var ida = Reclasificacion("ABARROTES", "ASEO", 700m);
        var vuelta = Reclasificacion("ASEO", "ABARROTES", 300m);
        E.Emitir(ida, DeliveryStatus.InBatch, lote, EscenarioContable.Horario("RC", resumido: true));
        E.Emitir(vuelta, DeliveryStatus.InBatch, lote, EscenarioContable.Horario("RC", resumido: true));

        var resultados = await ConsumirAsync(lote.PublicId, ida, vuelta);

        resultados.Should().OnlyContain(r => r.Resultado is ResultadoDeConsumo.Processed);
        var lineas = await E.D.Db.JournalEntries.Include(l => l.Account).ToListAsync();
        lineas.Select(l => (l.Account!.Code, l.Debit, l.Credit)).Should().BeEquivalentTo(new[]
        {
            ("14350502", 700m, 0m), ("14350501", 0m, 700m), ("14350501", 300m, 0m), ("14350502", 0m, 300m),
        });
    }

    [Fact]
    public async Task Repetir_el_grupo_no_crea_nada()
    {
        var lote = E.Lote();
        var compra = EscenarioContable.Compra(1000m, fecha: Fecha);
        E.Emitir(compra, DeliveryStatus.InBatch, lote, EscenarioContable.Horario("CO", resumido: true));
        var primero = await ConsumirAsync(lote.PublicId, compra);

        var segundo = await ConsumirAsync(lote.PublicId, compra);

        segundo.Single().Resultado.Should().BeOfType<ResultadoDeConsumo.AlreadyProcessed>()
            .Which.AccountingDocumentPublicId.Should().Be(((ResultadoDeConsumo.Processed)primero.Single().Resultado).AccountingDocumentPublicId);
        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task El_resumen_conserva_tercero_cruce_y_base()
    {
        var unidad = EscenarioContable.Unidad(EscenarioContable.Caso("01-venta-pos-de-mensajes-14"));
        var c = ConstructorDeLineasDeInventario.Construir(unidad, await E.CatalogosAsync(unidad));

        var resumen = PostInventorySummaryGroupCommandHandler.Resumen([c, c], Guid.NewGuid(), 3, "Principal", c.Fecha);

        resumen.Origin.SourceType.Should().Be(OrigenesDeInventario.LoteResumido);
        resumen.RegistradoPor.Should().BeNull();
        resumen.Lines.Where(l => l.AccountCode == "13050502").Should().HaveCount(2, "tercero y cruce por documento")
            .And.OnlyContain(l => l.CrossDocumentType == "FV" && l.PersonId != null);
        resumen.Lines.Where(l => l.AccountCode == "24080501").Should().HaveCount(2).And.OnlyContain(l => l.TaxBase == 95000m);
        resumen.Lines.Where(l => l.AccountCode == "11050501").Should().ContainSingle().Which.Debit.Should().Be(120000m, "el efectivo se suma");
        resumen.Lines.Sum(l => l.Debit).Should().Be(resumen.Lines.Sum(l => l.Credit)).And.Be(2 * 267830.50m);
    }

    private static IReadOnlyList<MensajeDeUnidad> Reclasificacion(string de, string a, decimal valor)
    {
        var contenido = new GrupoContableReclasificadoV1
        {
            ProductPublicId = Guid.NewGuid(), ProductCode = "P", FromAccountingGroupCode = de, ToAccountingGroupCode = a, Reason = "prueba",
            Lines = [new ReclassificationLineV1 { WarehouseCode = "B01", BranchPublicId = EscenarioContable.Principal, QuantityBase = 1m, Value = valor }],
        };
        var sobre = EscenarioContable.Sobre(GrupoContableReclasificadoV1.Type, fecha: Fecha) with { OriginEventKey = "Reclassification" };
        sobre = sobre with { Origin = sobre.Origin with { Kind = MessageOriginKind.Operation, DocumentClass = null, DocumentTypeCode = "RC" } };
        return [new MensajeDeUnidad(sobre with { Payload = contenido }, contenido)];
    }
}
