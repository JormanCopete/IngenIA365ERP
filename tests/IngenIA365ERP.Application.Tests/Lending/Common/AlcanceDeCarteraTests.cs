using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Security;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Common;

/// <summary>
/// Feature 013, T0244 (T62, R131, FR-004, SC-015): el alcance de Cartera por oficina y línea, que <b>falla cerrado</b>. Las oficinas salen
/// de <c>SEC_UserBranchAssignments</c> → <c>ADM_Branches</c> → <c>COR_Branches.TenantBranchPublicId</c> por <see cref="OficinasDelUsuario"/>
/// —el mismo salto que usa <c>UserBranchScope</c>, que conserva su semántica <b>abierta</b> (regresión de la 009)—; las líneas, de
/// <c>LND_UserCreditLineScopes</c>. Los permisos <c>Lending.Scope.AllOffices</c> / <c>.AllLines</c> dan el total; en segundo plano el
/// alcance es total; se lee una vez por petición; <see cref="FiltroDeAlcanceDeCartera"/> deja fuera lo de otra oficina o línea y no lo
/// cuenta.
/// </summary>
public class AlcanceDeCarteraTests
{
    private static readonly Guid CooperativaPublica = Guid.Parse(CooperativaDePrueba.PublicIdN);
    private const int UsuarioId = 7;

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly TestAdminDbContext _admin = TestAdminDbContext.Create();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private int _cooperativaId;
    private int _norte, _sur, _centro;

