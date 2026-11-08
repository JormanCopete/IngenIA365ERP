using FluentAssertions;
using IngenIA365ERP.Application.Admin.Tenants.SedePrincipal;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Admin;

/// <summary>
/// La oficina principal y su agencia nacen con la cooperativa (feature 018). El arranque corre esto sobre todas las
/// cooperativas, así que importa tanto lo que crea como lo que no toca.
/// </summary>
public class SedePrincipalDeCooperativaTests
{
    private readonly TestAdminDbContext _admin = TestAdminDbContext.Create();
    private readonly IApplicationDbContext _cooperativa = TestDbContextFactory.Create();
    private readonly IAuditAppendOnlyWriter _auditoria = Substitute.For<IAuditAppendOnlyWriter>();
    private readonly int _tenantId;

    public SedePrincipalDeCooperativaTests()
    {
        var tenant = new Tenant { Name = "Alfa", PublicId = Guid.NewGuid(), SchemaName = "coop_alfa", DatabaseName = "coop_alfa", IsActive = true };
        _admin.Tenants.Add(tenant);
        _admin.SaveChanges();
        _tenantId = tenant.Id;
    }

    private SedePrincipalDeCooperativa Sede()
    {
        var escena = Substitute.For<ITenantDbScope>();
        escena.Db.Returns(_cooperativa);
        var fabrica = Substitute.For<ITenantDbContextFactory>();
        fabrica.Abrir("coop_alfa", Arg.Any<string?>()).Returns(escena);
        return new SedePrincipalDeCooperativa(
            _admin, fabrica, Substitute.For<ICurrentUserService>(), _auditoria,
            Substitute.For<IDateTimeService>(), NullLogger<SedePrincipalDeCooperativa>.Instance);
    }

    private async Task OficinaAsync(string codigo, bool principal)
    {
        _admin.TenantBranches.Add(new TenantBranch { TenantId = _tenantId, Code = codigo, Name = $"Oficina {codigo}", IsActive = true, IsHeadquarters = principal });
        await _admin.SaveChangesAsync(default);
    }

    private async Task AgenciaAsync(bool dadaDeBaja = false)
    {
        _cooperativa.Branches.Add(new Branch { LegacyCode = "VIEJA", Name = "Agencia vieja", IsDeleted = dadaDeBaja });
        await _cooperativa.SaveChangesAsync(default);
    }

    private Task<int> OficinasAsync() => _admin.TenantBranches.CountAsync();
    private Task<int> AgenciasAsync() => _cooperativa.Branches.IgnoreQueryFilters().CountAsync();

    [Fact]
    public async Task Sin_oficinas_ni_agencias_crea_las_dos_vinculadas()
    {
        var r = await Sede().AsegurarAsync(_tenantId, default);

        r.Should().Be(ResultadoDeSede.OficinaYAgenciaCreadas);
        var oficina = await _admin.TenantBranches.SingleAsync();
        oficina.Code.Should().Be("PRINCIPAL");
        oficina.Name.Should().Be("Oficina principal");
        oficina.IsHeadquarters.Should().BeTrue();
        var agencia = await _cooperativa.Branches.SingleAsync();
        agencia.LegacyCode.Should().Be("PRINCIPAL");
        agencia.TenantBranchPublicId.Should().Be(oficina.PublicId);
        agencia.MunicipalityDaneCode.Should().BeNull();
    }

    [Fact]
    public async Task Repetir_no_duplica_nada()
    {
        await Sede().AsegurarAsync(_tenantId, default);

        var r = await Sede().AsegurarAsync(_tenantId, default);

        r.Should().Be(ResultadoDeSede.SinCambios);
        (await OficinasAsync()).Should().Be(1);
        (await AgenciasAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Con_oficina_principal_sin_agencia_completa_la_agencia()
    {
        // Es también lo que deja un fallo a mitad de camino: la oficina se guardó y la agencia no.
        await OficinaAsync("MATRIZ", principal: true);

        var r = await Sede().AsegurarAsync(_tenantId, default);

        r.Should().Be(ResultadoDeSede.AgenciaCreada);
        (await OficinasAsync()).Should().Be(1);
        var oficina = await _admin.TenantBranches.SingleAsync();
        (await _cooperativa.Branches.SingleAsync()).TenantBranchPublicId.Should().Be(oficina.PublicId);
    }

    [Fact]
    public async Task Con_oficinas_sin_principal_ni_agencias_no_adivina()
    {
        await OficinaAsync("NORTE", principal: false);

        var r = await Sede().AsegurarAsync(_tenantId, default);

        r.Should().Be(ResultadoDeSede.ARevisar);
        (await AgenciasAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Con_agencias_sin_oficinas_no_toca_nada()
    {
        await AgenciaAsync();

        var r = await Sede().AsegurarAsync(_tenantId, default);

        r.Should().Be(ResultadoDeSede.ARevisar);
        (await OficinasAsync()).Should().Be(0);
        (await AgenciasAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Con_oficina_y_agencia_sin_vincular_no_las_vincula()
    {
        await OficinaAsync("MATRIZ", principal: true);
        await AgenciaAsync();

        var r = await Sede().AsegurarAsync(_tenantId, default);

        r.Should().Be(ResultadoDeSede.SinCambios);
        (await _cooperativa.Branches.SingleAsync()).TenantBranchPublicId.Should().BeNull();
    }

    [Fact]
    public async Task Una_agencia_dada_de_baja_no_reaparece_sola()
    {
        await OficinaAsync("MATRIZ", principal: true);
        await AgenciaAsync(dadaDeBaja: true);

        var r = await Sede().AsegurarAsync(_tenantId, default);

        r.Should().Be(ResultadoDeSede.SinCambios);
        (await AgenciasAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Crear_la_sede_queda_en_la_auditoria()
    {
        await Sede().AsegurarAsync(_tenantId, default);

        await _auditoria.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(e => e.Action == "Tenant.HeadquartersCreated"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_cambios_no_deja_evento()
    {
        await OficinaAsync("MATRIZ", principal: true);
        await AgenciaAsync();

        await Sede().AsegurarAsync(_tenantId, default);

        await _auditoria.DidNotReceive().AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_fallo_de_la_auditoria_no_deshace_la_sede()
    {
        _auditoria.AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Mongo caído")));

        var r = await Sede().AsegurarAsync(_tenantId, default);

        r.Should().Be(ResultadoDeSede.OficinaYAgenciaCreadas);
        (await OficinasAsync()).Should().Be(1);
    }
}
