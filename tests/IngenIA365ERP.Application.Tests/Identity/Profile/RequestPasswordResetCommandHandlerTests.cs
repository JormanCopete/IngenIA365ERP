using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Services;
using IngenIA365ERP.Application.Identity.Profile.RequestPasswordReset;
using IngenIA365ERP.Application.Identity.Profile.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Profile;

public class RequestPasswordResetCommandHandlerTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 31, 12, 0, 0, DateTimeKind.Utc);
    private const string Email = "ana@cooperativa.co";

    private readonly ICentralIdentityProvider _identity;
    private readonly TestAdminDbContext _db;
    private readonly ISecureTokenGenerator _tokens = new SecureTokenGenerator();
    private readonly IPasswordResetEmailDispatcher _emailDispatcher;
    private readonly IAuditAppendOnlyWriter _audit;
    private readonly IDateTimeService _clock;

    public RequestPasswordResetCommandHandlerTests()
    {
        _identity = Substitute.For<ICentralIdentityProvider>();
        _db = TestAdminDbContext.Create();
        _emailDispatcher = Substitute.For<IPasswordResetEmailDispatcher>();
        _audit = Substitute.For<IAuditAppendOnlyWriter>();
        _clock = Substitute.For<IDateTimeService>();
        _clock.UtcNow.Returns(FixedNow);
    }

    private RequestPasswordResetCommandHandler NewHandler() => new(
        _identity, _db, _tokens, _emailDispatcher, _audit, _clock,
        NullLogger<RequestPasswordResetCommandHandler>.Instance);

    [Fact]
    public async Task Email_inexistente_devuelve_OK_sin_enviar_correo()
    {
        _identity.FindByEmailAsync(Email, Arg.Any<CancellationToken>())
            .Returns((CentralUser?)null);

        var result = await NewHandler().Handle(new RequestPasswordResetCommand(Email), default);

        result.IsSuccess.Should().BeTrue("FR-041 — defensa anti-enumeración");
        await _emailDispatcher.DidNotReceiveWithAnyArgs().DispatchAsync(
            default!, default!, default!, default, default, default);
        _db.PasswordResetTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task Email_existente_persiste_token_y_envia_correo()
    {
        var userId = Guid.NewGuid();
        _identity.FindByEmailAsync(Email, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = userId, Email = Email });

        var result = await NewHandler().Handle(new RequestPasswordResetCommand(Email), default);

        result.IsSuccess.Should().BeTrue();
        _db.PasswordResetTokens.Should().ContainSingle(t => t.CentralUserId == userId);
        await _emailDispatcher.Received(1).DispatchAsync(
            Email, Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Doble_request_consume_token_previo()
    {
        var userId = Guid.NewGuid();
        _identity.FindByEmailAsync(Email, Arg.Any<CancellationToken>())
            .Returns(new CentralUser { Id = userId, Email = Email });

        await NewHandler().Handle(new RequestPasswordResetCommand(Email), default);
        await NewHandler().Handle(new RequestPasswordResetCommand(Email), default);

        var tokens = _db.PasswordResetTokens.ToList();
        tokens.Should().HaveCount(2, "ambos persistidos");
        tokens.Where(t => t.ConsumedAt is null).Should().HaveCount(1, "el primero fue 'consumed' al emitir el segundo");
    }
}
