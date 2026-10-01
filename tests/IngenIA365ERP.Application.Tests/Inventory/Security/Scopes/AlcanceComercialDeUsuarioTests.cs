using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Security.Scopes;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Security;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Security.Scopes;

/// <summary>
/// Feature 012, T090 (T35; contracts/api.md §16.3): leer y reemplazar el alcance comercial de un usuario por los puertos
/// <see cref="IAsignacionesDeBodega"/> / <see cref="IAsignacionesDePuntoDeVenta"/>. Los puertos son falsos en memoria
/// (sus implementaciones reales son de US1 y US5); quien administra tiene un alcance falso.
/// </summary>
public class AlcanceComercialDeUsuarioTests
{
    private static readonly ElementoDeAlcance Norte = new(1, Guid.NewGuid(), "B01", "Bodega Norte");
    private static readonly ElementoDeAlcance Sur = new(2, Guid.NewGuid(), "B02", "Bodega Sur");
    private static readonly ElementoDeAlcance Oriente = new(3, Guid.NewGuid(), "B03", "Bodega Oriente");
    private static readonly ElementoDeAlcance Caja1 = new(11, Guid.NewGuid(), "C01", "Caja 1");

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly AsignacionesEnMemoria _bodegas = new(Norte, Sur, Oriente);
    private readonly AsignacionesEnMemoria _puntos = new(Caja1);
    private readonly IAlcanceDeInventario _alcanceDelAdministrador = Substitute.For<IAlcanceDeInventario>();
    private readonly IAutoridadDeOtroAprobador _permisos = Substitute.For<IAutoridadDeOtroAprobador>();
    private readonly User _cajero;

    public AlcanceComercialDeUsuarioTests()
    {
        _cajero = new User { Username = "cajero@coop.test", Email = "cajero@coop.test", PasswordHash = "x" };
        _db.Users.Add(_cajero);
        _db.SaveChanges();
        _alcanceDelAdministrador.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
    }

    private Task<Result<UserCommercialScopeDto>> Leer(Guid usuario) =>
        new GetUserCommercialScopeQueryHandler(new VistaDeAlcanceComercial(_db, _bodegas, _puntos, _alcanceDelAdministrador, _permisos))
            .Handle(new GetUserCommercialScopeQuery(usuario), CancellationToken.None);

    private Task<Result<UserCommercialScopeDto>> Fijar(IReadOnlyList<WarehouseScopeInput> bodegas, IReadOnlyList<PointOfSaleScopeInput>? puntos = null) =>
        new SetUserCommercialScopeCommandHandler(_db, _bodegas, _puntos, _alcanceDelAdministrador,
                new VistaDeAlcanceComercial(_db, _bodegas, _puntos, _alcanceDelAdministrador, _permisos))
            .Handle(new SetUserCommercialScopeCommand(_cajero.PublicId, bodegas, puntos) { OperationKey = Guid.NewGuid() }, CancellationToken.None);

    [Fact]
    public async Task Un_usuario_sin_asignaciones_ni_alcance_total_tiene_alcance_vacio()
    {
        var r = await Leer(_cajero.PublicId);

        r.IsSuccess.Should().BeTrue();
        r.Value.User.Email.Should().Be("cajero@coop.test");
        r.Value.HasAllWarehouses.Should().BeFalse();
        r.Value.HasAllPointsOfSale.Should().BeFalse();
        r.Value.Warehouses.Should().BeEmpty();
        r.Value.PointsOfSale.Should().BeEmpty();
    }

    [Fact]
    public async Task El_alcance_total_sale_del_permiso_del_usuario()
    {
        _permisos.TienePermisoAsync(_cajero.Id, "Inventory.Scope.AllWarehouses", Arg.Any<CancellationToken>()).Returns(true);

        (await Leer(_cajero.PublicId)).Value.HasAllWarehouses.Should().BeTrue();
    }

