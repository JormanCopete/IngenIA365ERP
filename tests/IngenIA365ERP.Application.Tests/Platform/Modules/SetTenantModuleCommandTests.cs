using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Platform.Modules;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Platform.Modules;

/// <summary>
/// Feature 020 (T032; US1; R1; FR-001 a FR-003): sólo el maestro habilita o deshabilita un módulo, con motivo; cada cambio queda auditado
/// e invalida la caché de la cooperativa; deshabilitar no borra la fila ni nada más; los módulos nunca tocados salen apagados.
/// </summary>
public class SetTenantModuleCommandTests
{
    private static readonly Guid Maestro = Guid.NewGuid();
    private readonly TestAdminDbContext _admin = TestAdminDbContext.Create();
    private readonly ICurrentCentralUserContext _usuario = Substitute.For<ICurrentCentralUserContext>();
    private readonly IModulosHabilitados _modulos = Substitute.For<IModulosHabilitados>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly IAuditAppendOnlyWriter _auditoria = Substitute.For<IAuditAppendOnlyWriter>();
    private readonly Guid _cooperativa;

    public SetTenantModuleCommandTests()
    {
        _usuario.IsAuthenticated.Returns(true);
        _usuario.CentralUserId.Returns(Maestro);
        _usuario.IsGlobalMasterAdmin.Returns(true);
        _usuario.Email.Returns("maestro@plataforma.test");
        _reloj.UtcNow.Returns(new DateTime(2026, 11, 20, 15, 0, 0, DateTimeKind.Utc));

        var tenant = new Tenant { Name = "Cooperativa de prueba", SchemaName = "dbo" };
        _admin.Tenants.Add(tenant);
        _admin.SaveChanges();
        _cooperativa = tenant.PublicId;
    }

    private SetTenantModuleCommandHandler Handler() => new(_admin, _usuario, _modulos, _reloj, _auditoria);

    [Fact]
    public async Task Habilitar_crea_la_fila_audita_e_invalida_la_cache()
    {
        var r = await Handler().Handle(new SetTenantModuleCommand(_cooperativa, "trn", true, "Cooperativa de taxis"), default);

        r.IsSuccess.Should().BeTrue();
        var fila = await _admin.TenantModules.SingleAsync();
        fila.ModuleCode.Should().Be(ModulosHabilitables.Transporte);
        fila.IsEnabled.Should().BeTrue();
        fila.Reason.Should().Be("Cooperativa de taxis");
        fila.ChangedBy.Should().Be("maestro@plataforma.test");
        _modulos.Received(1).Invalidar(_cooperativa);
        await _auditoria.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(e => e.Action == "Platform.Module.Enabled"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deshabilitar_conserva_la_fila_y_la_apaga()
    {
        await Handler().Handle(new SetTenantModuleCommand(_cooperativa, "TRN", true, "Alta"), default);

        var r = await Handler().Handle(new SetTenantModuleCommand(_cooperativa, "TRN", false, "Fin del convenio"), default);

        r.IsSuccess.Should().BeTrue();
        var fila = await _admin.TenantModules.SingleAsync();
        fila.IsEnabled.Should().BeFalse();
        fila.IsDeleted.Should().BeFalse();
        await _auditoria.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(e => e.Action == "Platform.Module.Disabled"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Quien_no_es_maestro_no_habilita()
    {
        _usuario.IsGlobalMasterAdmin.Returns(false);

        var r = await Handler().Handle(new SetTenantModuleCommand(_cooperativa, "TRN", true, "Intento"), default);

        r.Error.Code.Should().Be("Saas.MasterOnly");
        (await _admin.TenantModules.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Una_cooperativa_inexistente_se_rechaza()
    {
        var r = await Handler().Handle(new SetTenantModuleCommand(Guid.NewGuid(), "TRN", true, "Intento"), default);

        r.Error.Code.Should().Be("Tenant.NotFound");
    }

    [Fact]
    public void El_validador_exige_motivo_y_un_modulo_habilitable()
    {
        var v = new SetTenantModuleCommandValidator();

        v.Validate(new SetTenantModuleCommand(_cooperativa, "XYZ", true, "x")).IsValid.Should().BeFalse();
        v.Validate(new SetTenantModuleCommand(_cooperativa, "TRN", true, " ")).IsValid.Should().BeFalse();
        v.Validate(new SetTenantModuleCommand(_cooperativa, "TRN", true, "Alta")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_consulta_muestra_apagado_lo_que_nunca_se_toco()
    {
        var r = await new GetTenantModulesQueryHandler(_admin, _usuario).Handle(new GetTenantModulesQuery(_cooperativa), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().ContainSingle(m => m.Code == "TRN" && !m.Enabled);
    }
}
