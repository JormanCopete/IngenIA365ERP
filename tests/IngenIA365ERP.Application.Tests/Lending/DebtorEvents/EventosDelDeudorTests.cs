using FluentAssertions;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.DebtorEvents;
using IngenIA365ERP.Application.Tests.Lending.Novelties;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.DebtorEvents;

/// <summary>
/// Los eventos del deudor, la reclamación al seguro y la insolvencia (feature 013, K3, T0865, T0886; FR-097, FR-098, T102, api.md §11.4): el evento se
/// guarda como bloqueo; el fallecimiento abre la reclamación de cada crédito con seguro de vida deudores y cancela los mensajes de cobro en cola; la
/// reclamación se niega o se desiste sólo en trámite; la insolvencia abre en negociación, suspende la causación en la reconstrucción y fracasa sólo
/// negociando. (nuevo)
/// </summary>
public sealed class EventosDelDeudorTests : IAsyncLifetime
{
    private NovedadesDePrueba _n = null!;

    public async Task InitializeAsync() => _n = await NovedadesDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _n.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Application.Common.Models.Result<DebtorEventDto>> RegistrarAsync(DebtorBlockReason clase, DateOnly? fecha = null, string? notas = null)
    {
        _n.G.ActuarComo(_n.D.O.Asesora);
        var r = await _n.RegistrarEvento().Handle(new RegisterDebtorEventCommand(_n.D.O.Ana.PublicId, clase, fecha ?? _n.G.HoyLocal, Guid.NewGuid(), Notes: notas)
            { OperationKey = Guid.NewGuid() }, default);
        _n.Db.ChangeTracker.Clear();
        return r;
    }

