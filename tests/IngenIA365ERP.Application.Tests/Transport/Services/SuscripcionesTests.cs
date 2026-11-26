using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Services;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Transport.Services;

/// <summary>
/// 2026-11-25 (pedido del dueño): terminar una suscripción con la fecha que se elija (desde su inicio), quitar la que no tiene cargos de la
/// facturación mensual en su vigencia (la que todavía no empieza o nunca se facturó) y mover su inicio con la misma condición. Lo facturado
/// no se borra: se termina.
/// </summary>
public class SuscripcionesTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 25);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();
    private Vehicle _vehiculo = null!;
    private TransportService _servicio = null!;

    public SuscripcionesTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(new DateTime(2026, 11, 25, 15, 0, 0, DateTimeKind.Utc));
        _usuario.UserName.Returns("operador");
    }

    private SuscripcionDeServicio Suscripciones() => new(_db, _reloj, _usuario);

    private async Task<VehicleSubscription> SembrarAsync(DateOnly desde, DateOnly? hasta = null, decimal? precio = null)
    {
        var dueño = new Person { TaxId = "1010", FirstName = "Ana", LastName = "Prueba", IdType = "CC" };
        _db.People.Add(dueño);
        await _db.SaveChangesAsync();
        _vehiculo = Vehicle.Registrar(dueño, new DateOnly(2026, 1, 1));
        _vehiculo.Plate = "WDK622";
        _vehiculo.InternalNumber = "622";
        _db.Vehicles.Add(_vehiculo);
        _servicio = new TransportService { ProductPublicId = Guid.NewGuid(), ProductCode = "SERV01009", Name = "Administración", Priority = 1 };
        _db.TransportServices.Add(_servicio);
        await _db.SaveChangesAsync();

        var alta = await Suscripciones().SuscribirAsync(_vehiculo, _servicio, desde, hasta, precio, "Alta", default);
        alta.IsSuccess.Should().BeTrue(alta.IsFailure ? alta.Error.Message : "");
        await _db.SaveChangesAsync();
        return alta.Value;
    }

    private async Task CargoAsync(string periodo, ChargeStatus estado = ChargeStatus.Open, ChargeOrigin origen = ChargeOrigin.Billing)
    {
        _db.ServiceCharges.Add(new ServiceCharge
        {
            VehicleId = _vehiculo.Id, ServiceId = _servicio.Id, Period = periodo, Origin = origen, Status = estado, InvoicePublicId = Guid.NewGuid(),
            InvoiceNumber = "FE-1", InvoiceLinePublicId = Guid.NewGuid(), Amount = 73_425m, Route = ChargeRoute.Lending,
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task Terminar_acepta_la_fecha_que_se_elija_desde_el_inicio_y_cierra_el_precio_pactado()
    {
        var s = await SembrarAsync(new DateOnly(2026, 11, 25), precio: 73_425m);

        var r = Suscripciones().Terminar(s, new DateOnly(2026, 12, 31), "Se retira de la administración");

        r.IsSuccess.Should().BeTrue();
        s.To.Should().Be(new DateOnly(2026, 12, 31));
        s.Reason.Should().Be("Se retira de la administración");
        s.Prices.Single().To.Should().Be(new DateOnly(2026, 12, 31));
    }

    [Fact]
    public async Task Terminar_antes_de_empezar_se_rechaza_y_sugiere_quitarla()
    {
        var s = await SembrarAsync(new DateOnly(2026, 12, 1));

        var r = Suscripciones().Terminar(s, Hoy, "Terminar hoy");

        r.Error.Code.Should().Be("Subscription.Dates");
        r.Error.Message.Should().Contain("quítela");
        s.To.Should().BeNull();
    }

    [Fact]
    public async Task Alargar_el_fin_lleva_consigo_el_precio_que_cerraba_con_el_fin_anterior()
    {
        var s = await SembrarAsync(new DateOnly(2026, 11, 25), new DateOnly(2026, 12, 31), 73_425m);

        Suscripciones().Terminar(s, new DateOnly(2027, 3, 31), "Prórroga").IsSuccess.Should().BeTrue();

        s.Prices.Single().To.Should().Be(new DateOnly(2027, 3, 31));
    }

    [Fact]
    public async Task Quitar_una_que_todavia_no_empieza_la_da_de_baja_con_quien_y_el_motivo()
    {
        var s = await SembrarAsync(new DateOnly(2026, 12, 1), precio: 73_425m);

        var r = await Suscripciones().QuitarAsync(s, "Se cargó por error", default);
        await _db.SaveChangesAsync();

        r.IsSuccess.Should().BeTrue();
        s.IsDeleted.Should().BeTrue();
        s.DeletedBy.Should().Be("operador");
        s.DeletedAt.Should().NotBeNull();
        s.Reason.Should().StartWith("Quitada: Se cargó por error");
        s.Prices.Should().OnlyContain(p => p.IsDeleted);
    }

    [Fact]
    public async Task Quitar_una_ya_vigente_que_nunca_se_facturo_tambien_se_puede()
    {
        var s = await SembrarAsync(new DateOnly(2026, 11, 1));
        await CargoAsync("202611", ChargeStatus.Voided);
        await CargoAsync("202611", origen: ChargeOrigin.Sale);

        var r = await Suscripciones().QuitarAsync(s, "Nunca debió suscribirse", default);

        r.IsSuccess.Should().BeTrue("un cargo anulado o de una venta suelta con la placa no es facturación de la suscripción");
    }

    [Fact]
    public async Task Quitar_una_facturada_se_rechaza_y_dice_el_periodo()
    {
        var s = await SembrarAsync(new DateOnly(2026, 11, 25));
        await CargoAsync("202611");

        var r = await Suscripciones().QuitarAsync(s, "Error", default);

        r.Error.Code.Should().Be("Subscription.Billed");
        r.Error.Message.Should().Contain("noviembre 2026");
        s.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Un_cargo_de_un_periodo_fuera_de_la_vigencia_no_cuenta_como_facturado()
    {
        var s = await SembrarAsync(new DateOnly(2026, 12, 1));
        await CargoAsync("202610");

        (await Suscripciones().PeriodosFacturadosAsync(s, default)).Should().BeEmpty();
    }

    [Fact]
    public async Task Cambiar_el_inicio_de_una_sin_cargos_mueve_tambien_su_primer_precio()
    {
        var s = await SembrarAsync(new DateOnly(2026, 11, 25), precio: 73_425m);

        var r = await Suscripciones().CambiarInicioAsync(s, new DateOnly(2026, 12, 1), "Noviembre ya estaba cobrado", default);

        r.IsSuccess.Should().BeTrue();
        s.From.Should().Be(new DateOnly(2026, 12, 1));
        s.Prices.Single().From.Should().Be(new DateOnly(2026, 12, 1));
        s.Reason.Should().Contain("del 25/11/2026 al 01/12/2026");
    }

    [Fact]
    public async Task Cambiar_el_inicio_de_una_facturada_se_rechaza()
    {
        var s = await SembrarAsync(new DateOnly(2026, 11, 25));
        await CargoAsync("202611");

        var r = await Suscripciones().CambiarInicioAsync(s, new DateOnly(2026, 12, 1), "Corrección", default);

        r.Error.Code.Should().Be("Subscription.Billed");
        s.From.Should().Be(new DateOnly(2026, 11, 25));
    }

    [Fact]
    public async Task Los_comandos_de_terminar_y_quitar_buscan_la_suscripcion_del_vehiculo_y_guardan()
    {
        var s = await SembrarAsync(new DateOnly(2026, 12, 1));

        var terminar = await new EndSubscriptionCommandHandler(_db, Suscripciones())
            .Handle(new EndSubscriptionCommand(_vehiculo.PublicId, s.PublicId, new DateOnly(2027, 1, 31), "Fin de contrato"), default);
        terminar.IsSuccess.Should().BeTrue();

        var quitar = await new RemoveSubscriptionCommandHandler(_db, Suscripciones())
            .Handle(new RemoveSubscriptionCommand(_vehiculo.PublicId, s.PublicId, "Se cargó por error"), default);
        quitar.IsSuccess.Should().BeTrue();

        var guardada = await _db.VehicleSubscriptions.IgnoreQueryFilters().SingleAsync(x => x.Id == s.Id);
        guardada.To.Should().Be(new DateOnly(2027, 1, 31));
        guardada.IsDeleted.Should().BeTrue();

        var otra = await new RemoveSubscriptionCommandHandler(_db, Suscripciones())
            .Handle(new RemoveSubscriptionCommand(_vehiculo.PublicId, s.PublicId, "Otra vez"), default);
        otra.Error.Code.Should().Be("Subscription.NotFound", "una quitada ya no se encuentra");
    }

    [Fact]
    public void Terminar_quitar_y_mover_inicio_exigen_motivo()
    {
        new EndSubscriptionCommandValidator().Validate(new EndSubscriptionCommand(Guid.NewGuid(), Guid.NewGuid(), Hoy, "")).IsValid.Should().BeFalse();
        new RemoveSubscriptionCommandValidator().Validate(new RemoveSubscriptionCommand(Guid.NewGuid(), Guid.NewGuid(), " ")).IsValid.Should().BeFalse();
        new ChangeSubscriptionStartCommandValidator().Validate(new ChangeSubscriptionStartCommand(Guid.NewGuid(), Guid.NewGuid(), Hoy, "")).IsValid.Should().BeFalse();
        new EndSubscriptionCommandValidator().Validate(new EndSubscriptionCommand(Guid.NewGuid(), Guid.NewGuid(), Hoy, "Retiro")).IsValid.Should().BeTrue();
    }
}
