using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Tenants.UpdateTenantMfaPolicy;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Tenants;

public class UpdateTenantMfaPolicyCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ActorId = Guid.NewGuid();
    private static readonly Guid TenantPublicId = Guid.NewGuid();
    private const string Email = "admin@cooperativa.co";

    private readonly ICurrentCentralUserContext _currentUser;
    private readonly IMembershipChangedNotifier _notifier;
    private readonly IAuditAppendOnlyWriter _audit;
    private readonly IDateTimeService _clock;
    private readonly TestAdminDbContext _db;

    public UpdateTenantMfaPolicyCommandHandlerTests()
    {
        _currentUser = Substitute.For<ICurrentCentralUserContext>();
        _notifier = Substitute.For<IMembershipChangedNotifier>();
        _audit = Substitute.For<IAuditAppendOnlyWriter>();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(FixedNow);

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.CentralUserId.Returns(ActorId);
        _currentUser.Email.Returns(Email);
        _currentUser.Purpose.Returns(CentralJwtPurposes.Full);
        _currentUser.IsGlobalMasterAdmin.Returns(true);

        _db = TestAdminDbContext.Create();
        _db.Tenants.Add(new Tenant
        {
            PublicId = TenantPublicId,
            Name = "Coop Test",
            SchemaName = "coop_test",
            IsActive = true,
        });
        _db.SaveChanges();
    }

    /// <summary>
    /// Devuelve 0 sin que nadie lo configure: estas pruebas no montan miembros ni
    /// credenciales. El conteo de «cuántos quedarían sin método» tiene sus propias
    /// pruebas donde sí se monta.
    /// </summary>
    private readonly IMfaDirectory _credenciales = Substitute.For<IMfaDirectory>();

    private UpdateTenantMfaPolicyCommandHandler NewHandler() => new(
        _currentUser, _db, _credenciales, _notifier, _audit, _clock,
        NullLogger<UpdateTenantMfaPolicyCommandHandler>.Instance);

    [Fact]
    public async Task Activar_politica_crea_registro_invalida_cache_y_audita_Activated()
    {
        var result = await NewHandler().Handle(
            new UpdateTenantMfaPolicyCommand(TenantPublicId, IsRequired: true), default);

        result.IsSuccess.Should().BeTrue();

        var policy = await _db.TenantMfaPolicies.SingleAsync(p => p.TenantId == TenantPublicId);
        policy.IsRequired.Should().BeTrue();
        policy.ActivatedAt.Should().Be(FixedNow);
        policy.ActivatedByUserId.Should().Be(ActorId);

        await _notifier.Received(1).PublishForTenantMembersAsync(TenantPublicId, Arg.Any<CancellationToken>());
        await _audit.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(d =>
                d.Action == AuditEventTypes.TenantMfaPolicyActivated &&
                d.EntityType == nameof(TenantMfaPolicy) &&
                d.TenantId == TenantPublicId.ToString("N")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Desactivar_politica_actualiza_registro_invalida_cache_y_audita_Deactivated()
    {
        var existing = TenantMfaPolicy.CreateForTenant(TenantPublicId);
        existing.Enable(Guid.NewGuid(), FixedNow.AddDays(-1));
        _db.TenantMfaPolicies.Add(existing);
        await _db.SaveChangesAsync();

        var result = await NewHandler().Handle(
            new UpdateTenantMfaPolicyCommand(TenantPublicId, IsRequired: false), default);

        result.IsSuccess.Should().BeTrue();

        var policy = await _db.TenantMfaPolicies.SingleAsync(p => p.TenantId == TenantPublicId);
        policy.IsRequired.Should().BeFalse();
        policy.DeactivatedAt.Should().Be(FixedNow);
        policy.DeactivatedByUserId.Should().Be(ActorId);

        await _notifier.Received(1).PublishForTenantMembersAsync(TenantPublicId, Arg.Any<CancellationToken>());
        await _audit.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(d => d.Action == AuditEventTypes.TenantMfaPolicyDeactivated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cambio_idempotente_no_invalida_cache_ni_audita()
    {
        var existing = TenantMfaPolicy.CreateForTenant(TenantPublicId);
        existing.Enable(ActorId, FixedNow.AddDays(-1));
        _db.TenantMfaPolicies.Add(existing);
        await _db.SaveChangesAsync();

        var result = await NewHandler().Handle(
            new UpdateTenantMfaPolicyCommand(TenantPublicId, IsRequired: true), default);

        result.IsSuccess.Should().BeTrue();
        await _notifier.DidNotReceive().PublishForTenantMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Tenant_inexistente_devuelve_NotFound()
    {
        var result = await NewHandler().Handle(
            new UpdateTenantMfaPolicyCommand(Guid.NewGuid(), IsRequired: true), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Tenant.NotFound");
        await _notifier.DidNotReceive().PublishForTenantMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_permiso_de_admin_devuelve_fallo_de_autorizacion()
    {
        _currentUser.IsGlobalMasterAdmin.Returns(false);

        var result = await NewHandler().Handle(
            new UpdateTenantMfaPolicyCommand(TenantPublicId, IsRequired: true), default);

        result.IsFailure.Should().BeTrue();
        await _notifier.DidNotReceive().PublishForTenantMembersAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _audit.DidNotReceive().AppendAsync(Arg.Any<AuditEventDocument>(), Arg.Any<CancellationToken>());
    }
}
