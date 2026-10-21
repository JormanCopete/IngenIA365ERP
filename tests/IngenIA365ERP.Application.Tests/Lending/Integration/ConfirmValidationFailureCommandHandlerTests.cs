using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Integration.Comercial;
using IngenIA365ERP.Application.Lending.Loans.Commands;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;
using ScreeningOutcome = IngenIA365ERP.Domain.Enums.Lending.ScreeningOutcome;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US6, T0573 (FR-038, FR-052, T63, B8; api.md §8.2; mensajes.md §15.5): confirmar por excepción una obligación de venta con validación
/// fallida cambia el recibo a <c>ConfirmedByException</c> con su <c>CreditDecision</c> (sujeto <c>ValidationFailureException</c>), levanta el bloqueo
/// <c>ValidationFailed</c> y deja la entrega como estaba (B8); lo no exceptuable (listas, usura, autorización, capacidad) responde
/// <c>Lending.Attribution.NotExceptionable</c>; un crédito sin validación fallida, <c>Lending.Loan.NotValidationFailed</c>; por encima de la atribución
/// operativa, <c>Lending.Attribution.LevelRequired</c>; y la segregación, <c>Lending.Segregation.Conflict</c>.
/// </summary>
public sealed class ConfirmValidationFailureCommandHandlerTests : IAsyncLifetime
{
    private VentasACreditoDePrueba _v = null!;

    public async Task InitializeAsync() => _v = await VentasACreditoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private ConfirmValidationFailureCommandHandler Confirmar() =>
        new(_v.Db, _v.O.Cerrojo, _v.G.Intervenciones(), _v.G.Alcance, _v.G.Auditoria, _v.G.Reloj);

