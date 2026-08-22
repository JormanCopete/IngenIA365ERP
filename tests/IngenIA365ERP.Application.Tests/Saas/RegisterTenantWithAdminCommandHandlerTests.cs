using FluentAssertions;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Services;
using IngenIA365ERP.Application.Invitations.Services;
using IngenIA365ERP.Application.Saas.RegisterTenantWithAdmin;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Saas;

public class RegisterTenantWithAdminCommandHandlerTests
{
    private static readonly Guid MasterId = Guid.NewGuid();
    private const string MasterEmail = "master@saas.co";

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly TestAdminDbContext _db;
    private readonly ISecureTokenGenerator _tokens = new SecureTokenGenerator();
    private readonly IInvitationEmailDispatcher _email;

    public RegisterTenantWithAdminCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _db = TestAdminDbContext.Create();
        _email = Substitute.For<IInvitationEmailDispatcher>();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(MasterId);
        _currentUser.IsGlobalMasterAdmin.Returns(true);
        _currentUser.Email.Returns(MasterEmail);
    }

    /// <summary>
    /// El aprovisionador va simulado a proposito: crear el esquema fisico toca la
    /// base y estas pruebas corren sobre un contexto en memoria. Que se le llame
    /// —y que un fallo suyo no tumbe el alta— se comprueba en
    /// <see cref="ElAltaSobreviveSiElEsquemaNoSeAprovisiona"/>.
    /// </summary>
    private readonly ITenantSchemaProvisioner _aprovisionador =
        Substitute.For<ITenantSchemaProvisioner>();

    private RegisterTenantWithAdminCommandHandler NewHandler() => new(
        _currentUser, _db, _tokens, _aprovisionador, _email,
        Substitute.For<IAuditAppendOnlyWriter>(),
        Substitute.For<IDateTimeService>(),
        Options.Create(new IdentityEmailOptions()),
        NullLogger<RegisterTenantWithAdminCommandHandler>.Instance);

    private RegisterTenantWithAdminCommand NewCommand(string nit = "900123456") => new(
        Name: "Coop Test", SchemaName: "tenant_test",
        Subdomain: "test", Nit: nit,
        LegalName: "Coop Test SAS", LegalAddress: null, TaxRegime: null,
        ContactEmail: "contact@coop.co", ContactPhone: null,
        PlanType: "Basic", MaxUsers: 50, StorageLimitMb: 5000,
        FirstAdminEmail: "admin@coop.co");

    [Fact]
    public async Task No_master_rechaza()
    {
        _currentUser.IsGlobalMasterAdmin.Returns(false);

        var result = await NewHandler().Handle(NewCommand(), default);

        result.Error.Code.Should().Be("Saas.MasterOnly");
    }

    [Fact]
    public async Task Crea_tenant_y_emite_invitacion_admin()
    {
        var result = await NewHandler().Handle(NewCommand(), default);

        result.IsSuccess.Should().BeTrue();
        _db.Tenants.Should().ContainSingle();
        _db.Invitations.Should().ContainSingle(i => i.InviteAsTenantAdmin);
        await _email.Received(1).DispatchAsync(
            Arg.Any<InvitationEmailRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NIT_duplicado_devuelve_NitConflict()
    {
        // Seed previo con mismo NIT.
        _db.Tenants.Add(new Tenant
        {
            Name = "Otra", SchemaName = "t_otra",
            Nit = "900123456", LegalName = "Otra",
            ContactEmail = "x@y.co", IsActive = true,
        });
        _db.SaveChanges();

        var result = await NewHandler().Handle(NewCommand("900123456"), default);

        result.Error.Code.Should().Be("Tenant.NitConflict");
    }

    [Fact]
    public async Task ElAltaSobreviveSiElEsquemaNoSeAprovisiona()
    {
        // La cooperativa ya quedo guardada cuando se intenta crear su esquema.
        // Perder el registro por un fallo de aprovisionamiento seria peor que
        // quedarse a medias: el endpoint de provision es idempotente y lo repara.
        _aprovisionador
            .AprovisionarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("sin conexion"));

        var r = await NewHandler().Handle(NewCommand(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue("el alta no puede perderse por un fallo de esquema");
        _db.Tenants.Should().ContainSingle();
    }

    [Fact]
    public async Task ElAltaAprovisionaElEsquemaDeLaCooperativa()
    {
        // Sin esto la cooperativa existia en la consola y no tenia donde guardar
        // nada hasta el siguiente reinicio de la API.
        await NewHandler().Handle(NewCommand(), CancellationToken.None);

        await _aprovisionador.Received(1).AprovisionarAsync(
            "tenant_test", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
