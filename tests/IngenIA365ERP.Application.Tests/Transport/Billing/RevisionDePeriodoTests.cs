using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Billing;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Transport.Billing;

/// <summary>
/// 2026-11-25 (pedido del dueño): antes de facturar, avisar si el período es anterior al mes en curso o si tiene suscripciones por facturar
/// que empezaron después del día 1 —en COOFLOPAL noviembre ya venía cobrado de la carga y las suscripciones rigen desde el 25/11—. Es un
/// aviso: sin nada que advertir, la lista va vacía. Cuenta sólo lo que la corrida facturaría.
/// </summary>
public class RevisionDePeriodoTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 25);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private TransportService _servicio = null!;
    private Vehicle _v1 = null!, _v2 = null!;

    public RevisionDePeriodoTests() => _reloj.HoyLocal.Returns(Hoy);

    private void Sembrar(ProrationPolicy politica = ProrationPolicy.FullMonth)
    {
        var dueño = new Person { TaxId = "1010", IdType = "CC", FirstName = "Ana", LastName = "Prueba" };
        _db.People.Add(dueño);
        _db.SaveChanges();
        _servicio = new TransportService { ProductPublicId = Guid.NewGuid(), ProductCode = "SERV01009", Name = "Administración", Priority = 1 };
        _db.TransportServices.Add(_servicio);
        _v1 = Vehicle.Registrar(dueño, new DateOnly(2026, 1, 1));
        (_v1.Plate, _v1.InternalNumber) = ("AAA111", "0001");
        _v2 = Vehicle.Registrar(dueño, new DateOnly(2026, 1, 1));
        (_v2.Plate, _v2.InternalNumber) = ("BBB222", "0002");
        _db.Vehicles.AddRange(_v1, _v2);
        _db.TransportBillingPolicies.Add(new BillingPolicy { ValidFrom = new DateOnly(2026, 1, 1), Proration = politica, SalesDocumentTypeCode = "FE", DueDay = 10 });
        _db.SaveChanges();
    }

    private void Suscribir(Vehicle v, DateOnly desde) =>
        _db.VehicleSubscriptions.Add(new VehicleSubscription { VehicleId = v.Id, ServiceId = _servicio.Id, From = desde });

    private Task<IngenIA365ERP.Application.Common.Models.Result<BillingPeriodCheckDto>> RevisarAsync(string periodo, Guid? vehiculo = null) =>
        new CheckServiceBillingPeriodQueryHandler(_db, _reloj).Handle(new CheckServiceBillingPeriodQuery(periodo, vehiculo), default);

    [Fact]
    public async Task El_mes_en_curso_con_suscripciones_que_empezaron_despues_del_dia_1_avisa_que_cobraria_dos_veces()
    {
        Sembrar();
        Suscribir(_v1, new DateOnly(2026, 11, 25));
        Suscribir(_v2, new DateOnly(2026, 11, 25));
        _db.SaveChanges();

        var r = (await RevisarAsync("202611")).Value;

        r.CurrentMonth.Should().BeTrue();
        r.BeforeCurrentMonth.Should().BeFalse();
        r.StartedAfterFirstDay.Should().Be(2);
        r.LateStarts.Should().Equal(new BillingLateStartDto(new DateOnly(2026, 11, 25), 2));
        r.Notices.Should().ContainSingle().Which.Should().Contain("2 suscripción(es)").And.Contain("mes completo").And.Contain("dos veces");
    }

    [Fact]
    public async Task Un_periodo_anterior_al_mes_en_curso_avisa()
    {
        Sembrar();

        var r = (await RevisarAsync("202610")).Value;

        r.BeforeCurrentMonth.Should().BeTrue();
        r.Notices.Should().ContainSingle().Which.Should().Contain("octubre 2026").And.Contain("anterior al mes en curso");
    }

    [Fact]
    public async Task Sin_nada_que_advertir_no_hay_avisos()
    {
        Sembrar();
        Suscribir(_v1, new DateOnly(2026, 11, 1));
        Suscribir(_v2, new DateOnly(2026, 12, 1));
        _db.SaveChanges();

        (await RevisarAsync("202611")).Value.Notices.Should().BeEmpty();
        (await RevisarAsync("202612")).Value.Notices.Should().BeEmpty("empezar el día 1 no es empezar tarde");
    }

    [Fact]
    public async Task No_cuenta_lo_ya_facturado_lo_quitado_ni_los_vehiculos_inactivos_y_con_vehiculo_solo_mira_ese()
    {
        Sembrar(ProrationPolicy.Prorated);
        Suscribir(_v1, new DateOnly(2026, 11, 25));
        Suscribir(_v2, new DateOnly(2026, 11, 20));
        _db.VehicleSubscriptions.Add(new VehicleSubscription { VehicleId = _v2.Id, ServiceId = _servicio.Id, From = new DateOnly(2026, 11, 10), IsDeleted = true });
        _db.ServiceCharges.Add(new ServiceCharge
        {
            VehicleId = _v1.Id, ServiceId = _servicio.Id, Period = "202611", InvoicePublicId = Guid.NewGuid(), InvoiceNumber = "FE-1",
            InvoiceLinePublicId = Guid.NewGuid(), Amount = 1m, Route = ChargeRoute.Lending,
        });
        _db.SaveChanges();

        var todos = (await RevisarAsync("202611")).Value;
        todos.StartedAfterFirstDay.Should().Be(1, "el 0001 ya está facturado y la quitada no cuenta");
        todos.Notices.Single().Should().Contain("proporcionales");

        (await RevisarAsync("202611", _v1.PublicId)).Value.Notices.Should().BeEmpty();

        _v2.CambiarEstado(VehicleStatus.Inactive, Hoy);
        _db.SaveChanges();
        (await RevisarAsync("202611")).Value.StartedAfterFirstDay.Should().Be(0);
    }

    [Fact]
    public async Task Un_periodo_que_no_es_aaaamm_se_rechaza()
    {
        Sembrar();

        (await RevisarAsync("2026-11")).Error.Code.Should().Be("Billing.PeriodInvalid");
    }
}
