using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Transport.Vehicles;

/// <summary>
/// 2026-11-25 (pedido del dueño): el listado de vehículos filtra en el servidor por servicio («suscrito a», «no suscrito a», «sin ningún
/// servicio», «con alguno»), por vinculación deducida y por texto (placa, número interno, radio-móvil, nombre o documento del propietario), y
/// cada fila trae sus servicios con la marca de precio pactado. La vinculación como expresión dice lo mismo que la regla de siempre.
/// </summary>
public class FiltroDeVehiculosTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 25);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private TransportService _admin = null!, _radio = null!;

    public FiltroDeVehiculosTests() => _reloj.HoyLocal.Returns(Hoy);

    private Person Persona(string documento, string nombre, string apellido, bool asociado = false, string? estado = "A", DateOnly? retiro = null)
    {
        var p = new Person { TaxId = documento, IdType = "CC", FirstName = nombre, LastName = apellido, IsAssociate = asociado };
        _db.People.Add(p);
        _db.SaveChanges();
        if (asociado)
        {
            _db.Associates.Add(new Associate { PersonId = p.Id, Person = p, Status = estado, JoinDate = new DateOnly(2020, 1, 1), WithdrawalDate = retiro });
            _db.SaveChanges();
        }
        return p;
    }

    private Vehicle Vehiculo(Person dueño, string placa, string interno, string? radio = null, VehicleStatus estado = VehicleStatus.Active)
    {
        var v = Vehicle.Registrar(dueño, new DateOnly(2026, 1, 1));
        v.Plate = placa;
        v.InternalNumber = interno;
        v.RadioCode = radio;
        if (estado != VehicleStatus.Active) v.CambiarEstado(estado, new DateOnly(2026, 6, 1));
        _db.Vehicles.Add(v);
        _db.SaveChanges();
        return v;
    }

    private void Suscribir(Vehicle v, TransportService s, DateOnly desde, DateOnly? hasta = null, decimal? pactado = null, bool quitada = false)
    {
        var sub = new VehicleSubscription { VehicleId = v.Id, ServiceId = s.Id, From = desde, To = hasta, IsDeleted = quitada };
        if (pactado is { } p) sub.Prices.Add(new SubscriptionPrice { Subscription = sub, Price = p, From = desde, To = hasta });
        _db.VehicleSubscriptions.Add(sub);
        _db.SaveChanges();
    }

    /// <summary>
    /// Cinco vehículos: 0001 asociado con administración pactada y radio; 0002 externo con radio; 0003 externo sin servicios (su administración
    /// terminó ayer y otra se quitó); 0004 asociado retirado con administración desde diciembre; 0005 inactivo sin servicios.
    /// </summary>
    private void Sembrar()
    {
        _admin = new TransportService { ProductPublicId = Guid.NewGuid(), ProductCode = "SERV01009", Name = "Administración", Priority = 1 };
        _radio = new TransportService { ProductPublicId = Guid.NewGuid(), ProductCode = "SERV01010", Name = "Radio", Priority = 2 };
        _db.TransportServices.AddRange(_admin, _radio);
        _db.SaveChanges();

        var ana = Persona("1010101010", "Ana", "Gómez", asociado: true);
        var beto = Persona("2020202020", "Beto", "Ruiz");
        var carla = Persona("3030303030", "Carla", "Ríos");
        var dario = Persona("4040404040", "Darío", "Mora", asociado: true, estado: "R", retiro: new DateOnly(2026, 6, 30));

        var v1 = Vehiculo(ana, "AAA111", "0001", "618");
        var v2 = Vehiculo(beto, "BBB222", "0002", "620");
        var v3 = Vehiculo(carla, "CCC333", "0003");
        var v4 = Vehiculo(dario, "DDD444", "0004");
        Vehiculo(carla, "EEE555", "0005", estado: VehicleStatus.Inactive);

        Suscribir(v1, _admin, new DateOnly(2026, 11, 25), pactado: 73_425m);
        Suscribir(v1, _radio, new DateOnly(2026, 1, 1));
        Suscribir(v2, _radio, new DateOnly(2026, 1, 1));
        Suscribir(v3, _admin, new DateOnly(2026, 1, 1), Hoy.AddDays(-1));
        Suscribir(v3, _radio, new DateOnly(2026, 1, 1), quitada: true);
        Suscribir(v4, _admin, new DateOnly(2026, 12, 1));
    }

    private async Task<List<string>> InternosAsync(ListVehiclesQuery q)
    {
        var r = await new ListVehiclesQueryHandler(_db, _reloj).Handle(q, default);
        r.IsSuccess.Should().BeTrue();
        return r.Value.Items.Select(i => i.InternalNumber).ToList();
    }

    [Fact]
    public async Task Suscrito_a_un_servicio_cuenta_la_vigente_y_la_que_empieza_despues_pero_no_la_terminada_ni_la_quitada()
    {
        Sembrar();

        (await InternosAsync(new ListVehiclesQuery(ServicePublicId: _admin.PublicId, ServiceFilter: VehicleServiceFilter.Subscribed)))
            .Should().Equal("0001", "0004");
        (await InternosAsync(new ListVehiclesQuery(ServicePublicId: _radio.PublicId)))
            .Should().Equal(["0001", "0002"], "con el servicio y sin decir cómo, es «suscrito a»");
    }

    [Fact]
    public async Task No_suscrito_y_sin_ningun_servicio()
    {
        Sembrar();

        (await InternosAsync(new ListVehiclesQuery(ServicePublicId: _admin.PublicId, ServiceFilter: VehicleServiceFilter.NotSubscribed)))
            .Should().Equal("0002", "0003", "0005");
        (await InternosAsync(new ListVehiclesQuery(ServiceFilter: VehicleServiceFilter.None))).Should().Equal("0003", "0005");
        (await InternosAsync(new ListVehiclesQuery(ServiceFilter: VehicleServiceFilter.Any))).Should().Equal("0001", "0002", "0004");
    }

    [Fact]
    public async Task La_vinculacion_se_deduce_a_hoy_y_se_combina_con_el_estado()
    {
        Sembrar();

        (await InternosAsync(new ListVehiclesQuery(Affiliation: AffiliationClass.Associate))).Should().Equal(["0001"], "Darío está retirado");
        (await InternosAsync(new ListVehiclesQuery(Affiliation: AffiliationClass.External, Status: VehicleStatus.Active)))
            .Should().Equal("0002", "0003", "0004");
    }

    [Theory]
    [InlineData("aaa-111", "0001")]
    [InlineData("0002", "0002")]
    [InlineData("620", "0002")]
    [InlineData("gomez ana", "0001")]
    [InlineData("RIOS", "0003,0005")]
    [InlineData("1010101010", "0001")]
    [InlineData("30303", "0003,0005")]
    public async Task La_busqueda_encuentra_por_placa_numero_lateral_nombre_y_documento(string q, string esperados)
    {
        Sembrar();

        (await InternosAsync(new ListVehiclesQuery(q))).Should().Equal(esperados.Split(','));
    }

    [Fact]
    public async Task Un_lateral_corto_no_trae_los_documentos_que_lo_contienen()
    {
        Sembrar();

        (await InternosAsync(new ListVehiclesQuery("618"))).Should().Equal(["0001"], "«618» es el lateral, no el comienzo de un documento");
    }

    [Fact]
    public async Task Cada_fila_trae_sus_servicios_por_prioridad_con_la_marca_de_pactado_y_si_empiezan_despues()
    {
        Sembrar();

        var r = await new ListVehiclesQueryHandler(_db, _reloj).Handle(new ListVehiclesQuery(PageSize: 10), default);
        var filas = r.Value.Items.ToDictionary(i => i.InternalNumber);

        filas["0001"].Services.Select(s => (s.Name, s.AgreedPrice, s.Upcoming)).Should().Equal(("Administración", true, false), ("Radio", false, false));
        filas["0003"].Services.Should().BeEmpty("la terminada ayer y la quitada no se muestran");
        filas["0004"].Services.Single().Upcoming.Should().BeTrue();
        r.Value.TotalCount.Should().Be(5);
    }

    [Fact]
    public async Task Pagina_en_el_servidor_con_el_total()
    {
        Sembrar();

        var r = await new ListVehiclesQueryHandler(_db, _reloj).Handle(new ListVehiclesQuery(Page: 2, PageSize: 2), default);

        r.Value.TotalCount.Should().Be(5);
        r.Value.Items.Select(i => i.InternalNumber).Should().Equal("0003", "0004");
    }

    [Theory]
    [InlineData("A", null, null, "2026-11-25", false)]
    [InlineData("R", null, null, "2026-11-25", true)]
    [InlineData("r", null, null, "2026-11-25", true)]
    [InlineData("A", "2026-06-30", null, "2026-11-25", true)]
    [InlineData("A", "2026-12-31", null, "2026-11-25", false)]
    [InlineData("R", "2026-06-30", "2026-08-01", "2026-11-25", false)]
    [InlineData("R", "2026-06-30", "2026-12-01", "2026-11-25", true)]
    [InlineData("R", "2026-09-30", "2026-08-01", "2026-11-25", true)]
    [InlineData(null, null, null, "2026-11-25", false)]
    public void La_vinculacion_como_expresion_dice_lo_mismo_que_la_regla(string? estado, string? retiro, string? reingreso, string fecha, bool retirado)
    {
        var f = DateOnly.Parse(fecha);
        var r = retiro is null ? (DateOnly?)null : DateOnly.Parse(retiro);
        var i = reingreso is null ? (DateOnly?)null : DateOnly.Parse(reingreso);
        AfiliacionVigente.Retirado(estado, r, i, f).Should().Be(retirado);

        var asociado = new Associate
        {
            Status = estado, WithdrawalDate = r, RejoinDate = i, Person = new Person { IsAssociate = true },
        };
        AfiliacionVigente.VigenteEn(f).Compile()(asociado).Should().Be(!retirado);
    }
}
