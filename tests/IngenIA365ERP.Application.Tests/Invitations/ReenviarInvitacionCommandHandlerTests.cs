using FluentAssertions;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Services;
using IngenIA365ERP.Application.Invitations.GestionInvitaciones;
using IngenIA365ERP.Application.Invitations.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Invitations;

/// <summary>
/// El reenvío persiste la invitación nueva y DESPUÉS manda el correo, y el envío es
/// fail-soft: en producción, sin servidor de correo, el botón respondía 500 tras veinte
/// segundos («Ocurrió un error inesperado») y nadie sabía que la invitación anterior ya
/// no valía.
/// </summary>
public class ReenviarInvitacionCommandHandlerTests
{
    private static readonly DateTime Ahora = new(2026, 9, 11, 3, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Cooperativa = Guid.NewGuid();
    private static readonly Guid Maestro = Guid.NewGuid();

    private readonly TestAdminDbContext _db = TestAdminDbContext.Create();
    private readonly IInvitationEmailDispatcher _correo = Substitute.For<IInvitationEmailDispatcher>();
    private readonly Invitation _original;

    public ReenviarInvitacionCommandHandlerTests()
    {
        _db.Tenants.Add(new Tenant { PublicId = Cooperativa, Name = "COOFLOPAL", SchemaName = "cooflopal", IsActive = true, ContactEmail = "c@coop.co" });
        _original = Invitation.Create("copesan@hotmail.com", "COPESAN@HOTMAIL.COM", Cooperativa, Maestro,
            inviteAsTenantAdmin: true, tokenHash: System.Security.Cryptography.RandomNumberGenerator.GetBytes(32),
            createdAt: Ahora.AddHours(-1), expiresAt: Ahora.AddDays(6));
        _db.Invitations.Add(_original);
        _db.SaveChanges();
    }

    [Fact]
    public async Task Con_correo_el_resultado_dice_enviado_y_la_anterior_queda_reemplazada()
    {
        var r = await Handler().Handle(new ReenviarInvitacionCommand(_original.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.CorreoEnviado.Should().BeTrue();
        r.Value.MotivoCorreoNoEnviado.Should().BeNull();
        (await _db.Invitations.SingleAsync(i => i.PublicId == _original.PublicId)).Status.Should().Be(InvitationStatus.Superseded);
        (await _db.Invitations.SingleAsync(i => i.PublicId == r.Value.NuevaInvitacionPublicId)).Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public async Task Sin_servidor_de_correo_no_es_500_la_nueva_queda_y_el_resultado_trae_el_motivo()
    {
        _correo.DispatchAsync(Arg.Any<InvitationEmailRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new System.Net.Sockets.SocketException(111));

        var r = await Handler().Handle(new ReenviarInvitacionCommand(_original.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue("la invitación nueva quedó persistida; lo que falló fue el correo");
        r.Value.CorreoEnviado.Should().BeFalse();
        r.Value.MotivoCorreoNoEnviado.Should().Contain("servidor de correo");
        (await _db.Invitations.CountAsync(i => i.Status == InvitationStatus.Pending)).Should().Be(1, "la nueva; la anterior ya no vale");
        (await _db.Invitations.SingleAsync(i => i.PublicId == _original.PublicId)).Status.Should().Be(InvitationStatus.Superseded);
    }

    [Fact]
    public async Task Cancelar_la_peticion_no_se_disfraza_de_correo_no_enviado()
    {
        _correo.DispatchAsync(Arg.Any<InvitationEmailRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var acto = () => Handler().Handle(new ReenviarInvitacionCommand(_original.PublicId), CancellationToken.None);

        await acto.Should().ThrowAsync<OperationCanceledException>();
    }

    private ReenviarInvitacionCommandHandler Handler()
    {
        var usuario = Substitute.For<ICurrentCentralUserContext>();
        usuario.CentralUserId.Returns(Maestro);
        usuario.IsAuthenticated.Returns(true);
        usuario.IsGlobalMasterAdmin.Returns(true);
        usuario.Purpose.Returns(CentralJwtPurposes.Full);
        usuario.Email.Returns("master@ingenia365.com");
        var reloj = Substitute.For<IDateTimeService>();
        reloj.UtcNow.Returns(Ahora);

        return new ReenviarInvitacionCommandHandler(
            usuario, _db, new SecureTokenGenerator(), _correo, reloj,
            Options.Create(new IdentityEmailOptions()), NullLogger<ReenviarInvitacionCommandHandler>.Instance);
    }
}
