using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Identity;
using IngenIA365ERP.Application.Identity.Correccion.FusionarIdentidades;
using IngenIA365ERP.Application.Identity.Documento;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Correccion;

/// <summary>
/// Feature 017 (T072, US6): la fusión de dos identidades. Se niega si comparten cooperativa; el paso
/// administrativo lo hace <see cref="IFusionDeIdentidades"/> (sus filas se prueban en la e2e); y el paso por
/// cooperativa es reintentable: si una base falla, el caso queda en curso con la pendiente y repetir el mismo
/// pedido lo completa sin abrir otro caso.
/// </summary>
public class FusionarIdentidadesTests
{
    private static readonly DateTime Ahora = new(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Maestro = Guid.NewGuid();
    private static readonly Guid Origen = Guid.NewGuid();
    private static readonly Guid Destino = Guid.NewGuid();

    private readonly TestAdminDbContext _db = TestAdminDbContext.Create();
    private readonly ICentralIdentityProvider _identidad = Substitute.For<ICentralIdentityProvider>();
    private readonly ICurrentCentralUserContext _sesion = Substitute.For<ICurrentCentralUserContext>();
    private readonly IFusionDeIdentidades _fusion = Substitute.For<IFusionDeIdentidades>();
    private readonly ITenantDbContextFactory _bases = Substitute.For<ITenantDbContextFactory>();
    private readonly Tenant _coop = new() { Name = "Coop A", SchemaName = "coop_a", DatabaseName = "coop_a", IsActive = true };

    public FusionarIdentidadesTests()
    {
        _sesion.IsAuthenticated.Returns(true);
        _sesion.CentralUserId.Returns(Maestro);
        _sesion.Purpose.Returns(CentralJwtPurposes.Full);
        _sesion.IsGlobalMasterAdmin.Returns(true);
        _identidad.FindByIdAsync(Origen, Arg.Any<CancellationToken>()).Returns(new CentralUser { Id = Origen, Email = "ana.vieja@gmail.com" });
        _identidad.FindByIdAsync(Destino, Arg.Any<CancellationToken>()).Returns(new CentralUser { Id = Destino, Email = "ana@gmail.com" });
        _fusion.TrasladarAsync(Origen, Destino, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new ResultadoDeFusion(1, 2, 0, 0, "1234567"));
        _db.Tenants.Add(_coop);
        _db.SaveChanges();
    }

    private FusionarIdentidadesCommandHandler Handler()
    {
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(Ahora);
        return new FusionarIdentidadesCommandHandler(
            _sesion, _identidad, _db, _fusion,
            new PropagadorDeIdentidad(_db, _bases, NullLogger<PropagadorDeIdentidad>.Instance),
            new AuditoriaDeIdentidad(Substitute.For<IAuditAppendOnlyWriter>(), NullLogger<AuditoriaDeIdentidad>.Instance),
            reloj);
    }

    private static FusionarIdentidadesCommand Pedido() => new(Origen, Destino, "Dos correos de la misma persona");

    [Fact]
    public async Task Si_comparten_cooperativa_se_niega_y_no_mueve_nada()
    {
        _db.TenantMemberships.Add(TenantMembership.CreateActive(Origen, _coop.PublicId, false, Maestro, Ahora));
        _db.TenantMemberships.Add(TenantMembership.CreateActive(Destino, _coop.PublicId, false, Maestro, Ahora));
        await _db.SaveChangesAsync();

        var r = await Handler().Handle(Pedido(), default);

        r.Error.Code.Should().Be("Identity.Merge.SharedTenant");
        await _fusion.DidNotReceiveWithAnyArgs().TrasladarAsync(default, default, default, default);
        (await _db.IdentityCases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Sin_cooperativas_que_actualizar_el_caso_se_cierra()
    {
        var r = await Handler().Handle(Pedido(), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Status.Should().Be(nameof(IdentityCaseStatus.Done));
        var caso = await _db.IdentityCases.SingleAsync();
        caso.Kind.Should().Be(IdentityCaseKind.Merge);
        caso.OtherCentralUserId.Should().Be(Destino);
    }

    [Fact]
    public async Task Una_cooperativa_que_falla_deja_el_caso_en_curso_y_el_reintento_lo_completa()
    {
        // El paso administrativo ya movió la membresía al destino: la cooperativa es del destino.
        _db.TenantMemberships.Add(TenantMembership.CreateActive(Destino, _coop.PublicId, false, Maestro, Ahora));
        await _db.SaveChangesAsync();
        _bases.Abrir(Arg.Any<string>(), Arg.Any<string?>()).Throws(new InvalidOperationException("la base no responde"));

        var primera = await Handler().Handle(Pedido(), default);

        primera.Value.Status.Should().Be(nameof(IdentityCaseStatus.InProgress));
        primera.Value.PendingTenants.Should().ContainSingle().Which.Should().Be(_coop.PublicId);

        // El origen ya quedó deshabilitado; el reintento reusa el caso abierto.
        _identidad.FindByIdAsync(Origen, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = Origen, Email = "ana.vieja@gmail.com", Status = CentralUserStatus.Disabled });
        _bases.Abrir(Arg.Any<string>(), Arg.Any<string?>()).Returns(new AmbitoDePrueba(TestDbContextFactory.Create()));

        var segunda = await Handler().Handle(Pedido(), default);

        segunda.IsSuccess.Should().BeTrue(segunda.IsFailure ? segunda.Error.Message : null);
        segunda.Value.CasePublicId.Should().Be(primera.Value.CasePublicId);
        segunda.Value.Status.Should().Be(nameof(IdentityCaseStatus.Done));
        (await _db.IdentityCases.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Solo_el_maestro_fusiona()
    {
        _sesion.IsGlobalMasterAdmin.Returns(false);

        var r = await Handler().Handle(Pedido(), default);

        r.Error.Code.Should().Be("Saas.MasterOnly");
    }

    private sealed class AmbitoDePrueba(IApplicationDbContext db) : ITenantDbScope
    {
        public IApplicationDbContext Db => db;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
