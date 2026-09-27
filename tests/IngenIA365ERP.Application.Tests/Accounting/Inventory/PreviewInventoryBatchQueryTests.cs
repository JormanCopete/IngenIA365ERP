using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory;
using IngenIA365ERP.Application.Accounting.Inventory.Consultas;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T457 (T517; contracts/contabilidad.md §5.5; api.md §26.4): la vista previa de un lote. Devuelve los documentos,
/// los comprobantes propuestos (por documento o resumidos) y los excluidos con sus errores y quién los corrige, y el
/// <c>cutoffMessagePublicId</c> (nunca el <c>Id</c> interno). No numera ni guarda nada. Y el adaptador
/// <c>ContabilidadParaInventario</c> delega en las cuatro consultas.
/// </summary>
public class PreviewInventoryBatchQueryTests
{
    private static readonly DateOnly Fecha = new(2026, 3, 15);
    private readonly EscenarioContable E = new();

    private async Task<VistaPreviaDeLoteDto> PrevisualizarAsync(IEnumerable<Guid> mensajes)
    {
        var r = await new PreviewInventoryBatchQueryHandler(E.D.Db, new MensajesEntrantes(E.D.Db), E.D.Poster, new ResolutorDeReglas(E.D.Db),
            new TiposDeComprobanteDeInventario(E.D.Db), E.D.Clock).Handle(new PreviewInventoryBatchQuery(mensajes.ToList()), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        return r.Value;
    }

    private (IReadOnlyList<MensajeDeUnidad> A, IReadOnlyList<MensajeDeUnidad> B, IReadOnlyList<MensajeDeUnidad> Malo) Tres(bool resumido)
    {
        var horario = EscenarioContable.Horario("CO", resumido);
        var a = EscenarioContable.Compra(1000m, fecha: Fecha, numero: "REC-1");
        var b = EscenarioContable.Compra(500m, "ASEO", fecha: Fecha, numero: "REC-2");
        var malo = EscenarioContable.Compra(300m, "CARNES", fecha: Fecha, numero: "REC-3");
        foreach (var u in new[] { a, b, malo }) E.Emitir(u, DeliveryStatus.InBatch, null, horario);
        return (a, b, malo);
    }

    [Fact]
    public async Task Resumido_propone_un_comprobante_por_grupo_y_excluye_lo_que_falla()
    {
        var (a, b, malo) = Tres(resumido: true);
        var siguiente = (await E.D.Db.VoucherTypes.AsNoTracking().SingleAsync(v => v.Code == "EI")).NextNumber;

        var vista = await PrevisualizarAsync(new[] { a, b, malo }.SelectMany(u => u.Select(m => m.Sobre.MessageId)));

        vista.CutoffMessagePublicId.Should().Be(malo[0].Sobre.MessageId, "el último mensaje, por PublicId");
        vista.Documents.Should().HaveCount(3);
        var comprobante = vista.Vouchers.Should().ContainSingle().Subject;
        comprobante.Granularity.Should().Be(PostingGranularity.Summarized);
        comprobante.DocumentsCount.Should().Be(2);
        comprobante.OperationDate.Should().Be(Fecha);
        comprobante.VoucherTypeCode.Should().Be("EI");
        comprobante.Totals.Should().Be(new TotalesPropuestosDto(1500m, 1500m));
        comprobante.Lines.Where(l => l.Account.Code == "22050501").Should().HaveCount(2).And.OnlyContain(l => l.ThirdParty != null);
        var excluido = vista.Excluded.Should().ContainSingle().Subject;
        excluido.DocumentPublicId.Should().Be(malo[0].Sobre.Origin.PublicId);
        excluido.Errors.Single().Rule.Should().Be("Accounting.InventoryRule.Missing");
        excluido.Errors.Single().WhoFixes!.Page.Should().Be("/contabilidad/inventario/matriz");

        (await E.D.Db.AccountingDocuments.CountAsync()).Should().Be(0, "no guarda nada");
        (await E.D.Db.InventoryPostings.CountAsync()).Should().Be(0);
        (await E.D.Db.VoucherTypes.AsNoTracking().SingleAsync(v => v.Code == "EI")).NextNumber.Should().Be(siguiente, "no numera");
    }

    [Fact]
    public async Task Por_documento_propone_un_comprobante_por_documento()
    {
        var (a, b, _) = Tres(resumido: false);

        var vista = await PrevisualizarAsync(new[] { a, b }.SelectMany(u => u.Select(m => m.Sobre.MessageId)));

        vista.Vouchers.Should().HaveCount(2).And.OnlyContain(v => v.Granularity == PostingGranularity.PerDocument && v.DocumentsCount == 1);
        vista.Excluded.Should().BeEmpty();
    }

    [Fact]
    public async Task Los_relacionados_de_un_excluido_quedan_fuera_esperando()
    {
        var (_, _, malo) = Tres(resumido: true);
        var relacionado = EscenarioContable.Compra(200m, fecha: Fecha, numero: "REC-4",
            relacionado: new DocumentRefV1 { PublicId = malo[0].Sobre.Origin.PublicId, DocumentClass = DocumentClass.PurchaseReceipt, Number = "REC-3" });
        E.Emitir(relacionado, DeliveryStatus.InBatch, null, EscenarioContable.Horario("CO", true));

        var vista = await PrevisualizarAsync(new[] { malo, relacionado }.SelectMany(u => u.Select(m => m.Sobre.MessageId)));

        vista.Excluded.Should().HaveCount(2);
        vista.Excluded.Single(x => x.Number == "REC-4").Errors.Single().Rule.Should().Be("Accounting.InventoryMessage.WaitingForOriginal");
        vista.Vouchers.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_mensajes_no_hay_nada()
    {
        var vista = await PrevisualizarAsync([]);
        vista.Should().Be(new VistaPreviaDeLoteDto(null, [], [], []));
    }

    [Fact]
    public async Task El_adaptador_delega_en_las_cuatro_consultas()
    {
        var sender = Substitute.For<ISender>();
        var adaptador = new ContabilidadParaInventario(sender, E.D.Db);
        var corte = new DateOnly(2026, 3, 31);

        await adaptador.EvaluarAsync([], default);
        await adaptador.SaldosDeCuentasMapeadasAsync(corte, default);
        await adaptador.CompletitudAsync(corte, default);
        await adaptador.PrevisualizarLoteAsync([], default);

        await sender.Received(1).Send(Arg.Any<EvaluateInventoryPostingQuery>(), Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Is<InventoryAccountBalancesQuery>(q => q.Corte == corte), Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Is<InventoryRulesCompletenessQuery>(q => q.Date == corte), Arg.Any<CancellationToken>());
        await sender.Received(1).Send(Arg.Any<PreviewInventoryBatchQuery>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_destino_consume_por_los_comandos_y_una_excepcion_es_reintento()
    {
        var sender = Substitute.For<ISender>();
        var unidad = new UnidadDeConsumo(IntegrationDestinations.Accounting, Guid.NewGuid(), "Confirmation", [Guid.NewGuid()], null, 0);
        sender.Send(Arg.Any<PostInventoryMessagesCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(Guid.NewGuid(), "EI", "1")));
        var destino = new DestinoContabilidad(sender, Microsoft.Extensions.Logging.Abstractions.NullLogger<DestinoContabilidad>.Instance);

        destino.Destino.Should().Be(IntegrationDestinations.Accounting);
        destino.Acepta(CompraRecibidaV1.Type, 1).Should().BeTrue();
        destino.Acepta(CompraRecibidaV1.Type, 2).Should().BeFalse();
        destino.Acepta(VentaACreditoRegistradaV1.Type, 1).Should().BeFalse("es de Cartera");
        VersionesAceptadas.Todas.Should().HaveCount(18);
        (await destino.ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(unidad), default)).Single().Resultado.Should().BeOfType<ResultadoDeConsumo.Processed>();

        sender.Send(Arg.Any<PostInventoryMessagesCommand>(), Arg.Any<CancellationToken>()).Returns<Result<ResultadoDeConsumo>>(_ => throw new InvalidOperationException("base caída"));
        (await destino.ConsumirAsync(TrabajoDeConsumo.DeUnaUnidad(unidad), default)).Single().Resultado.Should().BeOfType<ResultadoDeConsumo.Retry>();
    }
}