    /// <summary>Publica desde mañana una versión de la línea con seguro de vida deudores y pasa el reloj a mañana.</summary>
    private async Task ConSeguroDeVidaAsync()
    {
        var r = await _n.D.O.L.Versionar().Handle(new AddCreditLineVersionCommand(_n.D.O.Linea.PublicId, OriginacionDePrueba.VersionDeLinea() with
        {
            ValidFrom = _n.G.HoyLocal.AddDays(1), Insurances = [_n.D.O.L.Seguro() with { IsDebtorLifeInsurance = true }],
        }) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _n.Db.ChangeTracker.Clear();
        _n.D.Manana();
    }

    private async Task<DebtorMessage> MensajeDeCobroEnColaAsync()
    {
        var mensaje = new DebtorMessage
        {
            PersonId = _n.D.O.Ana.Id, RecipientRole = ContactRole.Debtor, LoanId = _n.Credito.Id, Purpose = DebtorMessagePurpose.Collection,
            Channel = MessageChannel.Sms, ChannelCode = "SMS", RenderedBody = "Su cuota está vencida.", BodySha256 = new string('a', 64),
            IdempotencyKey = $"prueba:{Guid.NewGuid():N}", OriginKind = "Manual", RequestedAt = _n.G.UtcNow, RequestedByKind = ActorKind.Process,
        };
        _n.Db.DebtorMessages.Add(mensaje);
        await _n.Db.SaveChangesAsync();
        _n.Db.DebtorMessageEvents.Add(new DebtorMessageEvent
        {
            DebtorMessageId = mensaje.Id, Sequence = 1, Kind = DebtorMessageEventKind.Queued, OccurredAt = _n.G.UtcNow, ActorKind = ActorKind.Process,
        });
        await _n.Db.SaveChangesAsync();
        _n.Db.ChangeTracker.Clear();
        return mensaje;
    }

    [Fact]
    public async Task El_fallecimiento_bloquea_abre_la_reclamacion_y_cancela_los_mensajes_de_cobro_en_cola()
    {
        await ConSeguroDeVidaAsync();
        var mensaje = await MensajeDeCobroEnColaAsync();

        var r = await RegistrarAsync(DebtorBlockReason.Deceased);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Kind.Should().Be(DebtorBlockReason.Deceased);
        var bloqueo = await _n.Db.DebtorBlocks.AsNoTracking().SingleAsync();
        (bloqueo.Reason, bloqueo.PersonId, bloqueo.Vigente).Should().Be((DebtorBlockReason.Deceased, _n.D.O.Ana.Id, true));
        var reclamacion = await _n.Db.InsuranceClaims.AsNoTracking().SingleAsync();
        (reclamacion.LoanId, reclamacion.Status, reclamacion.InsurerPersonId, reclamacion.DeceasedPersonId)
            .Should().Be((_n.Credito.Id, InsuranceClaimStatus.InProcess, _n.D.O.L.Aseguradora.Id, _n.D.O.Ana.Id));
        var ultimo = await _n.Db.DebtorMessageEvents.AsNoTracking().Where(e => e.DebtorMessageId == mensaje.Id).OrderByDescending(e => e.Sequence).FirstAsync();
        ultimo.Kind.Should().Be(DebtorMessageEventKind.Cancelled);
        r.Value.Effects.Should().Contain(e => e.Contains("Reclamación al seguro abierta"));
    }

    [Fact]
    public async Task El_mismo_evento_vigente_no_se_registra_dos_veces()
    {
        (await RegistrarAsync(DebtorBlockReason.AssociateWithdrawal)).IsSuccess.Should().BeTrue();

        var otra = await RegistrarAsync(DebtorBlockReason.AssociateWithdrawal);

        otra.IsFailure.Should().BeTrue();
        otra.Error.Code.Should().Be(ErroresDeEventosDelDeudor.CodigoEventoDuplicado);
    }

    [Fact]
    public async Task El_retiro_y_el_embargo_bloquean_el_cupo_y_se_listan_como_eventos()
    {
        (await RegistrarAsync(DebtorBlockReason.AssociateWithdrawal)).IsSuccess.Should().BeTrue();
        (await RegistrarAsync(DebtorBlockReason.Garnishment, notas: "Oficio del juzgado 12")).IsSuccess.Should().BeTrue();

        (await _n.D.O.BloqueosReales().VigentesAsync([_n.D.O.Ana.Id], default)).Select(b => b.Reason)
            .Should().BeEquivalentTo([DebtorBlockReason.AssociateWithdrawal, DebtorBlockReason.Garnishment]);
        var lista = await _n.Eventos().Handle(new ListDebtorEventsQuery(_n.D.O.Ana.PublicId), default);
        lista.Value.Items.Should().HaveCount(2);
        lista.Value.Items.Single(e => e.Kind == DebtorBlockReason.AssociateWithdrawal).Effects.Should().Contain(e => e.Contains("proceso de retiro"));
    }

    [Fact]
    public async Task La_reclamacion_se_niega_o_se_desiste_solo_en_tramite()
    {
        await ConSeguroDeVidaAsync();
        (await RegistrarAsync(DebtorBlockReason.Deceased)).IsSuccess.Should().BeTrue();
        var reclamacion = await _n.Db.InsuranceClaims.AsNoTracking().SingleAsync();

        var negada = await _n.CambiarReclamacion().Handle(new ChangeInsuranceClaimStatusCommand(reclamacion.PublicId, InsuranceClaimStatus.Denied,
            "La aseguradora objetó la reclamación") { OperationKey = Guid.NewGuid() }, default);
        negada.IsSuccess.Should().BeTrue(negada.IsFailure ? negada.Error.Message : string.Empty);
        negada.Value.Status.Should().Be(InsuranceClaimStatus.Denied);
        _n.Db.ChangeTracker.Clear();

        var otraVez = await _n.CambiarReclamacion().Handle(new ChangeInsuranceClaimStatusCommand(reclamacion.PublicId, InsuranceClaimStatus.Withdrawn,
            "Desistimos") { OperationKey = Guid.NewGuid() }, default);
        otraVez.IsFailure.Should().BeTrue();
        otraVez.Error.Code.Should().Be(ErroresDeEventosDelDeudor.CodigoReclamacionNoEnTramite);

        var lista = await _n.Reclamaciones().Handle(new ListInsuranceClaimsQuery(LoanPublicId: _n.Credito.PublicId), default);
        lista.Value.Items.Should().ContainSingle(c => c.Status == InsuranceClaimStatus.Denied);
    }

    [Fact]
    public void Pagada_la_pone_solo_el_contrato()
    {
        var v = new ChangeInsuranceClaimStatusCommandValidator().Validate(new ChangeInsuranceClaimStatusCommand(Guid.NewGuid(), InsuranceClaimStatus.Paid, "Pagó"));
        v.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task La_insolvencia_abre_en_negociacion_bloquea_suspende_la_causacion_y_fracasa_solo_negociando()
    {
        var vencimiento = await _n.PrimerVencimientoAsync();
        _n.G.ActuarComo(_n.D.O.Asesora);
        var inicio = _n.G.HoyLocal.AddDays(10);
        _n.P.C.Ir(inicio);
        var abierto = await _n.AbrirInsolvencia().Handle(new OpenInsolvencyProceedingCommand(_n.D.O.Ana.PublicId, inicio, InsolvencyRegime.NaturalPersonDebtNegotiation,
            "Centro de conciliación", "2026-77", Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);
        abierto.IsSuccess.Should().BeTrue(abierto.IsFailure ? abierto.Error.Message : string.Empty);
        abierto.Value.Status.Should().Be(InsolvencyProceedingStatus.Negotiating);
        _n.Db.ChangeTracker.Clear();
        (await _n.D.O.BloqueosReales().VigentesAsync([_n.D.O.Ana.Id], default)).Should().ContainSingle(b => b.Reason == DebtorBlockReason.Insolvency);

        var repetido = await _n.AbrirInsolvencia().Handle(new OpenInsolvencyProceedingCommand(_n.D.O.Ana.PublicId, inicio,
            InsolvencyRegime.NaturalPersonDebtNegotiation, "Juzgado", null, Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);
        repetido.Error.Code.Should().Be(ErroresDeEventosDelDeudor.CodigoInsolvenciaAbierta);

        // La reconstrucción lee el proceso (T0886): sin interés corriente ni mora desde el inicio.
        await _n.P.CausarHastaAsync(vencimiento.AddDays(30));
        var cargado = await _n.P.C.Cargador().CargarAsync(_n.Credito.Id, null, _n.G.HoyLocal, default);
        var estado = Domain.Lending.MotorDeCartera.Reconstruir(cargado!.Entrada!, _n.G.HoyLocal);
        estado.Suspendida.Should().BeTrue("la negociación de insolvencia suspende la causación");
        await _n.P.ElLibroCuadraAsync();

        var fracaso = await _n.Fracaso().Handle(new RecordInsolvencyFailureCommand(abierto.Value.PublicId, _n.G.HoyLocal, "No hubo acuerdo", Guid.NewGuid())
            { OperationKey = Guid.NewGuid() }, default);
        fracaso.IsSuccess.Should().BeTrue(fracaso.IsFailure ? fracaso.Error.Message : string.Empty);
        (fracaso.Value.Status, fracaso.Value.FailedOn).Should().Be((InsolvencyProceedingStatus.Failed, (DateOnly?)_n.G.HoyLocal));
        _n.Db.ChangeTracker.Clear();
        // Bloque B2: terminada la negociación, el bloqueo de crédito nuevo se levanta en la misma transacción.
        (await _n.D.O.BloqueosReales().VigentesAsync([_n.D.O.Ana.Id], default)).Should().NotContain(b => b.Reason == DebtorBlockReason.Insolvency);
        var levantado = await _n.Db.DebtorBlocks.AsNoTracking().SingleAsync(b => b.Reason == DebtorBlockReason.Insolvency);
        (levantado.LiftedOn, levantado.LiftReason).Should().Be(((DateOnly?)_n.G.HoyLocal, "La negociación de insolvencia fracasó el "
            + $"{_n.G.HoyLocal:yyyy-MM-dd}: No hubo acuerdo"));

        var otraVez = await _n.Fracaso().Handle(new RecordInsolvencyFailureCommand(abierto.Value.PublicId, _n.G.HoyLocal, "Otra vez", null)
            { OperationKey = Guid.NewGuid() }, default);
        otraVez.Error.Code.Should().Be(ErroresDeEventosDelDeudor.CodigoInsolvenciaNoNegociando);
        (await _n.Procesos().Handle(new ListInsolvencyProceedingsQuery(PersonPublicId: _n.D.O.Ana.PublicId), default)).Value.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Lo_migrado_sin_regimen_se_completa_al_registrar_el_fracaso()
    {
        _n.Db.InsolvencyProceedings.Add(new InsolvencyProceeding { PersonId = _n.D.O.Ana.Id, StartDate = _n.G.HoyLocal, Status = InsolvencyProceedingStatus.Negotiating });
        await _n.Db.SaveChangesAsync();
        var migrado = await _n.Db.InsolvencyProceedings.AsNoTracking().SingleAsync();
        _n.Db.ChangeTracker.Clear();

        var sinRegimen = await _n.Fracaso().Handle(new RecordInsolvencyFailureCommand(migrado.PublicId, _n.G.HoyLocal, "No hubo acuerdo", null)
            { OperationKey = Guid.NewGuid() }, default);
        sinRegimen.IsFailure.Should().BeTrue("C64: lo migrado se completa al tramitarlo");

        var completo = await _n.Fracaso().Handle(new RecordInsolvencyFailureCommand(migrado.PublicId, _n.G.HoyLocal, "No hubo acuerdo", null,
            InsolvencyRegime.BusinessReorganization, "Superintendencia de Sociedades") { OperationKey = Guid.NewGuid() }, default);
        completo.IsSuccess.Should().BeTrue(completo.IsFailure ? completo.Error.Message : string.Empty);
        (completo.Value.Regime, completo.Value.Authority).Should().Be(((InsolvencyRegime?)InsolvencyRegime.BusinessReorganization, "Superintendencia de Sociedades"));
    }

    [Fact]
    public async Task Tras_el_fallecimiento_la_causacion_sigue_salvo_que_el_parametro_diga_otra_cosa()
    {
        // FR-097, T102: Causacion.ContinuaTrasFallecimiento (defecto true). Con true el crédito causa como siempre; con false, desde el día del
        // fallecimiento causa en suspenso (motivo Fallecimiento, CausacionSuspendida con cause = Deceased).
        var vencimiento = await _n.PrimerVencimientoAsync();
        var fallecimiento = _n.G.HoyLocal.AddDays(5);
        _n.P.C.Ir(fallecimiento.AddDays(1));
        (await RegistrarAsync(DebtorBlockReason.Deceased, fallecimiento)).IsSuccess.Should().BeTrue();
        await _n.P.CausarHastaAsync(vencimiento.AddDays(10));

        var sigue = await _n.P.C.Cargador().CargarAsync(_n.Credito.Id, null, _n.G.HoyLocal, default);
        sigue!.Entrada!.Condiciones.FallecimientoDesde.Should().BeNull("el defecto es que la causación sigue");
        Domain.Lending.MotorDeCartera.Reconstruir(sigue.Entrada!, _n.G.HoyLocal).Suspendida.Should().BeFalse();

        _n.Db.ParameterVersions.Add(new Domain.Entities.Parameters.ParameterVersion
        {
            Module = Domain.Lending.Parameters.ParametrosDeCartera.Modulo, Key = Domain.Lending.Parameters.ParametrosDeCartera.CausacionContinuaTrasFallecimiento,
            ScopeKind = Domain.Enums.Parameters.ParameterScopeKind.None, Value = "false", ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
            CreatedAt = _n.G.UtcNow.AddDays(-30), CreatedBy = "test",
        });
        await _n.Db.SaveChangesAsync();
        _n.Db.ChangeTracker.Clear();

        var noSigue = await _n.P.C.Cargador().CargarAsync(_n.Credito.Id, null, _n.G.HoyLocal, default);
        noSigue!.Entrada!.Condiciones.FallecimientoDesde.Should().Be(fallecimiento);
        var estado = Domain.Lending.MotorDeCartera.Reconstruir(noSigue.Entrada!, _n.G.HoyLocal);
        estado.Suspendida.Should().BeTrue();
        estado.MotivoDeSuspension.Should().Be(Domain.Lending.Accrual.MotivoDeSuspension.Fallecimiento);
        Domain.Lending.MotorDeCartera.Reconstruir(noSigue.Entrada!, fallecimiento.AddDays(-1)).Suspendida.Should().BeFalse("antes del fallecimiento causa normal");
    }

    [Fact]
    public async Task La_reclamacion_en_tramite_se_consulta_por_credito_para_suspender_el_reporte_negativo()
    {
        // FR-097: mientras la reclamación está en trámite no hay reporte negativo; la proyección a centrales de K5 (T1003) lo consulta aquí.
        await ConSeguroDeVidaAsync();
        (await ReclamacionesEnTramite.CreditosAsync(_n.Db, [_n.Credito.Id], default)).Should().BeEmpty();
        (await RegistrarAsync(DebtorBlockReason.Deceased)).IsSuccess.Should().BeTrue();

        (await ReclamacionesEnTramite.CreditosAsync(_n.Db, [_n.Credito.Id], default)).Should().Equal(_n.Credito.Id);
        (await ReclamacionesEnTramite.HayAsync(_n.Db, _n.D.O.Ana.Id, null, default)).Should().BeTrue();
        var reclamacion = await _n.Db.InsuranceClaims.AsNoTracking().SingleAsync();
        _n.G.ActuarComo(_n.D.O.Asesora);
        (await _n.CambiarReclamacion().Handle(new ChangeInsuranceClaimStatusCommand(reclamacion.PublicId, InsuranceClaimStatus.Denied, "La aseguradora la negó")
            { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();
        _n.Db.ChangeTracker.Clear();
        (await ReclamacionesEnTramite.CreditosAsync(_n.Db, [_n.Credito.Id], default)).Should().BeEmpty("negada, la suspensión se levanta");
    }
}
