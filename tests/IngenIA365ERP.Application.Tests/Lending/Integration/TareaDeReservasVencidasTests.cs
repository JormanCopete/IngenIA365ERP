using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Origination.Tareas;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US6, T0574 (FR-057, T22, C21; interfaces.md §5.2; DT §2.13): la tarea <c>cartera.reservas-vencidas</c> deja <c>Expired</c> las reservas
/// <c>Reserved</c> con <c>ExpiresOn</c> vencido (dejan de descontar cupo), nunca reasigna el número, alerta <c>Cartera.ReservaVencida</c> con su
/// <c>DedupKey</c>, sale de inmediato si Cartera no está activada y es idempotente.
/// </summary>
public sealed class TareaDeReservasVencidasTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private readonly List<AlertaALevantar> _levantadas = [];

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _d.Alertas.When(a => a.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>()))
            .Do(ci => _levantadas.Add(ci.Arg<AlertaALevantar>()));
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private LoanNumberReservation Reserva(string numero, DateOnly vence, LoanNumberReservationStatus estado = LoanNumberReservationStatus.Reserved)
    {
        var r = new LoanNumberReservation
        {
            Number = numero, PersonId = _d.O.Ana.Id, Amount = 100_000m, IdempotencyKey = "INV:" + numero, RequestSha256 = new string('a', 64),
            SalesDocumentPublicId = Guid.NewGuid(), PaymentPublicId = Guid.NewGuid(), Status = estado, ReservedAt = _d.G.UtcNow.AddDays(-10),
            ReservedByUserId = _d.Cajera.Id, ExpiresOn = vence,
        };
        _d.Db.LoanNumberReservations.Add(r);
        _d.Db.SaveChanges();
        _d.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<int> VencerAsync()
    {
        var r = await new ExpireLoanNumberReservationsCommandHandler(_d.Db, _d.Alertas, _d.G.Reloj).Handle(new ExpireLoanNumberReservationsCommand(), default);
        _d.Db.ChangeTracker.Clear();
        return r.Value;
    }

    [Fact]
    public async Task Vence_las_reservadas_con_el_plazo_pasado_y_alerta_sin_reasignar_el_numero()
    {
        var vencida = Reserva("CR-000901", DesembolsoDePrueba.Hoy.AddDays(-1));
        var vigente = Reserva("CR-000902", DesembolsoDePrueba.Hoy);
        Reserva("CR-000903", DesembolsoDePrueba.Hoy.AddDays(-3), LoanNumberReservationStatus.Used);

        (await VencerAsync()).Should().Be(1);

        var filas = await _d.Db.LoanNumberReservations.AsNoTracking().ToDictionaryAsync(r => r.Number);
        (filas["CR-000901"].Status, filas["CR-000901"].ExpiredAt is not null).Should().Be((LoanNumberReservationStatus.Expired, true));
        filas["CR-000902"].Status.Should().Be(LoanNumberReservationStatus.Reserved, "vence el día siguiente a su ExpiresOn");
        filas["CR-000903"].Status.Should().Be(LoanNumberReservationStatus.Used);
        filas.Should().HaveCount(3, "el número nunca se reasigna: no nace otra reserva");
        _levantadas.Should().ContainSingle().Which.Should().Match<AlertaALevantar>(a =>
            a.TypeCode == TiposDeAlerta.ReservaVencida && a.EntityPublicId == vencida.PublicId && a.DedupKey == $"{TiposDeAlerta.ReservaVencida}:{vencida.PublicId:D}");
        _ = vigente;
    }

    [Fact]
    public async Task Es_idempotente()
    {
        Reserva("CR-000911", DesembolsoDePrueba.Hoy.AddDays(-2));

        (await VencerAsync()).Should().Be(1);
        (await VencerAsync()).Should().Be(0);

        _levantadas.Should().ContainSingle();
    }

    [Fact]
    public async Task Sin_Cartera_activada_sale_de_inmediato()
    {
        Reserva("CR-000921", DesembolsoDePrueba.Hoy.AddDays(-2));
        var setup = await _d.Db.LendingSetups.SingleAsync();
        setup.ActivationDate = null;
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        (await VencerAsync()).Should().Be(0);
        (await _d.Db.LoanNumberReservations.AsNoTracking().SingleAsync()).Status.Should().Be(LoanNumberReservationStatus.Reserved);
    }

    [Fact]
    public async Task La_tarea_corre_su_comando_una_vez_al_dia_y_deja_su_marca()
    {
        var tarea = new TareaDeReservasVencidas();
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<IRequest<Result<int>>>(), Arg.Any<CancellationToken>()).Returns(Result.Success(0));
        var servicios = new ServiceCollection().AddSingleton(sender).BuildServiceProvider();

        await tarea.EjecutarAsync(servicios, default);

        tarea.Nombre.Should().Be("cartera.reservas-vencidas");
        tarea.DebeCorrer(new DateTimeOffset(2026, 12, 1, 7, 0, 0, TimeSpan.FromHours(-5)), null).Should().BeTrue();
        tarea.DebeCorrer(new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.FromHours(-5)), new DateTimeOffset(2026, 12, 1, 7, 0, 0, TimeSpan.FromHours(-5)))
            .Should().BeFalse();
        await sender.Received(1).Send(Arg.Is<IRequest<Result<int>>>(r => r is ExpireLoanNumberReservationsCommand), Arg.Any<CancellationToken>());
    }
}
