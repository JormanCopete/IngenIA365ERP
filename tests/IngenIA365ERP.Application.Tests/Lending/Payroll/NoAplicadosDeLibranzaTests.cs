using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.NotApplied;
using IngenIA365ERP.Application.Lending.Payroll.Results;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 013, K2, T0813 (FR-112, FR-115, C32, C88): los no aplicados y su seguimiento.
/// <list type="bullet">
/// <item>la lista trae los de la nómina propia (origen <c>Contract</c>, nacidos desde K1) y los de la activación, y respeta el alcance de oficina y
/// línea;</item>
/// <item>resolver (pasar a caja, a cobranza o cerrar) exige motivo y avisa al deudor por sus canales; dos veces responde
/// <c>Lending.InstallmentNonDeduction.AlreadyResolved</c>;</item>
/// <item>desde K2, un <c>NoDescontado</c> de Nómina recibe en la misma transacción el seguimiento automático del convenio de la nómina propia.</item>
/// </list>
/// </summary>
public sealed class NoAplicadosDeLibranzaTests : IAsyncLifetime
{
    private LibranzaDePrueba _l = null!;
    private DateOnly _vencimiento;

    public async Task InitializeAsync()
    {
        _l = await LibranzaDePrueba.CrearAsync(conConvenio: false);
        _l.Convenio = await _l.ConvenioAsync("NOMINA", _l.Cooperativa, nominaPropia: true);
        _l.Vincular(_l.Credito, _l.Convenio, null, RepaymentChannel.OwnPayroll);
        _vencimiento = (await _l.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync()).DueDate;
        await _l.P.CausarHastaAsync(_vencimiento.AddDays(1));
    }

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    private async Task<InstallmentNonDeduction> DeNominaAsync()
    {
        var r = await _l.NoDescontados().RegistrarAsync(Recaudador.Nomina, new NoDescontado($"NOM:{Guid.NewGuid():N}:{_l.Credito.PublicId:N}", _l.Credito.PublicId, null,
            160_000m, 60_000m, MotivoDeNoDescuento.DeductionCap, "Tope de descuentos", Guid.NewGuid()), default);
        r.Result.Should().Be(ResultadoDelContrato.Applied, $"{r.Code}: {r.Message}");
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        return await _l.Db.InstallmentNonDeductions.AsNoTracking().OrderByDescending(n => n.Id).FirstAsync();
    }

    private async Task<InstallmentNonDeduction> DeActivacionAsync()
    {
        var cuota = await _l.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync();
        var fila = new InstallmentNonDeduction
        {
            LoanId = _l.Credito.Id, RepaymentInstallmentId = cuota.Id, InstallmentNumber = cuota.InstallmentNumber, DueDate = cuota.DueDate,
            Origin = NonDeductionOrigin.Activation, Reason = NonDeductionReason.Other, ExpectedAmount = 160_000m, NotDeductedAmount = 160_000m,
        };
        _l.Db.InstallmentNonDeductions.Add(fila);
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();
        return fila;
    }