    [Fact]
    public async Task Un_usuario_inexistente_es_404()
    {
        (await Leer(Guid.NewGuid())).Error.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task Reemplaza_las_asignaciones_con_una_por_defecto()
    {
        await Fijar([new(Norte.PublicId, false), new(Sur.PublicId, true)]);

        var r = await Fijar([new(Sur.PublicId, true), new(Oriente.PublicId, false)], [new(Caja1.PublicId, true)]);

        r.IsSuccess.Should().BeTrue();
        r.Value.Warehouses.Select(w => (w.Code, w.IsDefault)).Should().BeEquivalentTo(new[] { ("B02", true), ("B03", false) });
        r.Value.PointsOfSale.Should().ContainSingle().Which.IsDefault.Should().BeTrue();
        _bodegas.De(_cajero.Id).Select(a => a.Id).Should().BeEquivalentTo([2, 3], "Norte quedó retirada");
        _db.ChangeTracker.HasChanges().Should().BeFalse("el comando guarda con su unidad de trabajo");
    }

    [Fact]
    public async Task Dos_por_defecto_de_la_misma_clase_es_DefaultDuplicate()
    {
        var r = await Fijar([new(Norte.PublicId, true), new(Sur.PublicId, true)]);

        r.Error.Code.Should().Be("Inventory.Scope.DefaultDuplicate");
        _bodegas.De(_cajero.Id).Should().BeEmpty();
    }

    [Fact]
    public async Task Una_bodega_inexistente_es_404()
    {
        var r = await Fijar([new(Guid.NewGuid(), false)]);

        r.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
    }

    [Fact]
    public async Task Quien_administra_solo_asigna_bodegas_de_su_propio_alcance()
    {
        _alcanceDelAdministrador.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(AlcanceDeInventario.Vacio with { Bodegas = new HashSet<int> { Norte.Id } });

        var fuera = await Fijar([new(Sur.PublicId, false)]);

        fuera.Error.Code.Should().Be("Inventory.Warehouse.NotFound", "fuera de su alcance es el mismo 404 que inexistente");
    }

    [Fact]
    public async Task Lo_asignado_fuera_del_alcance_de_quien_administra_se_conserva_y_no_se_le_muestra()
    {
        _bodegas.Fijar(_cajero.Id, new AsignacionPedida(Sur.Id, true));
        _alcanceDelAdministrador.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(AlcanceDeInventario.Vacio with { Bodegas = new HashSet<int> { Norte.Id } });

        var r = await Fijar([new(Norte.PublicId, true)]);

        r.IsSuccess.Should().BeTrue();
        r.Value.Warehouses.Select(w => w.Code).Should().Equal("B01");
        _bodegas.De(_cajero.Id).Should().BeEquivalentTo(new[] { new AsignacionPedida(Sur.Id, false), new AsignacionPedida(Norte.Id, true) },
            "la de Sur sigue asignada; la por defecto pedida manda");
    }

    [Fact]
    public async Task Sin_pointsOfSale_no_se_tocan_los_puntos()
    {
        _puntos.Fijar(_cajero.Id, new AsignacionPedida(Caja1.Id, true));

        await Fijar([new(Norte.PublicId, false)]);

        _puntos.De(_cajero.Id).Should().ContainSingle();
    }

    [Fact]
    public void El_validador_rechaza_una_bodega_repetida()
    {
        var v = new SetUserCommercialScopeCommandValidator();

        v.Validate(new SetUserCommercialScopeCommand(_cajero.PublicId, [new(Norte.PublicId, false), new(Norte.PublicId, true)], null))
            .IsValid.Should().BeFalse();
        v.Validate(new SetUserCommercialScopeCommand(_cajero.PublicId, [new(Norte.PublicId, false)], null)).IsValid.Should().BeTrue();
    }

    /// <summary>Un puerto de asignaciones en memoria, con la regla de a lo sumo una por defecto.</summary>
    private sealed class AsignacionesEnMemoria(params ElementoDeAlcance[] elementos) : IAsignacionesDeBodega, IAsignacionesDePuntoDeVenta
    {
        private readonly Dictionary<int, List<AsignacionPedida>> _porUsuario = [];

        public IReadOnlyList<AsignacionPedida> De(int userId) => _porUsuario.GetValueOrDefault(userId) ?? [];

        public void Fijar(int userId, params AsignacionPedida[] asignaciones) => _porUsuario[userId] = [.. asignaciones];

        private Task<AsignacionesDeAlcance> Vigentes(int userId) => Task.FromResult(new AsignacionesDeAlcance(
            De(userId).Select(a => new AsignacionDeAlcance(elementos.Single(e => e.Id == a.Id), a.IsDefault)).ToList()));

        public Task<AsignacionesDeAlcance> BodegasDelUsuarioAsync(int userId, CancellationToken ct) => Vigentes(userId);

        public Task<AsignacionesDeAlcance> PuntosDelUsuarioAsync(int userId, CancellationToken ct) => Vigentes(userId);

        public Task<IReadOnlyDictionary<Guid, ElementoDeAlcance>> BuscarAsync(IReadOnlyCollection<Guid> publicIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<Guid, ElementoDeAlcance>>(elementos.Where(e => publicIds.Contains(e.PublicId)).ToDictionary(e => e.PublicId));

        public Task<Result> ReemplazarAsync(int userId, IReadOnlyList<AsignacionPedida> asignaciones, CancellationToken ct)
        {
            if (asignaciones.Count(a => a.IsDefault) > 1) return Task.FromResult(Result.Failure(ErroresDeAlcance.PorDefectoRepetido("warehouse")));
            _porUsuario[userId] = [.. asignaciones];
            return Task.FromResult(Result.Success());
        }
    }
}
