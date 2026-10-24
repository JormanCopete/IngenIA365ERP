using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Periods;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Cuadres;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Activation;
using IngenIA365ERP.Application.Tests.Accounting.Lending;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Domain.Common.Cuadres;
using IngenIA365ERP.Domain.Entities.Receipts.Activation;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Accounting.Periods;

/// <summary>
/// Feature 015, US24, T0318 (FR-228; E-04; SC-029): el cierre del mes contable pregunta a Recaudo y Tesorería por <see cref="IEstadoOperativoParaElCierre"/> y niega el
/// mes de la activación con <c>Accounting.Period.OpeningSquarePending</c> mientras el cuadre de apertura siga provisional; el definitivo lo libera. La alerta diaria
/// <c>Recaudo.CuadreProvisionalPendiente</c> se repite con su clave, escala al vencer la fecha límite (otra clave, también a <c>Platform.GoLive.Override</c>) y el
/// definitivo la cierra. Prueba escrita, sin correr.
/// </summary>
public class GuardaDelCuadreProvisionalTests
{
    private readonly EscenarioContableDeCartera _c = new();
    private readonly EscenarioDeRecaudo _e = new();

    private static IEstadoOperativoParaElCierre Estado(params PendienteDeCierreDto[] pendientes)
    {
        var estado = Substitute.For<IEstadoOperativoParaElCierre>();
        estado.PendientesAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(pendientes);
        return estado;
    }

    private Task<Result> CerrarMarzoAsync(params IEstadoOperativoParaElCierre[] estados) =>
        new ClosePeriodCommandHandler(_c.D.Db, _c.D.Clock, _c.D.User, _c.Emisor, estados).Handle(new ClosePeriodCommand(2026, 3), default);

    [Fact]
    public async Task Un_cuadre_provisional_pendiente_niega_el_cierre_del_mes_de_la_activacion()
    {
        var r = await CerrarMarzoAsync(Estado(new PendienteDeCierreDto("REC", CuadreProvisionalPendiente.CodigoDelPendiente, "Recaudo sigue provisional.", Blocks: true)));

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Period.OpeningSquarePending");
        (await _c.D.Db.AccountingPeriods.SingleAsync(p => p.StartDate == new DateOnly(2026, 3, 1))).Status.Should().Be(PeriodStatus.Open);
    }

    [Fact]
    public async Task Sin_pendientes_que_bloqueen_el_mes_cierra()
    {
        var r = await CerrarMarzoAsync(Estado(), Estado(new PendienteDeCierreDto("TES", "Aviso", "Sólo informa.", Blocks: false)));

        r.IsSuccess.Should().BeTrue();
        (await _c.D.Db.AccountingPeriods.SingleAsync(p => p.StartDate == new DateOnly(2026, 3, 1))).Status.Should().Be(PeriodStatus.Closed);
    }

    private ReceiptsOpeningSquare Cuadre(OpeningSquareKind tipo, OpeningSquareState estado, DateOnly? limite = null)
    {
        var c = new ReceiptsOpeningSquare
        {
            Kind = tipo, Status = estado, CutoffDate = new DateOnly(2026, 11, 30), RegisteredByUserId = _e.Supervisor.Id, RegisteredAt = DateTime.UtcNow,
            DeadlineOn = limite, Reason = "Faltan las partidas al corte",
        };
        _e.Db.ReceiptsOpeningSquares.Add(c);
        _e.Db.SaveChanges();
        return c;
    }

    private void Activar(ReceiptsOpeningSquare cuadre)
    {
        _e.Db.ReceiptsActivations.Add(new ReceiptsActivation
        {
            ActivatedOn = new DateOnly(2026, 12, 1), CutoffDate = cuadre.CutoffDate, ActivatedByUserId = _e.Supervisor.Id, OpeningSquareId = cuadre.Id,
        });
        _e.Db.SaveChanges();
    }

    [Fact]
    public async Task Recaudo_informa_el_pendiente_solo_en_el_mes_de_la_activacion_y_mientras_siga_provisional()
    {
        var provisional = Cuadre(OpeningSquareKind.Provisional, OpeningSquareState.ProvisionalApproved, new DateOnly(2026, 12, 4));
        Activar(provisional);
        var estado = new EstadoOperativoDeRecaudoParaElCierre(_e.Db);

        (await estado.PendientesAsync(2026, 12, default)).Should().ContainSingle(p => p.Blocks && p.Code == CuadreProvisionalPendiente.CodigoDelPendiente);
        (await estado.PendientesAsync(2027, 1, default)).Should().BeEmpty("sólo se niega el mes de la activación");

        provisional.Status = OpeningSquareState.Superseded;
        _e.Db.SaveChanges();
        (await estado.PendientesAsync(2026, 12, default)).Should().BeEmpty("el definitivo reemplazó al provisional");
    }

    [Fact]
    public async Task Activado_con_un_definitivo_no_hay_pendiente()
    {
        Activar(Cuadre(OpeningSquareKind.Definitive, OpeningSquareState.Squared));

        (await new EstadoOperativoDeRecaudoParaElCierre(_e.Db).PendientesAsync(2026, 12, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task La_alerta_se_repite_con_su_clave_y_escala_al_vencer()
    {
        var alertas = Substitute.For<IAlertas>();
        var pendiente = new CuadreProvisionalPendienteDto(Guid.NewGuid(), new DateOnly(2026, 12, 1), new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 4), null);

        await CuadreProvisionalPendiente.AlertarAsync(alertas, TiposDeAlerta.RecaudoCuadreProvisionalPendiente, "Recaudo", "Receipts", "ReceiptsOpeningSquare", pendiente,
            new DateOnly(2026, 12, 3), default);
        await CuadreProvisionalPendiente.AlertarAsync(alertas, TiposDeAlerta.RecaudoCuadreProvisionalPendiente, "Recaudo", "Receipts", "ReceiptsOpeningSquare", pendiente,
            new DateOnly(2026, 12, 5), default);

        await alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.DedupKey == CuadreProvisionalPendiente.Clave(
            TiposDeAlerta.RecaudoCuadreProvisionalPendiente, pendiente.SquarePublicId, false) && a.RecipientPermissions == null), Arg.Any<CancellationToken>());
        await alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.DedupKey!.EndsWith(":vencido")
            && a.RecipientPermissions!.Contains(CuadreProvisionalPendiente.PermisoDelEscalamiento)), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_pendiente_no_alerta_y_el_definitivo_cierra_las_dos_claves()
    {
        var alertas = Substitute.For<IAlertas>();
        var cuadre = Guid.NewGuid();

        (await CuadreProvisionalPendiente.AlertarAsync(alertas, TiposDeAlerta.TesoreriaCuadreProvisionalPendiente, "Tesorería", "Treasury", "TreasuryOpeningSquare", null,
            new DateOnly(2026, 12, 3), default)).Should().Be(0);
        await CuadreProvisionalPendiente.CerrarAlertasAsync(alertas, TiposDeAlerta.TesoreriaCuadreProvisionalPendiente, cuadre, default);

        await alertas.DidNotReceiveWithAnyArgs().LevantarAsync(default!, default);
        await alertas.Received(1).AtenderPorProcesoAsync(CuadreProvisionalPendiente.Clave(TiposDeAlerta.TesoreriaCuadreProvisionalPendiente, cuadre, false), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await alertas.Received(1).AtenderPorProcesoAsync(CuadreProvisionalPendiente.Clave(TiposDeAlerta.TesoreriaCuadreProvisionalPendiente, cuadre, true), Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }
}