    private async Task<IngenIA365ERP.Application.Common.Models.Result<InstallmentNonDeductionDto>> ResolverAsync(Guid noAplicado, NonDeductionFollowUpAction accion,
        string motivo = "Acordado con el asociado")
    {
        _l.G.ActuarComo(_l.D.Cajera);
        var r = await _l.Resolver().Handle(new ResolveInstallmentNonDeductionCommand(noAplicado, accion, motivo) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Un_no_descontado_de_Nomina_recibe_el_seguimiento_del_convenio_de_la_nomina_propia()
    {
        var n = await DeNominaAsync();

        n.Origin.Should().Be(NonDeductionOrigin.Contract);
        var seguimiento = await _l.Db.NonDeductionFollowUps.AsNoTracking().Where(s => s.InstallmentNonDeductionId == n.Id).ToListAsync();
        seguimiento.Should().Contain(s => s.Action == NonDeductionFollowUpAction.MovedToCashPayment && s.IsAutomatic, "la regla del convenio");
        seguimiento.Should().Contain(s => s.Action == NonDeductionFollowUpAction.DebtorNotified && s.DebtorMessageId != null);
        var aviso = await _l.Db.DebtorMessages.AsNoTracking().SingleAsync(m => m.InstallmentNonDeductionId == n.Id);
        aviso.OriginKind.Should().Be(SeguimientoDeNoAplicados.OrigenDelMensaje);
    }

    [Fact]
    public async Task La_lista_trae_los_de_Nomina_y_los_de_la_activacion_y_respeta_el_alcance()
    {
        var nomina = await DeNominaAsync();
        var activacion = await DeActivacionAsync();

        var todos = (await _l.NoAplicados().Handle(new ListInstallmentNonDeductionsQuery(), default)).Value;
        todos.Items.Select(i => i.PublicId).Should().BeEquivalentTo([nomina.PublicId, activacion.PublicId]);
        todos.Items.Should().OnlyContain(i => i.LoanNumber == _l.Credito.Number && !i.Resolved);
        (await _l.NoAplicados().Handle(new ListInstallmentNonDeductionsQuery(Origin: NonDeductionOrigin.Activation), default)).Value.Items
            .Should().ContainSingle(i => i.PublicId == activacion.PublicId);
        (await _l.NoAplicados().Handle(new ListInstallmentNonDeductionsQuery(_l.Convenio.PublicId), default)).Value.Items.Should().HaveCount(2,
            "los de la nómina propia sin resultado se ligan al convenio por el vínculo del crédito");
        (await _l.NoAplicados().Handle(new ListInstallmentNonDeductionsQuery(Status: "Open"), default)).Value.TotalCount.Should().Be(2);

        _l.G.AlcanceActual = AlcanceDeCartera.Vacio with { Oficinas = new HashSet<int> { 9999 }, TodasLasLineas = true };
        (await _l.NoAplicados().Handle(new ListInstallmentNonDeductionsQuery(), default)).Value.Items.Should().BeEmpty();
        (await ResolverAsync(nomina.PublicId, NonDeductionFollowUpAction.Closed)).Error.Code.Should().Be(ErroresDeLibranza.CodigoNoAplicadoInexistente,
            "fuera del alcance, el 404");
    }

    [Theory]
    [InlineData(NonDeductionFollowUpAction.MovedToCashPayment)]
    [InlineData(NonDeductionFollowUpAction.SentToCollection)]
    [InlineData(NonDeductionFollowUpAction.Closed)]
    public async Task Resolver_avisa_al_deudor_y_dos_veces_no_se_puede(NonDeductionFollowUpAction accion)
    {
        var n = await DeActivacionAsync();

        var r = await ResolverAsync(n.PublicId, accion);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        (r.Value.Resolved, r.Value.LastAction).Should().Be((true, accion.ToString()));
        r.Value.NotifiedAt.Should().NotBeNull();
        var seguimiento = await _l.Db.NonDeductionFollowUps.AsNoTracking().Where(s => s.InstallmentNonDeductionId == n.Id).ToListAsync();
        seguimiento.Should().Contain(s => s.Action == accion && !s.IsAutomatic && s.Reason == "Acordado con el asociado" && s.ActorUserId != null);
        (await _l.Db.DebtorMessages.AsNoTracking().CountAsync(m => m.InstallmentNonDeductionId == n.Id)).Should().Be(1);

        (await ResolverAsync(n.PublicId, NonDeductionFollowUpAction.Closed)).Error.Code.Should().Be(ErroresDeLibranza.CodigoNoAplicadoYaResuelto);
        (await _l.NoAplicados().Handle(new ListInstallmentNonDeductionsQuery(Status: "Resolved"), default)).Value.Items.Should().ContainSingle();
    }

    [Fact]
    public void Resolver_exige_motivo_y_una_de_las_tres_salidas()
    {
        var v = new ResolveInstallmentNonDeductionCommandValidator();
        v.Validate(new ResolveInstallmentNonDeductionCommand(Guid.NewGuid(), NonDeductionFollowUpAction.Closed, " ")).IsValid.Should().BeFalse();
        v.Validate(new ResolveInstallmentNonDeductionCommand(Guid.NewGuid(), NonDeductionFollowUpAction.DebtorNotified, "x")).IsValid.Should().BeFalse();
        v.Validate(new ResolveInstallmentNonDeductionCommand(Guid.NewGuid(), NonDeductionFollowUpAction.SentToCollection, "x")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Un_no_aplicado_que_no_existe_responde_su_404()
    {
        (await ResolverAsync(Guid.NewGuid(), NonDeductionFollowUpAction.Closed)).Error.Code.Should().Be(ErroresDeLibranza.CodigoNoAplicadoInexistente);
    }
}