    /// <summary>Una venta pendiente que excede el cupo: validación fallida exceptuable (<c>Lending.CreditLimit.Insufficient</c>).</summary>
    private async Task<(Guid Mensaje, VentasACreditoDePrueba.Venta Venta)> FallidaAsync(bool porListas = false)
    {
        _v.Rotativo(_v.Ana, 300_000m);
        if (porListas) _v.O.ListasDe[_v.Ana.Id] = ScreeningOutcome.Match;
        var venta = _v.NuevaVenta(600_000m);
        var (mensaje, r) = await _v.VenderAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta, pendiente: true));
        r.Should().BeOfType<ResultadoDeConsumo.ValidationFailed>();
        _v.G.ActuarComo(_v.O.Analista);
        return (mensaje, venta);
    }

    private Task<IngenIA365ERP.Application.Common.Models.Result<Application.Lending.Loans.LoanDto>> ConfirmarAsync(Guid credito, string motivo = "Cliente con buen historial") =>
        Confirmar().Handle(new ConfirmValidationFailureCommand(credito, motivo) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task Confirma_por_excepcion_cambia_el_recibo_levanta_el_bloqueo_y_no_toca_la_entrega()
    {
        var (mensaje, venta) = await FallidaAsync();
        var credito = await _v.CreditoDeAsync(venta);
        var entregaAntes = await _v.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(e => _v.Db.IntegrationMessages.Any(m => m.Id == e.MessageId && m.PublicId == mensaje));

        var r = await ConfirmarAsync(credito.PublicId);
        _v.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Marks.ValidationFailed.Should().BeFalse("el crédito sale sin la marca");
        var recibo = await _v.Db.LendingInboundMessages.AsNoTracking().SingleAsync();
        (recibo.Status, recibo.ExceptionReason, recibo.ConfirmedByExceptionByUserId).Should()
            .Be((InboundMessageStatus.ConfirmedByException, "Cliente con buen historial", (int?)_v.O.Analista.Id));
        var decision = await _v.Db.CreditDecisions.AsNoTracking().SingleAsync(d => d.Id == recibo.ExceptionDecisionId);
        (decision.SubjectType, decision.SubjectPublicId, decision.IsPolicyException, decision.Outcome).Should()
            .Be((LendingDecisionSubjects.ValidationFailureException, credito.PublicId, true, CreditDecisionOutcome.Approve));
        (await _v.Db.DebtorBlocks.AsNoTracking().SingleAsync(b => b.Reason == DebtorBlockReason.ValidationFailed)).LiftedAt.Should().NotBeNull();
        (await _v.Db.Loans.AsNoTracking().SingleAsync(l => l.Id == credito.Id)).ConfirmedByExceptionDecisionId.Should().Be(decision.Id);
        (await _v.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(e => e.Id == entregaAntes.Id)).Status.Should().Be(entregaAntes.Status,
            "la entrega queda como hecho histórico (B8)");
        var validacion = await _v.Consultas().EstadoDeValidacionAsync(mensaje, default);
        (validacion.Status, validacion.Reason).Should().Be((EstadosDeValidacion.Validated, "confirmada por excepción: Cliente con buen historial"));
    }

    [Fact]
    public async Task Las_listas_no_admiten_excepcion()
    {
        var (_, venta) = await FallidaAsync(porListas: true);

        var r = await ConfirmarAsync((await _v.CreditoDeAsync(venta)).PublicId);

        r.Error.Code.Should().Be(NoExceptuables.CodigoDeError);
    }

    [Fact]
    public async Task Un_credito_sin_validacion_fallida_no_se_confirma()
    {
        var venta = _v.NuevaVenta();
        await _v.VenderAsync(venta);
        _v.G.ActuarComo(_v.O.Analista);

        var r = await ConfirmarAsync((await _v.CreditoDeAsync(venta)).PublicId);

        r.Error.Code.Should().Be(ErroresDeVentasACredito.CodigoSinValidacionFallida);
    }

    [Fact]
    public async Task Por_encima_de_la_atribucion_operativa_pasa_a_decision()
    {
        var (_, venta) = await FallidaAsync();
        Limite(_v.O.Analista, 100_000m);

        var r = await ConfirmarAsync((await _v.CreditoDeAsync(venta)).PublicId);

        r.Error.Code.Should().Be("Lending.Attribution.LevelRequired");
        (await _v.Db.LendingInboundMessages.AsNoTracking().SingleAsync()).Status.Should().Be(InboundMessageStatus.ValidationFailed);
    }

    [Fact]
    public async Task La_segregacion_impide_confirmar_a_quien_ya_intervino()
    {
        var (_, venta) = await FallidaAsync();
        var credito = await _v.CreditoDeAsync(venta);
        _v.Db.CaseParticipations.Add(CaseParticipation.Registrar("Loan", credito.PublicId, "Loan", credito.PublicId, AccionesDeSegregacion.Radicar,
            _v.O.Analista.Id, _v.O.Analista.PersonId!.Value, _v.G.UtcNow.AddHours(-1)));
        await _v.Db.SaveChangesAsync();
        _v.Db.ChangeTracker.Clear();

        var r = await ConfirmarAsync(credito.PublicId);

        r.Error.Code.Should().Be("Lending.Segregation.Conflict");
    }

    [Fact]
    public async Task El_validador_exige_el_motivo() =>
        (await new ConfirmValidationFailureCommandValidator().ValidateAsync(new ConfirmValidationFailureCommand(Guid.NewGuid(), ""))).IsValid.Should().BeFalse();

    private void Limite(User usuario, decimal maximo)
    {
        var permiso = new Permission { Resource = "Lending.ValidationFailures", Action = "Confirm" };
        _v.Db.Permissions.Add(permiso);
        var rol = new Role { Code = "EXCEP", Name = "Excepciones", IsActive = true };
        _v.Db.Roles.Add(rol);
        _v.Db.SaveChanges();
        _v.Db.RolePermissions.Add(new RolePermission { RoleId = rol.Id, PermissionId = permiso.Id });
        _v.Db.UserRoles.Add(new UserRole { UserId = usuario.Id, RoleId = rol.Id });
        _v.Db.PermissionAmountLimits.Add(new PermissionAmountLimit
        {
            RoleId = rol.Id, PermissionCode = ConfirmValidationFailureCommandHandler.Permiso, MaxAmount = maximo, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
        });
        _v.Db.SaveChanges();
        _v.Db.ChangeTracker.Clear();
    }
}
