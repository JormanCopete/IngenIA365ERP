using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Periods;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Lending;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Periods;

/// <summary>
/// Feature 013, T0622 (T0652; contracts/contabilidad.md §12; T57, FR-146): Contabilidad no cierra un mes que Cartera, activada, no cerró. Lo sabe
/// por los recibos de los informativos de Cartera en <c>ACC_LendingPostings</c> y por las entregas, sin leer <c>LND_</c>: (a) bloqueo sin
/// reconocimiento; (b) aviso de hechos de Cartera pendientes en el mes, reconocible; (c) aviso de la carga que el cierre dejaría sin activar,
/// reconocible. Sin <c>CarteraActivada</c>, cierra como hoy; los reconocimientos quedan en el evento de cierre.
/// </summary>
public class ClosePeriodConCarteraTests
{
    private readonly EscenarioContableDeCartera E = new();

    private async Task<Result> CerrarMarzoAsync(bool pendientes = false, bool carga = false) =>
        await new ClosePeriodCommandHandler(E.D.Db, E.D.Clock, E.D.User, E.Emisor)
            .Handle(new ClosePeriodCommand(2026, 3, AcknowledgeLendingPending: pendientes, AcknowledgeLendingLoad: carga), default);

    private LendingPosting Recibo(string tipo, DateOnly fecha, Guid? origen = null, Guid? relacionado = null)
    {
        var recibo = new LendingPosting
        {
            MessagePublicId = Guid.NewGuid(), MessageType = tipo, MessageKind = IntegrationMessageKind.Informational, SourceModule = "CAR",
            SourcePublicId = origen ?? Guid.NewGuid(), OperationDate = fecha, RelatedMessagePublicId = relacionado, NoVoucherReason = LendingPosting.SinComprobanteInformativo,
            OriginActorKind = ActorKind.Person, OriginUserName = "Usuario de ensayo", ActorKind = ActorKind.Process, ActorName = "Proceso de integración",
            ProcessedAt = DateTime.UtcNow, CreatedBy = "test",
        };
        E.D.Db.LendingPostings.Add(recibo);
        E.D.Db.SaveChanges();
        return recibo;
    }

    [Fact]
    public async Task Cartera_activada_sin_el_cierre_del_mes_bloquea_aunque_se_reconozca_todo()
    {
        Recibo(CarteraActivadaV1.Type, new DateOnly(2026, 2, 1));
        Recibo(CierreDeCarteraRealizadoV1.Type, new DateOnly(2026, 2, 28));

        var r = await CerrarMarzoAsync(pendientes: true, carga: true);

        r.Error.Code.Should().Be("Accounting.Period.LendingNotClosed");
        ((ErrorConDatos)r.Error).Data.Should().BeEquivalentTo(new
        {
            month = "2026-03", activationDate = new DateOnly(2026, 2, 1), lastLendingClose = new { month = "2026-02", date = new DateOnly(2026, 2, 28) },
        });
        (await E.D.Db.AccountingPeriods.SingleAsync(p => p.StartDate == new DateOnly(2026, 3, 1))).Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public async Task Con_el_cierre_de_Cartera_del_mes_vigente_Contabilidad_cierra()
    {
        Recibo(CarteraActivadaV1.Type, new DateOnly(2026, 3, 1));
        Recibo(CierreDeCarteraRealizadoV1.Type, new DateOnly(2026, 3, 31));

        (await CerrarMarzoAsync()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Un_cierre_de_Cartera_reabierto_ya_no_vale()
    {
        Recibo(CarteraActivadaV1.Type, new DateOnly(2026, 3, 1));
        var cierre = Recibo(CierreDeCarteraRealizadoV1.Type, new DateOnly(2026, 3, 31));
        Recibo(CierreDeCarteraReabiertoV1.Type, new DateOnly(2026, 3, 31), cierre.SourcePublicId, cierre.MessagePublicId);

        (await CerrarMarzoAsync()).Error.Code.Should().Be("Accounting.Period.LendingNotClosed");
    }

    [Fact]
    public async Task La_regla_rige_desde_el_mes_de_la_activacion()
    {
        Recibo(CarteraActivadaV1.Type, new DateOnly(2026, 4, 1));

        (await CerrarMarzoAsync()).IsSuccess.Should().BeTrue("Cartera se activó después del fin de marzo");
    }

    [Fact]
    public async Task Hechos_de_Cartera_pendientes_en_el_mes_avisan_y_se_reconocen()
    {
        var hecho = EscenarioContableDeCartera.Copia(EscenarioContableDeCartera.UnidadDe("01-desembolso-por-transferencia"), fecha: new DateOnly(2026, 3, 10));
        E.Emitir(hecho, DeliveryStatus.Rejected);

        var aviso = await CerrarMarzoAsync();

        aviso.Error.Code.Should().Be("Accounting.Period.LendingPending");
        ((ErrorConDatos)aviso.Error).Data.Should().BeEquivalentTo(new { pending = 0, inBatch = 0, rejected = 1 }, o => o.ExcludingMissingMembers());
        (await CerrarMarzoAsync(pendientes: true)).IsSuccess.Should().BeTrue();
        await E.Auditoria.Received().AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == "Accounting.Period.Closed"
            && a.NewValuesJson!.Contains("\"lendingPendingAcknowledged\":true")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_carga_que_vence_en_el_mes_sin_activacion_avisa_y_se_reconoce()
    {
        var carga = Recibo(CargaDeCarteraAplicadaV1.Type, new DateOnly(2026, 3, 15));

        var aviso = await CerrarMarzoAsync();

        aviso.Error.Code.Should().Be("Accounting.Period.LendingLoadWillExpire");
        ((ErrorConDatos)aviso.Error).Data.Should().BeEquivalentTo(new { cutoffDate = new DateOnly(2026, 3, 15), loadPublicId = carga.SourcePublicId });
        (await CerrarMarzoAsync(carga: true)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Una_carga_ya_activada_no_avisa()
    {
        Recibo(CargaDeCarteraAplicadaV1.Type, new DateOnly(2026, 3, 15));
        Recibo(CarteraActivadaV1.Type, new DateOnly(2026, 4, 2));

        (await CerrarMarzoAsync()).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_Cartera_activada_cierra_como_hoy()
    {
        (await CerrarMarzoAsync()).IsSuccess.Should().BeTrue();
    }
}
