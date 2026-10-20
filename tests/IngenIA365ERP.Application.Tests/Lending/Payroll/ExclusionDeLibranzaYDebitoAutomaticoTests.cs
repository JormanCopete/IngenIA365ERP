using FluentAssertions;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Transfers;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 014, E249 (decisión del dueño del 2026-10-21): un crédito se paga por libranza o nómina, o por débito automático, nunca por las dos. La regla vive
/// en <see cref="ExclusionDeLibranzaYDebitoAutomatico"/>: el crédito con vínculo de libranza vigente se reconoce como tal, el débito automático vigente que
/// nombra el crédito se reconoce, y el traslado de la libranza se niega si el crédito tiene un débito automático vigente.
/// </summary>
public sealed class ExclusionDeLibranzaYDebitoAutomaticoTests : IAsyncLifetime
{
    private LibranzaDePrueba _l = null!;

    public async Task InitializeAsync() => _l = await LibranzaDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    private DirectDebitMandate Mandato(DirectDebitScope alcance, int? credito, DateOnly? hasta = null) => new()
    {
        MemberAccountId = 1, AuthorizedByPersonId = _l.Credito.DebtorPersonId, Scope = alcance, LoanId = credito, ValidFrom = LibranzaDePrueba.Hoy.AddDays(-1),
        ValidTo = hasta, AuthorizationAttachmentPublicId = Guid.NewGuid(),
    };

    [Fact]
    public async Task El_credito_con_vinculo_de_libranza_vigente_se_reconoce_y_sin_el_no()
    {
        (await ExclusionDeLibranzaYDebitoAutomatico.ConLibranzaAsync(_l.Db, [_l.Credito.Id], default)).Should().Contain(_l.Credito.Id);

        var vinculo = await _l.Db.LoanPayrollLinks.SingleAsync(v => v.LoanId == _l.Credito.Id && v.ValidTo == null);
        vinculo.RepaymentChannel = RepaymentChannel.Cashier;
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        (await ExclusionDeLibranzaYDebitoAutomatico.ConLibranzaAsync(_l.Db, [_l.Credito.Id], default)).Should().BeEmpty("por ventanilla no es libranza");

        vinculo = await _l.Db.LoanPayrollLinks.SingleAsync(v => v.LoanId == _l.Credito.Id && v.ValidTo == null);
        vinculo.RepaymentChannel = RepaymentChannel.OwnPayroll;
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        (await ExclusionDeLibranzaYDebitoAutomatico.ConLibranzaAsync(_l.Db, [_l.Credito.Id], default)).Should().Contain(_l.Credito.Id, "la nómina propia también es nómina");
    }

    [Fact]
    public async Task Solo_la_autorizacion_vigente_que_nombra_el_credito_cuenta_como_debito_automatico()
    {
        var hoy = LibranzaDePrueba.Hoy;
        (await ExclusionDeLibranzaYDebitoAutomatico.CuentaConDebitoAutomaticoAsync(_l.Db, _l.Credito.Id, hoy, default)).Should().BeNull();

        _l.Db.DirectDebitMandates.Add(Mandato(DirectDebitScope.AllLoansOfHolder, null));
        _l.Db.DirectDebitMandates.Add(Mandato(DirectDebitScope.SingleLoan, _l.Credito.Id, hoy.AddDays(-1)));
        await _l.Db.SaveChangesAsync();
        (await ExclusionDeLibranzaYDebitoAutomatico.CuentaConDebitoAutomaticoAsync(_l.Db, _l.Credito.Id, hoy, default))
            .Should().BeNull("«todos mis créditos» no nombra el crédito (el ciclo lo salta) y la otra ya venció");

        _l.Db.DirectDebitMandates.Add(Mandato(DirectDebitScope.SingleLoan, _l.Credito.Id));
        await _l.Db.SaveChangesAsync();
        (await ExclusionDeLibranzaYDebitoAutomatico.CuentaConDebitoAutomaticoAsync(_l.Db, _l.Credito.Id, hoy, default)).Should().NotBeNull();
    }

    [Fact]
    public async Task El_traslado_de_la_libranza_se_niega_si_el_credito_tiene_debito_automatico_vigente_y_la_forma_alterna_no()
    {
        var gobernacion = await _l.ConvenioAsync("GOBVALLE", _l.OtraPagaduria);
        _l.Db.DirectDebitMandates.Add(Mandato(DirectDebitScope.SingleLoan, _l.Credito.Id));
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        _l.G.ActuarComo(_l.D.Cajera);

        var r = await _l.Trasladar().Handle(new TransferLoanPayrollCommand(_l.Credito.PublicId, "Lo trasladaron a la Gobernación", gobernacion.PublicId, "GOB-778",
            Guid.NewGuid(), LibranzaDePrueba.Hoy.AddDays(-2), 10_000_000m) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ExclusionDeLibranzaYDebitoAutomatico.CodigoCreditoConDebitoAutomatico);
        r.Error.Message.Should().Contain("débito automático").And.Contain("libranza");
        (await _l.Db.LoanPayrollLinks.CountAsync(v => v.LoanId == _l.Credito.Id)).Should().Be(1, "no se abrió un vínculo nuevo");
    }
}
