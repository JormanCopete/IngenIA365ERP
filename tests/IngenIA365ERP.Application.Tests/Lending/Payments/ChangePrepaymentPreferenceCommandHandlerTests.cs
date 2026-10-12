using FluentAssertions;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Application.Lending.Payments.Commands;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>
/// Feature 013, US5, T0521 (api.md §8.3; FR-035, FR-075, D24, T24, C85): cambiar la preferencia de abono de un crédito registra una versión nueva de
/// <c>LND_PrepaymentPreferences</c> con <c>Origin = DebtorRequest</c> y una operación <c>PreferenceChange</c> sin filas de libro, con motivo obligatorio;
/// <c>Choice</c> nulo la revoca; un crédito que no está activo responde <c>Lending.Loan.NotActive</c>; rige la última con <c>RegisteredAt ≤ R</c>; sin
/// <c>Lending.PrepaymentPreferences.Manage</c>, el 404 genérico.
/// </summary>
public class ChangePrepaymentPreferenceCommandHandlerTests : IAsyncLifetime
{
    private PagosDePrueba _p = null!;

    public async Task InitializeAsync()
    {
        _p = await PagosDePrueba.CrearAsync();
        _p.G.Conceder(ChangePrepaymentPreferenceCommandHandler.Permiso);
        _p.G.UtcNow = _p.G.UtcNow.AddMinutes(5);
    }

    public Task DisposeAsync()
    {
        _p.Dispose();
        return Task.CompletedTask;
    }

    private Task<IngenIA365ERP.Application.Common.Models.Result<PrepaymentPreferenceDto>> CambiarAsync(PrepaymentChoice? eleccion, string motivo = "Instrucción escrita del deudor") =>
        _p.CambiarPreferencia().Handle(new ChangePrepaymentPreferenceCommand(_p.Credito.PublicId, eleccion, motivo) { OperationKey = Guid.NewGuid() }, default);

    [Fact]
    public async Task Cambiarla_registra_una_version_nueva_y_una_operacion_PreferenceChange_sin_libro()
    {
        var r = await CambiarAsync(PrepaymentChoice.ReduceInstallment);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Current!.Choice.Should().Be(nameof(PrepaymentChoice.ReduceInstallment));
        r.Value.Current.Origin.Should().Be(nameof(PreferenceOrigin.DebtorRequest));
        r.Value.History.Should().HaveCount(2, "la de la solicitud y la nueva");
        _p.Db.ChangeTracker.Clear();
        var op = await _p.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.Kind == LoanOperationKind.PreferenceChange);
        op.Reason.Should().Be("Instrucción escrita del deudor");
        op.PrepaymentChoice.Should().Be(PrepaymentChoice.ReduceInstallment);
        (await _p.Db.LoanTransactions.AnyAsync(t => t.OperationId == op.Id)).Should().BeFalse("cambiar la preferencia no mueve el libro");
        var version = await _p.Db.PrepaymentPreferences.AsNoTracking().OrderByDescending(x => x.VersionNumber).FirstAsync(x => x.LoanId == _p.Credito.Id);
        (version.VersionNumber, version.OperationId, version.Origin).Should().Be((2, (long?)op.Id, PreferenceOrigin.DebtorRequest));
        await _p.ElLibroCuadraAsync();
    }

    [Fact]
    public async Task Sin_eleccion_la_revoca_y_el_abono_sin_interaccion_queda_como_saldo_a_favor()
    {
        (await CambiarAsync(null)).Value.Current!.Choice.Should().BeNull();
        _p.G.UtcNow = _p.G.UtcNow.AddMinutes(1);
        _p.Db.ChangeTracker.Clear();

        var c = await _p.AplicadoAsync(_p.Pago(IngenIA365ERP.Application.Common.Integration.Lending.ModalidadDePago.ExtraordinaryPrincipal, 100_000m));

        c.CreditBalanceAmount.Should().Be(100_000m);
    }

    [Fact]
    public async Task Rige_la_ultima_registrada_hasta_el_instante_de_la_lectura()
    {
        await CambiarAsync(PrepaymentChoice.ReduceInstallment);
        _p.G.UtcNow = _p.G.UtcNow.AddMinutes(1);
        await CambiarAsync(PrepaymentChoice.ReduceTerm);
        _p.Db.ChangeTracker.Clear();

        var actual = await _p.Preferencia().Handle(new GetPrepaymentPreferenceQuery(_p.Credito.PublicId), default);
        actual.Value.Current!.Version.Should().Be(3);
        actual.Value.Current.Choice.Should().Be(nameof(PrepaymentChoice.ReduceTerm));
        var antes = await _p.Preferencias().DelCreditoAsync(_p.Credito.Id, _p.G.UtcNow.AddSeconds(-30), default);
        antes!.Choice.Should().Be(PrepaymentChoice.ReduceInstallment, "a un instante anterior rige la versión que había (T24)");
    }

    [Fact]
    public async Task Un_credito_que_no_esta_activo_no_cambia_su_preferencia()
    {
        var credito = await _p.Db.Loans.SingleAsync(l => l.Id == _p.Credito.Id);
        credito.Status = LoanStatus.PaidOff;
        await _p.Db.SaveChangesAsync();
        (await CambiarAsync(PrepaymentChoice.ReduceTerm)).Error.Code.Should().Be("Lending.Loan.NotActive");
    }

    [Fact]
    public async Task Sin_el_permiso_responde_el_404_generico()
    {
        _p.G.Conceder();
        (await CambiarAsync(PrepaymentChoice.ReduceTerm)).Error.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public void El_motivo_es_obligatorio()
    {
        new ChangePrepaymentPreferenceCommandValidator().Validate(new ChangePrepaymentPreferenceCommand(Guid.NewGuid(), PrepaymentChoice.ReduceTerm, " "))
            .IsValid.Should().BeFalse();
    }
}
