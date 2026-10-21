using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Origination.CreditLimits;
using IngenIA365ERP.Application.Lending.Sessions;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ResultadoDeLaLista = IngenIA365ERP.Domain.Enums.Compliance.ScreeningOutcome;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0377 (api.md §7.1, FR-049, FR-058, T34): los cupos. El disponible (cupo − saldos vivos − reservas vigentes − aprobadas sin
/// desembolsar) se calcula al leer; asignar toma el cerrojo del deudor; dentro de la atribución operativa y sin otro nivel en la matriz rige de una
/// vez (<c>Active</c>); si no, <c>PendingDecision</c> con su enrutamiento y sin error; concentración y patrimonio técnico como en la radicación; un no
/// asociado sin línea que lo admita no tiene cupo; una coincidencia en listas deja el cupo bloqueado y lo avisa; el rotativo sin línea vale para toda
/// línea de origen comercial (C54) y registra la preferencia; cruces, terminar, revalidar y la preferencia del rotativo; un vinculado sin decisión del
/// consejo queda «por revalidar».
/// </summary>
public sealed class CreditLimitCommandsTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync(activada: false);
        _o.G.ActuarComo(_o.Asesora);
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private static AssignCreditLimitCommand Cupo(Domain.Entities.Core.Person p, decimal monto, CreditLimitKind clase = CreditLimitKind.Consolidated,
        Guid? linea = null, PrepaymentChoice? preferencia = null, DateOnly? desde = null, DateOnly? hasta = null,
        CreditLimitOrigin origen = CreditLimitOrigin.Assigned) =>
        new(p.PublicId, clase, linea, origen, origen == CreditLimitOrigin.Assigned ? monto : null, desde ?? OriginacionDePrueba.Hoy, hasta, preferencia,
            "Cupo aprobado por el comité de crédito") { OperationKey = Guid.NewGuid() };

    private async Task<Result<CreditLimitDto>> AsignarAsync(AssignCreditLimitCommand c, AssignCreditLimitCommandHandler? handler = null)
    {
        var r = await (handler ?? _o.Asignar()).Handle(c, default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<CreditLimitDto> AsignadoAsync(AssignCreditLimitCommand c)
    {
        var r = await AsignarAsync(c);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<CreditLimitAvailabilityDto> DisponibleAsync(Domain.Entities.Core.Person p, Guid? linea = null, DateOnly? fecha = null)
    {
        var r = await new GetCreditLimitAvailabilityQueryHandler(_o.Db, _o.Disponible(), _o.BloqueosReales(), _o.G.Alcance, _o.G.Auditoria, _o.G.Reloj)
            .Handle(new GetCreditLimitAvailabilityQuery(p.PublicId, linea, fecha), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    private async Task<int> LineaIdAsync(Guid linea) => await _o.Db.CreditLines.Where(l => l.PublicId == linea).Select(l => l.Id).SingleAsync();

    private async Task<CreditLineDto> LineaComercialAsync() =>
        await _o.L.LineaAsync("ROTAT", OriginacionDePrueba.VersionDeLinea() with
        {
            AllowsCommercialOrigin = true, InterestTaxConcept = new InterestTaxConceptInput(false, Guid.NewGuid()),
        });

    // ------------------------------------------------------------------------------------------------------ disponible --

    [Fact]
    public async Task El_disponible_resta_saldos_vivos_reservas_y_aprobadas_y_no_se_guarda()
    {
        await AsignadoAsync(Cupo(_o.Ana, 10_000_000m));
        _o.Credito(_o.Ana, 2_000_000m, "CR-2");
        var linea = await LineaIdAsync(_o.Linea.PublicId);
        _o.Db.LoanNumberReservations.Add(new LoanNumberReservation
        {
            Number = "R-1", PersonId = _o.Ana.Id, Amount = 1_000_000m, IdempotencyKey = "INV:1", RequestSha256 = new string('c', 64),
            SalesDocumentPublicId = Guid.NewGuid(), PaymentPublicId = Guid.NewGuid(), Status = LoanNumberReservationStatus.Reserved,
            ReservedAt = _o.G.UtcNow, ReservedByUserId = _o.Asesora.Id, ExpiresOn = OriginacionDePrueba.Hoy.AddDays(5),
        });
        _o.Db.CreditApplications.Add(new CreditApplication
        {
            ApplicantPersonId = _o.Ana.Id, BranchId = _o.Norte.Id, CreditLineId = linea, CreditLineVersionId = 1, RequestedAmount = 3_000_000m,
            ApprovedAmount = 3_000_000m, Status = CreditApplicationStatus.Approved, ApprovalValidUntil = OriginacionDePrueba.Hoy.AddDays(10),
            CreatedByUserId = _o.Asesora.Id,
        });
        await _o.Db.SaveChangesAsync();
        var cuposAntes = await _o.Db.CreditLimits.CountAsync();

        var d = await DisponibleAsync(_o.Ana);

        d.Consolidated.Should().BeEquivalentTo(new { Limit = 10_000_000m, Used = 2_000_000m, Reserved = 1_000_000m, ApprovedNotDisbursed = 3_000_000m, Available = 4_000_000m });
        d.Status.Should().Be(EstadosDeCupo.Activo);
        (await _o.Db.CreditLimits.CountAsync()).Should().Be(cuposAntes, "el disponible se calcula, nunca se guarda");
    }

    [Fact]
    public async Task El_rotativo_sin_linea_vale_para_toda_linea_de_origen_comercial_y_registra_la_preferencia()
    {
        var comercial = await LineaComercialAsync();
        var cupo = await AsignadoAsync(Cupo(_o.Ana, 2_000_000m, CreditLimitKind.Revolving, preferencia: PrepaymentChoice.ReduceInstallment));

        cupo.Status.Should().Be(EstadosDeCupo.Activo);
        cupo.PrepaymentPreference.Should().Be(PrepaymentChoice.ReduceInstallment);
        var preferencia = await _o.Db.PrepaymentPreferences.SingleAsync();
        preferencia.Origin.Should().Be(PreferenceOrigin.CreditLimitGrant);
        preferencia.CreditLimitId.Should().NotBeNull();
        preferencia.LoanId.Should().BeNull();

        var enComercial = await _o.Disponible().CalcularAsync(_o.Ana.Id, await LineaIdAsync(comercial.PublicId), OriginacionDePrueba.Hoy, default);
        var enConsumo = await _o.Disponible().CalcularAsync(_o.Ana.Id, await LineaIdAsync(_o.Linea.PublicId), OriginacionDePrueba.Hoy, default);
        enComercial.TieneCupo.Should().BeTrue();
        enComercial.Disponible.Should().Be(2_000_000m);
        enComercial.CreditLimitPublicId.Should().Be(cupo.PublicId);
        enConsumo.TieneCupo.Should().BeFalse("la línea de consumo no admite origen comercial (C54)");
    }

    // --------------------------------------------------------------------------------------------------------- asignar --

    [Fact]
    public async Task Dentro_de_la_atribucion_operativa_rige_y_por_encima_espera_decision_sin_error()
    {
        _o.MontoOperativo(_o.Asesora, 1_000_000m);

        var chico = await AsignadoAsync(Cupo(_o.Ana, 500_000m, CreditLimitKind.Revolving, preferencia: PrepaymentChoice.ReduceTerm));
        var grande = await AsignadoAsync(Cupo(_o.Beto, 3_000_000m));

        chico.Status.Should().Be(EstadosDeCupo.Activo);
        chico.Routing.Should().BeNull();
        grande.Status.Should().Be(EstadosDeCupo.PendienteDeDecision);
        grande.Routing!.LevelCode.Should().Be("ANALISTA");
        grande.Routing.ContentSha256.Should().HaveLength(64);
        (await _o.Db.CaseParticipations.SingleAsync()).Should().Match<Domain.Entities.Lending.Governance.CaseParticipation>(c =>
            c.CaseRootPublicId == grande.PublicId && c.Action == AccionesDeSegregacion.Radicar, "quien pide el cupo no lo decide");
        (await DisponibleAsync(_o.Beto)).Consolidated.Should().BeNull("un cupo que espera decisión no rige");
    }

    [Fact]
    public async Task Un_vinculado_va_al_consejo_aunque_quepa_en_la_atribucion()
    {
        await RegistrarVinculoAsync(_o.Dario);

        var cupo = await AsignadoAsync(Cupo(_o.Dario, 500_000m));

        cupo.Status.Should().Be(EstadosDeCupo.PendienteDeDecision);
        cupo.Routing!.LevelCode.Should().Be("CONSEJO");
        cupo.Routing.RelatedParty.Should().BeTrue();
    }

    [Fact]
    public async Task Asignar_toma_el_cerrojo_del_deudor()
    {
        _o.Cerrojo.ClearReceivedCalls();

        await AsignadoAsync(Cupo(_o.Ana, 1_000_000m));

        await _o.Cerrojo.Received().BloquearDeudoresAsync(Arg.Is<IReadOnlyCollection<int>>(x => x.Single() == _o.Ana.Id), Arg.Any<ModoDeEspera>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_concentracion_excedida_y_sin_patrimonio_tecnico_no_se_otorga()
    {
        var excedida = await AsignarAsync(Cupo(_o.Ana, 150_000_000m));
        excedida.Error.Code.Should().Be("Lending.Concentration.LimitExceeded");
        OriginacionDePrueba.Datos(excedida.Error).GetProperty("limit").GetDecimal().Should().Be(100_000_000m);

        _o.PatrimonioTecnico = null;
        var sinBase = await AsignarAsync(Cupo(_o.Ana, 1_000_000m));
        sinBase.Error.Code.Should().Be("Lending.Concentration.TechnicalCapitalUnavailable");
        _o.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.PatrimonioTecnicoNoDisponible);
        (await _o.Db.CreditLimits.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Un_no_asociado_sin_linea_que_lo_admita_no_tiene_cupo()
    {
        var r = await AsignarAsync(Cupo(_o.Carla, 1_000_000m));

        r.Error.Code.Should().Be("Lending.CreditLine.NonAssociatesNotAllowed");
    }

    [Fact]
    public async Task Con_un_bloqueo_vigente_no_se_otorga()
    {
        _o.Db.DebtorBlocks.Add(DebtorBlock.Nuevo(_o.Ana.Id, DebtorBlockReason.Garnishment, Guid.NewGuid().ToString(), OriginacionDePrueba.Hoy, "Embargo judicial", 1));
        await _o.Db.SaveChangesAsync();

        (await AsignarAsync(Cupo(_o.Ana, 1_000_000m))).Error.Code.Should().Be("Lending.Debtor.Blocked");
    }

    [Fact]
    public async Task Una_coincidencia_en_listas_deja_el_cupo_bloqueado_y_lo_avisa()
    {
        var listas = new ListasDePrueba();
        listas.PorDocumento[_o.Dario.TaxId!] = ResultadoDeLaLista.Match;

        var limpio = await AsignadoAsync(Cupo(_o.Ana, 1_000_000m));
        var r = await AsignarAsync(Cupo(_o.Dario, 2_000_000m, CreditLimitKind.Revolving, preferencia: PrepaymentChoice.ReduceTerm),
            _o.Asignar(listas.ConsultaReal(_o)));

        limpio.Warnings.Should().BeEmpty();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var aviso = r.Value.Warnings.Should().ContainSingle().Subject;
        aviso.Code.Should().Be("Lending.Screening.Blocked");
        var resultado = await _o.Db.ScreeningResults.SingleAsync(x => x.PersonId == _o.Dario.Id);
        aviso.ScreeningResultPublicId.Should().Be(resultado.PublicId);
        resultado.Context.Should().Be(ScreeningContext.CreditLimit);
        resultado.CreditLimitId.Should().Be((await _o.Db.CreditLimits.SingleAsync(c => c.PublicId == r.Value.PublicId)).Id);
        r.Value.Status.Should().Be(EstadosDeCupo.Bloqueado);
        r.Value.BlockReason.Should().Be(DebtorBlockReason.RestrictiveListMatch);
    }

    [Fact]
    public async Task No_se_cruza_con_una_version_pendiente_ni_con_una_posterior()
    {
        _o.MontoOperativo(_o.Asesora, 1_000_000m);
        await AsignadoAsync(Cupo(_o.Beto, 3_000_000m));
        (await AsignarAsync(Cupo(_o.Beto, 500_000m))).Error.Code.Should().Be("Lending.CreditLimit.Overlaps");

        await AsignadoAsync(Cupo(_o.Ana, 500_000m, desde: OriginacionDePrueba.Hoy.AddDays(10)));
        var r = await AsignarAsync(Cupo(_o.Ana, 600_000m, desde: OriginacionDePrueba.Hoy));
        r.Error.Code.Should().Be("Lending.CreditLimit.Overlaps");
        OriginacionDePrueba.Datos(r.Error).GetProperty("reason").GetString().Should().Be("Later");
    }

    [Fact]
    public async Task El_cupo_calculado_sale_del_saldo_de_aportes_por_la_relacion_de_la_linea()
    {
        var linea = await _o.L.LineaAsync("APORTES", OriginacionDePrueba.VersionDeLinea() with { CreditLimit = new CreditLimitInput(ContributionsMultiple: 3m) });
        _o.Db.ContributionBalanceLoads.Add(Domain.Entities.Lending.Configuration.ContributionBalanceLoad.Registrar(_o.Ana.Id, new DateOnly(2026, 11, 30),
            1_500_000m, 0m, Guid.NewGuid(), _o.G.UtcNow.AddDays(-1), _o.Asesora.Id, "Reporte de aportes"));
        await _o.Db.SaveChangesAsync();

        var cupo = await AsignadoAsync(Cupo(_o.Ana, 0m, CreditLimitKind.ByLine, linea.PublicId, origen: CreditLimitOrigin.Calculated));

        cupo.Amount.Should().Be(4_500_000m);
        cupo.Origin.Should().Be(CreditLimitOrigin.Calculated);
        (await _o.Db.CreditLimits.SingleAsync()).ContributionBalanceLoadId.Should().NotBeNull();
        (await AsignarAsync(Cupo(_o.Beto, 0m, CreditLimitKind.ByLine, linea.PublicId, origen: CreditLimitOrigin.Calculated)))
            .Error.Code.Should().Be("Lending.CreditLimit.CalculationUnavailable", "Beto no tiene saldo de aportes cargado");
    }

    // ------------------------------------------------------------------------------------ terminar, preferencia, lista --

    [Fact]
    public async Task Terminar_deja_una_version_en_cero_y_una_que_no_rige_no_se_termina()
    {
        _o.MontoOperativo(_o.Asesora, 1_000_000m);
        var pendiente = await AsignadoAsync(Cupo(_o.Beto, 3_000_000m));
        var activo = await AsignadoAsync(Cupo(_o.Ana, 800_000m));
        var terminar = new EndCreditLimitCommandHandler(_o.Db, _o.Cerrojo, _o.G.Alcance, _o.G.Auditoria, _o.G.Actor, _o.G.Reloj);

        (await terminar.Handle(new EndCreditLimitCommand(pendiente.PublicId, OriginacionDePrueba.Hoy, "Retiro") { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be("Lending.CreditLimit.NotActive");
        var fin = await terminar.Handle(new EndCreditLimitCommand(activo.PublicId, OriginacionDePrueba.Hoy.AddDays(5), "Retiro voluntario")
            { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();

        fin.IsSuccess.Should().BeTrue(fin.IsFailure ? fin.Error.Message : string.Empty);
        fin.Value.Amount.Should().Be(0m);
        fin.Value.ValidFrom.Should().Be(OriginacionDePrueba.Hoy.AddDays(6));
        (await DisponibleAsync(_o.Ana)).Consolidated!.Limit.Should().Be(800_000m, "rige hasta el día de la terminación");
        (await DisponibleAsync(_o.Ana, fecha: OriginacionDePrueba.Hoy.AddDays(6))).Consolidated.Should().BeNull();
        var leido = await ConsultasDeCupos.DtoAsync(_o.Db, await _o.Db.CreditLimits.AsNoTracking().SingleAsync(c => c.PublicId == activo.PublicId),
            OriginacionDePrueba.Hoy, default);
        leido.ValidTo.Should().Be(OriginacionDePrueba.Hoy.AddDays(5));
    }

    [Fact]
    public async Task La_preferencia_se_cambia_solo_en_el_cupo_rotativo()
    {
        var consolidado = await AsignadoAsync(Cupo(_o.Ana, 800_000m));
        var rotativo = await AsignadoAsync(Cupo(_o.Beto, 800_000m, CreditLimitKind.Revolving, preferencia: PrepaymentChoice.ReduceTerm));
        var cambiar = new ChangePrepaymentPreferenceCommandHandler(_o.Db, _o.G.Alcance, _o.G.Auditoria, _o.G.Actor, _o.G.Reloj);

        (await cambiar.Handle(new ChangePrepaymentPreferenceCommand(consolidado.PublicId, PrepaymentChoice.ReduceInstallment) { OperationKey = Guid.NewGuid() },
            default)).Error.Code.Should().Be("Lending.CreditLimit.NotRevolving");
        _o.G.UtcNow = _o.G.UtcNow.AddMinutes(5);
        var r = await cambiar.Handle(new ChangePrepaymentPreferenceCommand(rotativo.PublicId, PrepaymentChoice.ReduceInstallment, "Lo pidió el deudor")
            { OperationKey = Guid.NewGuid() }, default);

        r.Value.PrepaymentPreference.Should().Be(PrepaymentChoice.ReduceInstallment);
        (await _o.Db.PrepaymentPreferences.OrderByDescending(p => p.VersionNumber).FirstAsync()).Should().Match<PrepaymentPreference>(p =>
            p.VersionNumber == 2 && p.Origin == PreferenceOrigin.DebtorRequest);
    }

    [Fact]
    public async Task La_lista_muestra_lo_que_rige_y_lo_pendiente_con_su_estado_y_filtra_por_la_linea_del_alcance()
    {
        _o.MontoOperativo(_o.Asesora, 1_000_000m);
        var otra = await _o.L.LineaAsync("OTRA", OriginacionDePrueba.VersionDeLinea());
        await AsignadoAsync(Cupo(_o.Ana, 800_000m));
        await AsignadoAsync(Cupo(_o.Beto, 3_000_000m));
        await AsignadoAsync(Cupo(_o.Ana, 700_000m, CreditLimitKind.ByLine, otra.PublicId));
        var listar = new ListCreditLimitsQueryHandler(_o.Db, _o.G.Alcance, _o.G.Reloj);

        (await listar.Handle(new ListCreditLimitsQuery(), default)).Value.TotalCount.Should().Be(3);
        (await listar.Handle(new ListCreditLimitsQuery(Status: EstadosDeCupo.PendienteDeDecision), default)).Value.Items
            .Should().ContainSingle().Which.Person.PersonPublicId.Should().Be(_o.Beto.PublicId);

        _o.G.AlcanceActual = new AlcanceDeCartera(true, new HashSet<int>(), null, false, new HashSet<int> { await LineaIdAsync(_o.Linea.PublicId) });
        (await listar.Handle(new ListCreditLimitsQuery(), default)).Value.Items.Should().OnlyContain(c => c.Line == null,
            "el cupo de una línea fuera del alcance no aparece");
    }

    // ------------------------------------------------------------------------------------------------- revalidación --

    [Fact]
    public async Task Un_vinculado_sin_decision_del_consejo_queda_por_revalidar_y_se_revalida_al_dejar_de_serlo()
    {
        var cupo = await AsignadoAsync(Cupo(_o.Beto, 800_000m));

        var vinculo = await RegistrarVinculoAsync(_o.Beto);

        var bloqueo = await _o.Db.DebtorBlocks.SingleAsync();
        bloqueo.Reason.Should().Be(DebtorBlockReason.LimitRevalidation);
        bloqueo.SourceKey.Should().Be(cupo.PublicId.ToString());
        _o.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.CupoPorRevalidar);
        (await DisponibleAsync(_o.Beto)).Status.Should().Be(EstadosDeCupo.Bloqueado);

        var revalidar = new RevalidateCreditLimitCommandHandler(_o.Db, _o.Revalidacion(), _o.G.Alcance, _o.G.Auditoria, _o.G.Actor, _o.G.Reloj);
        var falta = await revalidar.Handle(new RevalidateCreditLimitCommand(cupo.PublicId, "Revisión") { OperationKey = Guid.NewGuid() }, default);
        falta.Error.Code.Should().Be("Lending.Attribution.LevelRequired");

        (await new EndRelatedPartyCommandHandler(_o.Db, _o.G.Actor, _o.Revalidacion()).Handle(new EndRelatedPartyCommand(vinculo, OriginacionDePrueba.Hoy.AddDays(-1),
            "Dejó el consejo") { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();
        _o.Db.ChangeTracker.Clear();
        var ok = await revalidar.Handle(new RevalidateCreditLimitCommand(cupo.PublicId, "Ya no es vinculado") { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();

        ok.IsSuccess.Should().BeTrue(ok.IsFailure ? ok.Error.Message : string.Empty);
        ok.Value.Status.Should().Be(EstadosDeCupo.Activo);
        (await _o.Db.DebtorBlocks.SingleAsync()).LiftedAt.Should().NotBeNull();
    }

    private async Task<Guid> RegistrarVinculoAsync(Domain.Entities.Core.Person p)
    {
        var alta = new AltaDeVinculados(_o.Db, new ReglasDeVinculados(_o.G.Valores, _o.G.Perfil), _o.G.Actor, _o.G.Reloj);
        var r = await new RegisterRelatedPartyCommandHandler(_o.Db, alta, _o.Revalidacion()).Handle(new RegisterRelatedPartyCommand(p.PublicId,
            RelatedPartyBasis.BoardMember, new DateOnly(2026, 3, 1), null, null, null, Support: "Acta de asamblea 45") { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        _o.Db.ChangeTracker.Clear();
        return r.Value.PublicId;
    }
}