    public AlcanceDeCarteraTests()
    {
        var tenant = new Tenant { Name = "Coop", SchemaName = "coop", PublicId = CooperativaPublica, IsActive = true };
        _admin.Tenants.Add(tenant);
        _admin.SaveChanges();
        _cooperativaId = tenant.Id;

        // Tres oficinas de la cooperativa; Norte y Sur tienen sucursal contable; Centro no.
        var oNorte = new TenantBranch { TenantId = tenant.Id, Code = "N", Name = "Norte", PublicId = Guid.NewGuid() };
        var oSur = new TenantBranch { TenantId = tenant.Id, Code = "S", Name = "Sur", PublicId = Guid.NewGuid() };
        var oCentro = new TenantBranch { TenantId = tenant.Id, Code = "C", Name = "Centro", PublicId = Guid.NewGuid() };
        _admin.TenantBranches.AddRange(oNorte, oSur, oCentro);
        _admin.SaveChanges();
        _norte = oNorte.Id;
        _sur = oSur.Id;
        _centro = oCentro.Id;

        _db.Branches.AddRange(
            new Branch { Name = "Sucursal Norte", TenantBranchPublicId = oNorte.PublicId },
            new Branch { Name = "Sucursal Sur", TenantBranchPublicId = oSur.PublicId });
        _db.SaveChanges();

        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Persona(UsuarioId)));
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>([]));
    }

    private static Actor Persona(int? usuario) =>
        new(ActorKind.Person, usuario, Guid.NewGuid(), Guid.NewGuid(), "Ana", "ana@coop", ExecutionChannel.Web, "GET /api/lending", "10.0.0.1", null);

    private int SucursalDe(string nombre) => _db.Branches.Single(b => b.Name == nombre).Id;

    private void Asignar(int oficinaAdm, bool porDefecto = false) =>
        _db.UserBranchAssignments.Add(new UserBranchAssignment
        {
            UserId = UsuarioId, TenantId = _cooperativaId, BranchId = oficinaAdm, IsDefault = porDefecto, IsActive = true,
        });

    private void Lineas(params (int Linea, bool PorDefecto)[] lineas)
    {
        foreach (var (linea, porDefecto) in lineas)
            _db.UserCreditLineScopes.Add(new UserCreditLineScope { UserId = UsuarioId, CreditLineId = linea, IsDefault = porDefecto });
        _db.SaveChanges();
    }

    private void Conceder(params string[] codigos) =>
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>(codigos));

    private AlcanceDeCarteraDelActor Alcance() => new(_actor, _permisos, CooperativaDePrueba.Actual, _admin, _db, new OficinasDelUsuario(_db, _admin));

    // -------------------------------------------------------------------------------------------------------- oficinas --

    [Fact]
    public async Task Sin_filas_en_SEC_UserBranchAssignments_no_hay_oficinas()
    {
        var alcance = await Alcance().ObtenerAsync();

        alcance.TodasLasOficinas.Should().BeFalse();
        alcance.Oficinas.Should().BeEmpty("en Cartera el alcance falla cerrado");
        alcance.OficinaPorDefecto.Should().BeNull();
    }

    [Fact]
    public async Task La_oficina_sale_por_la_asignacion_la_oficina_y_la_sucursal_contable_que_la_referencia()
    {
        Asignar(_norte);
        Asignar(_sur, porDefecto: true);
        Asignar(_centro);
        _db.SaveChanges();

        var alcance = await Alcance().ObtenerAsync();

        alcance.Oficinas.Should().BeEquivalentTo([SucursalDe("Sucursal Norte"), SucursalDe("Sucursal Sur")],
            "Centro no tiene sucursal contable que la referencie");
        alcance.OficinaPorDefecto.Should().Be(SucursalDe("Sucursal Sur"));
        alcance.IncluyeOficina(SucursalDe("Sucursal Norte")).Should().BeTrue();
    }

    [Fact]
    public async Task Una_asignacion_inactiva_o_de_otra_cooperativa_no_cuenta()
    {
        _db.UserBranchAssignments.Add(new UserBranchAssignment { UserId = UsuarioId, TenantId = _cooperativaId, BranchId = _norte, IsActive = false });
        _db.UserBranchAssignments.Add(new UserBranchAssignment { UserId = UsuarioId, TenantId = _cooperativaId + 50, BranchId = _sur, IsActive = true });
        _db.SaveChanges();

        (await Alcance().ObtenerAsync()).Oficinas.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_Lending_Scope_AllOffices_todas_las_oficinas()
    {
        Conceder(AlcanceDeCarteraDelActor.TodasLasOficinas);

        var alcance = await Alcance().ObtenerAsync();

        alcance.TodasLasOficinas.Should().BeTrue();
        alcance.IncluyeOficina(SucursalDe("Sucursal Sur")).Should().BeTrue();
        alcance.TodasLasLineas.Should().BeFalse("un permiso de alcance no concede el otro");
    }

    [Fact]
    public async Task OficinasDelUsuario_distingue_sin_filas_de_filas_sin_sucursal_contable()
    {
        var oficinas = new OficinasDelUsuario(_db, _admin);

        var sinFilas = await oficinas.LeerAsync(UsuarioId, _cooperativaId);
        sinFilas.TieneAsignaciones.Should().BeFalse();

        Asignar(_centro, porDefecto: true);
        _db.SaveChanges();
        var sinSucursal = await oficinas.LeerAsync(UsuarioId, _cooperativaId);
        sinSucursal.TieneAsignaciones.Should().BeTrue();
        sinSucursal.Sucursales.Should().BeEmpty();
    }

    [Fact]
    public async Task UserBranchScope_conserva_su_semantica_abierta_sobre_el_mismo_salto()
    {
        // Regresión de la 009 (FR-035, R7): sin asignaciones, Contabilidad no restringe; con asignaciones que ninguna sucursal
        // contable referencia, el alcance queda vacío a propósito; con asignaciones vinculadas, sólo esas, con la por defecto.
        var oficinas = new OficinasDelUsuario(_db, _admin);

        AlcanceDeSucursales.De(await oficinas.LeerAsync(UsuarioId, _cooperativaId)).Should().Be(AlcanceDeSucursales.SinRestriccion);

        Asignar(_centro);
        _db.SaveChanges();
        var vacio = AlcanceDeSucursales.De(await oficinas.LeerAsync(UsuarioId, _cooperativaId));
        vacio.Restringido.Should().BeTrue();
        vacio.Sucursales.Should().BeEmpty();

        Asignar(_norte, porDefecto: true);
        _db.SaveChanges();
        var limitado = AlcanceDeSucursales.De(await oficinas.LeerAsync(UsuarioId, _cooperativaId));
        limitado.Sucursales.Should().BeEquivalentTo([SucursalDe("Sucursal Norte")]);
        limitado.SucursalPorDefecto.Should().Be(SucursalDe("Sucursal Norte"));
    }

    // ---------------------------------------------------------------------------------------------------------- líneas --

    [Fact]
    public async Task Sin_filas_en_LND_UserCreditLineScopes_no_hay_lineas_salvo_AllLines()
    {
        (await Alcance().ObtenerAsync()).Lineas.Should().BeEmpty();

        Conceder(AlcanceDeCarteraDelActor.TodasLasLineas);
        var total = await Alcance().ObtenerAsync();
        total.TodasLasLineas.Should().BeTrue();
        total.IncluyeLinea(99).Should().BeTrue();
    }

    [Fact]
    public async Task Las_lineas_son_las_asignadas_vivas()
    {
        Lineas((10, false), (11, true));
        _db.UserCreditLineScopes.Add(new UserCreditLineScope { UserId = UsuarioId, CreditLineId = 12, IsDeleted = true });
        _db.UserCreditLineScopes.Add(new UserCreditLineScope { UserId = UsuarioId + 1, CreditLineId = 13 });
        _db.SaveChanges();

        var alcance = await Alcance().ObtenerAsync();

        alcance.Lineas.Should().BeEquivalentTo([10, 11]);
        alcance.IncluyeLinea(12).Should().BeFalse("lo retirado quedó de baja lógica");
    }

    // ------------------------------------------------------------------------------------- quién, cuándo y cuántas veces --

    [Fact]
    public async Task En_segundo_plano_el_alcance_es_total()
    {
        using var _ = ContextoAmbiental.Fijar(new TenantDirectoryEntry(5, CooperativaPublica, "coop", "Coop", DatabaseName: "coop"),
            Actor.ProcesoDeIntegracion("Tarea:cartera.mensajes"), "Tarea:cartera.mensajes");

        var alcance = await Alcance().ObtenerAsync();

        alcance.Should().Be(AlcanceDeCartera.Total);
        await _actor.DidNotReceive().ObtenerAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_usuario_resuelto_el_alcance_es_vacio()
    {
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Persona(null)));
        Conceder(AlcanceDeCarteraDelActor.TodasLasOficinas, AlcanceDeCarteraDelActor.TodasLasLineas);

        (await Alcance().ObtenerAsync()).Should().Be(AlcanceDeCartera.Vacio);
    }

    [Fact]
    public async Task El_maestro_global_tiene_alcance_total()
    {
        _permisos.EsMaestroGlobal.Returns(true);

        (await Alcance().ObtenerAsync()).Should().Be(AlcanceDeCartera.Total);
    }

    [Fact]
    public async Task Se_lee_una_sola_vez_por_peticion()
    {
        var alcance = Alcance();

        var primero = await alcance.ObtenerAsync();
        var segundo = await alcance.ObtenerAsync();

        segundo.Should().BeSameAs(primero);
        await _permisos.Received(1).ListAsync(Arg.Any<CancellationToken>());
        await _actor.Received(1).ObtenerAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_implementacion_cerrada_siempre_es_vacia()
    {
        (await new AlcanceDeCarteraCerrado().ObtenerAsync()).Should().Be(AlcanceDeCartera.Vacio);
    }

    // ---------------------------------------------------------------------------------------------------------- filtro --

    private sealed record Credito(string Numero, int BranchId, int CreditLineId);

    private static readonly Credito[] Creditos =
    [
        new("000001", 1, 10),
        new("000002", 1, 11),
        new("000003", 2, 10),
        new("000004", 3, 12),
    ];

    private static AlcanceDeCartera Solo(int[] oficinas, int[] lineas) =>
        AlcanceDeCartera.Vacio with { Oficinas = oficinas.ToHashSet(), Lineas = lineas.ToHashSet() };

    [Fact]
    public void El_filtro_deja_fuera_lo_de_otra_oficina_o_linea_y_no_lo_cuenta()
    {
        var visibles = Creditos.AsQueryable().PorOficinaYLinea(Solo([1], [10]), c => c.BranchId, c => c.CreditLineId);

        visibles.Select(c => c.Numero).Should().Equal("000001");
        visibles.Count().Should().Be(1, "lo que está fuera del alcance no aparece ni cuenta en totalCount");
        Creditos.AsQueryable().PorOficinaYLinea(AlcanceDeCartera.Vacio, c => c.BranchId, c => c.CreditLineId).Should().BeEmpty();
        Creditos.AsQueryable().PorOficinaYLinea(AlcanceDeCartera.Total, c => c.BranchId, c => c.CreditLineId).Should().HaveCount(4);
        Creditos.AsQueryable().PorOficina(Solo([1, 2], []), c => c.BranchId).Should().HaveCount(3);
        Creditos.AsQueryable().PorLinea(AlcanceDeCartera.Vacio with { TodasLasLineas = true }, c => c.CreditLineId).Should().HaveCount(4);
    }

    [Fact]
    public async Task Un_comando_fuera_de_alcance_responde_el_404_del_recurso_y_deja_Lending_Access_OutOfScope()
    {
        var alcance = Substitute.For<IAlcanceDeCartera>();
        alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(Solo([1], [10])));
        var servicios = new ServiceCollection()
            .AddSingleton<IApplicationDbContext>(_db)
            .AddSingleton(CooperativaDePrueba.Actual)
            .AddSingleton(Substitute.For<IAuditService>())
            .AddSingleton(Payroll.Common.NominaTestData.UsuarioDePrueba("ana@coop", UsuarioId))
            .BuildServiceProvider();
        var emisor = new LendingAuditEmitter(servicios, NullLogger<LendingAuditEmitter>.Instance);
        var noExiste = new Error("Lending.Loan.NotFound", "El crédito no existe.");
        var credito = Guid.NewGuid();

        var dentro = await alcance.AsegurarAsync(1, 10, noExiste, emisor, "Loan", credito);
        var otraLinea = await alcance.AsegurarAsync(1, 11, noExiste, emisor, "Loan", credito);
        var otraOficina = await alcance.AsegurarAsync(2, 10, noExiste, emisor, "Loan", credito);

        dentro.IsSuccess.Should().BeTrue();
        otraLinea.Error.Should().Be(noExiste, "fuera de alcance es indistinguible de inexistente");
        otraOficina.Error.Code.Should().Be("Lending.Loan.NotFound");
        var eventos = (await _db.AuditOutbox.ToListAsync()).Select(f => AuditoriaEncadenada.LeerCarga(f.PayloadJson!)).ToList();
        eventos.Should().HaveCount(2).And.OnlyContain(e => e.Action == AuditEventTypes.LendingAccessOutOfScope && e.Module == "Lending");
        eventos[0].EntityType.Should().Be("Loan");
        eventos[0].EntityPublicId.Should().Be(credito.ToString());
    }
}
