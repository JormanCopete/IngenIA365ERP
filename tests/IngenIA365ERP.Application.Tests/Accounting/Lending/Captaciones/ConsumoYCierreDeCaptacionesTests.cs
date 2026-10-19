using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Documents;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Periods;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using static IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones.EscenarioDeCaptaciones;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones;

/// <summary>
/// Feature 014, US8 (T146, T147, T148; contabilidad.md §8.3 y §8.4 de la 014; US8-3, US8-4): el consumidor de la Cartera contabiliza un hecho de captaciones
/// por el mismo camino de los créditos (<c>PostLendingFactsCommand</c> → <c>AccountingPoster</c>), el comprobante no se reversa desde Comprobantes —se corrige
/// en la Cartera con el reverso de la operación— y Contabilidad no cierra un mes que la Cartera, con captaciones activadas, no cerró, diciendo desde qué día
/// está pendiente la causación de captaciones.
/// </summary>
public class ConsumoYCierreDeCaptacionesTests
{
    private readonly EscenarioDeCaptaciones S = new();

    private IReadOnlyList<MensajeEntrante> Consignacion()
    {
        var unidad = S.Unidad("AccountDeposit", "CON-000090", new ConsignacionEnCuentaAplicadaV1
        {
            Account = Cuenta("AHVISTA-000090"), HolderPersonPublicId = S.Titular, Dimensions = S.AlaVista(), Collector = Recaudador("Receipts", S.Pagador),
            Amounts = [Valor("Principal", 250_000m)],
        });
        return unidad.Select(Entrante).ToList();
    }

    [Fact]
    public async Task US8_el_consumidor_contabiliza_la_consignacion_en_un_MA_y_ese_comprobante_no_se_reversa_desde_Comprobantes()
    {
        var unidad = Consignacion();
        S.E.Emitir(unidad);

        var r = await S.E.PorUnidad().Handle(new PostLendingFactsCommand(unidad.Select(m => m.MessageId).ToList(), null), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Should().BeOfType<ResultadoDeConsumo.Processed>().Which.VoucherTypeCode.Should().Be("MA");
        S.E.D.Db.ChangeTracker.Clear();
        var documento = await S.E.D.Db.AccountingDocuments.Include(d => d.Lines).SingleAsync();
        documento.OriginModule.Should().Be("CAR");
        documento.Status.Should().Be(DocumentStatus.Posted);
        documento.Lines.Should().Contain(l => l.CrossDocumentNumber == "AHVISTA-000090" && l.Credit == 250_000m);
        (await S.E.D.Db.LendingPostings.SingleAsync()).MessageType.Should().Be(ConsignacionEnCuentaAplicadaV1.Type);

        var reverso = await new ReverseDocumentCommandHandler(S.E.D.Db, S.E.D.Clock, S.E.D.Poster)
            .Handle(new ReverseDocumentCommand(documento.PublicId, "Quiero reversarlo"), default);

        reverso.IsFailure.Should().BeTrue();
        reverso.Error.Code.Should().Be("Accounting.Document.ModuleOwned");
        reverso.Error.Message.Should().Contain("captaciones y aportes").And.Contain("reverso de la operación de la cuenta");
    }

    private void Recibo(string tipo, DateOnly fecha, IntegrationMessageKind clase = IntegrationMessageKind.Informational)
    {
        S.E.D.Db.LendingPostings.Add(new LendingPosting
        {
            MessagePublicId = Guid.NewGuid(), MessageType = tipo, MessageKind = clase, SourceModule = "CAR", SourcePublicId = Guid.NewGuid(), OperationDate = fecha,
            NoVoucherReason = clase == IntegrationMessageKind.Informational ? LendingPosting.SinComprobanteInformativo : null,
            AccountingDocumentId = clase == IntegrationMessageKind.Informational ? null : 1, OriginActorKind = ActorKind.Process, OriginUserName = "Proceso",
            ActorKind = ActorKind.Process, ActorName = "Proceso de integración", ProcessedAt = DateTime.UtcNow, CreatedBy = "test",
        });
        S.E.D.Db.SaveChanges();
    }

    [Fact]
    public async Task US8_3_con_captaciones_activadas_y_sin_el_cierre_de_la_Cartera_el_mes_no_cierra_y_dice_desde_que_dia_falta_la_causacion()
    {
        Recibo(CaptacionesActivadasV1.Type, new DateOnly(2026, 11, 1));
        Recibo(InteresDeCaptacionCausadoV1.Type, new DateOnly(2026, 11, 20), IntegrationMessageKind.Business);

        var evaluacion = await CarteraEnElCierre.EvaluarAsync(S.E.D.Db, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), default);

        evaluacion.Bloqueo.Should().NotBeNull();
        evaluacion.Bloqueo!.Code.Should().Be("Accounting.Period.LendingNotClosed");
        evaluacion.Bloqueo.Message.Should().Contain("La causación de captaciones está pendiente desde el 2026-11-21");
        ((ErrorConDatos)evaluacion.Bloqueo).Data.Should().BeEquivalentTo(new { depositAccrualPendingFrom = new DateOnly(2026, 11, 21) });
    }
}
