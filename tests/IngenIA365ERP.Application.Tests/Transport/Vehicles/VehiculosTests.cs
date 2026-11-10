using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Transport.Vehicles;

/// <summary>
/// Feature 020 (T060; US2; R3, R4; FR-005, FR-009, FR-010, FR-014, FR-016): el alta unitaria normaliza y exige unicidad entre vivos con el
/// vehículo que tiene el dato; los cambios de estado y propietario dejan historia, no van hacia atrás y sellan la clase de vinculación;
/// el retiro es terminal y libera la placa.
/// </summary>
public class VehiculosTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 20);
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public VehiculosTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc));
    }

    private Person Persona(string documento, bool asociado = false)
    {
        var p = new Person { FirstName = "Persona", LastName = documento, TaxId = documento, IdType = "C", Username = documento, IsAssociate = asociado };
        _db.People.Add(p);
        _db.SaveChanges();
        if (asociado)
        {
            _db.Associates.Add(new Associate { PersonId = p.Id, Person = p, Status = "A", JoinDate = new DateOnly(2020, 1, 1) });
            _db.SaveChanges();
        }
        return p;
    }

    private VehicleRegistrar Registrar() => new(_db, _reloj);

    private CambiosDeVehiculo Cambios() => new(_db, _reloj);

    private async Task<Vehicle> AltaAsync(string placa, string interno, Person dueño, string? radio = null)
    {
        var r = await Registrar().RegistrarAsync(new VehicleInput { Plate = placa, InternalNumber = interno, RadioCode = radio, JoinedOn = new DateOnly(2026, 1, 1) },
            dueño, ChangeSource.Screen, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : "");
        await _db.SaveChangesAsync();
        return r.Value;
    }

    [Fact]
    public async Task El_alta_normaliza_la_placa_y_registra_el_evento_con_la_clase_deducida()
    {
        var asociado = Persona("100", asociado: true);

        var v = await AltaAsync("abc-123", "0001", asociado);

        v.Plate.Should().Be("ABC123");
        v.Status.Should().Be(VehicleStatus.Active);
        var evento = await _db.VehicleEvents.SingleAsync();
        evento.Kind.Should().Be(VehicleEventKind.Registered);
        evento.ToAffiliation.Should().Be(AffiliationClass.Associate);
    }

    [Fact]
    public async Task Una_placa_numero_interno_o_radio_movil_repetido_nombra_al_vehiculo_vivo()
    {
        var dueño = Persona("200");
        var primero = await AltaAsync("AAA111", "0001", dueño, "101");

        var placa = await Registrar().RegistrarAsync(new VehicleInput { Plate = "aaa-111", InternalNumber = "0002" }, dueño, ChangeSource.Screen, default);
        var interno = await Registrar().RegistrarAsync(new VehicleInput { Plate = "BBB222", InternalNumber = "0001" }, dueño, ChangeSource.Screen, default);
        var radio = await Registrar().RegistrarAsync(new VehicleInput { Plate = "CCC333", InternalNumber = "0003", RadioCode = "101" }, dueño, ChangeSource.Screen, default);

        placa.Error.Code.Should().Be("Vehicle.PlateDuplicate");
        interno.Error.Code.Should().Be("Vehicle.InternalNumberDuplicate");
        radio.Error.Code.Should().Be("Vehicle.RadioCodeDuplicate");
        placa.Error.Message.Should().Contain("0001");
        _ = primero;
    }

    [Fact]
    public async Task El_retiro_es_terminal_y_libera_la_placa()
    {
        var dueño = Persona("300");
        var v = await AltaAsync("DDD444", "0004", dueño);

        (await Cambios().RetirarAsync(v, Hoy, "Venta fuera de la cooperativa", ChangeSource.Screen, default)).IsSuccess.Should().BeTrue();
        await _db.SaveChangesAsync();

        (await Cambios().CambiarEstadoAsync(v, VehicleStatus.Active, Hoy, "Reactivar", ChangeSource.Screen, default)).Error.Code.Should().Be("Vehicle.Retired");
        var reingreso = await Registrar().RegistrarAsync(new VehicleInput { Plate = "DDD444", InternalNumber = "0004" }, dueño, ChangeSource.Screen, default);
        reingreso.IsSuccess.Should().BeTrue("un retirado libera placa y número interno: el reingreso es un vehículo nuevo");
    }

    [Fact]
    public async Task Un_cambio_no_va_antes_del_ultimo_del_mismo_tipo()
    {
        var dueño = Persona("400");
        var v = await AltaAsync("EEE555", "0005", dueño);
        (await Cambios().CambiarEstadoAsync(v, VehicleStatus.Inactive, new DateOnly(2026, 6, 1), "Suspensión", ChangeSource.Screen, default)).IsSuccess.Should().BeTrue();
        await _db.SaveChangesAsync();

        var atras = await Cambios().CambiarEstadoAsync(v, VehicleStatus.Active, new DateOnly(2026, 5, 1), "Reactivar", ChangeSource.Screen, default);

        atras.Error.Code.Should().Be("Vehicle.EffectiveDateBeforeLastChange");
    }

    [Fact]
    public async Task El_cambio_de_propietario_sella_la_clase_de_cada_lado()
    {
        var asociado = Persona("500", asociado: true);
        var externo = Persona("501");
        var v = await AltaAsync("FFF666", "0006", asociado);

        (await Cambios().CambiarPropietarioAsync(v, externo, Hoy, "Venta", ChangeSource.Screen, default)).IsSuccess.Should().BeTrue();
        await _db.SaveChangesAsync();

        var evento = await _db.VehicleEvents.SingleAsync(e => e.Kind == VehicleEventKind.OwnerChanged);
        evento.FromAffiliation.Should().Be(AffiliationClass.Associate);
        evento.ToAffiliation.Should().Be(AffiliationClass.External);
        v.OwnerPersonId.Should().Be(externo.Id);
    }
}
