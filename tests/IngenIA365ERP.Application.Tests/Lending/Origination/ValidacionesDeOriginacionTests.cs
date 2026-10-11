using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using Microsoft.EntityFrameworkCore;
using ScreeningOutcome = IngenIA365ERP.Domain.Enums.Lending.ScreeningOutcome;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0370 (FR-036, FR-026, FR-041, FR-110; data-model §2.4): las validaciones automáticas. Una fila por clase, parte y etapa con su
/// huella de evidencia; listas, capacidad de pago, autorización y usura nunca se exceptúan; centrales en las líneas que las exigen (sin consulta,
/// con una simulada en producción, con el canal caído); capacidad de descuento en nómina propia contra el límite nacional; endeudamiento consolidado
/// como deudor y codeudor; cupo; concentración sobre el patrimonio técnico vigente (sin él, bloquea y alerta al contador); requisitos de la línea
/// (antigüedad, saldo de aportes con la fecha de la carga, codeudores por la regla de garantías); vinculado; bloqueo vigente; las advertencias son
/// alertas para el aprobador.
/// </summary>
public sealed class ValidacionesDeOriginacionTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync() => _o = await OriginacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>Arma la solicitud, la carga y corre las validaciones en <c>BeforeDecision</c> (sin registrarlas).</summary>
    private async Task<(ValidacionesEvaluadas V, SolicitudCargada C)> Evaluar(CreditApplicationInput entrada)
    {
        var dto = await _o.BorradorAsync(entrada);
        await _o.DocumentosAsync(dto);
        var s = await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == dto.PublicId);
        var c = await _o.Evaluacion().CargarAsync(s, default);
        c.IsSuccess.Should().BeTrue(c.IsFailure ? c.Error.Message : string.Empty);
        var v = await _o.Validaciones().EvaluarAsync(
            new ContextoDeValidacion(s, c.Value.Partes, c.Value.Personas, c.Value.Linea, c.Value.Simulacion, CheckStage.BeforeDecision, OriginacionDePrueba.Hoy),
            default);
        return (v, c.Value);
    }

    private static ValidacionPendiente De(ValidacionesEvaluadas v, ApplicationCheckKind clase, int? parte = null) =>
        v.Pendientes.Where(p => p.Clase == clase && (parte is null || p.PartyId == parte)).Should().ContainSingle().Subject;

    private static JsonElement Detalle(ValidacionPendiente p) => JsonSerializer.SerializeToElement(p.Detalle, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private async Task<CreditLineDto> Linea(string codigo, Func<CreditLineVersionInput, CreditLineVersionInput> cambiar) =>
        await _o.L.LineaAsync(codigo, cambiar(OriginacionDePrueba.VersionDeLinea()));

    [Fact]
    public async Task Cada_clase_queda_una_vez_por_parte_y_etapa_con_su_evidencia()
    {
        var (v, c) = await Evaluar(_o.Solicitud(3_000_000m, 24, null, _o.Beto));
        var s = c.Solicitud;

        _o.Db.ScreeningResults.Local.Should().HaveCount(2, "una consulta de listas por parte");
        await _o.Db.SaveChangesAsync();
        var filas = _o.Validaciones().Registrar(v, s.Id, CheckStage.BeforeDecision, _o.G.UtcNow, null);
        await _o.Db.SaveChangesAsync();

        filas.GroupBy(f => (f.Kind, f.PartyId, f.Stage)).Should().OnlyContain(g => g.Count() == 1);
        filas.Should().OnlyContain(f => f.EvidenceSha256.Length == 64 && f.Stage == CheckStage.BeforeDecision);
        filas.Where(f => f.Kind == ApplicationCheckKind.RestrictiveLists).Should().HaveCount(2).And.OnlyContain(f => f.ScreeningResultId != null);
        filas.Where(f => f.Kind == ApplicationCheckKind.PaymentCapacity).Should().HaveCount(2);
        filas.Single(f => f.Kind == ApplicationCheckKind.Usury).PartyId.Should().BeNull("la usura es de la solicitud");
        v.Bloquea.Should().BeFalse();
        v.Hallazgos.Should().BeEmpty();
        v.MontoConsolidado.Should().Be(3_000_000m);
    }

    [Fact]
    public async Task Una_coincidencia_en_listas_bloquea_y_no_se_exceptua()
    {
        _o.ListasDe[_o.Beto.Id] = ScreeningOutcome.Match;
        _o.ListasDe[_o.Ana.Id] = ScreeningOutcome.Unavailable;

        var (v, c) = await Evaluar(_o.Solicitud(3_000_000m, 24, null, _o.Beto));

        var beto = De(v, ApplicationCheckKind.RestrictiveLists, c.Partes.Single(p => p.PersonId == _o.Beto.Id).Id);
        beto.Resultado.Should().Be(CheckOutcome.Blocking);
        beto.Exceptuable.Should().BeFalse();
        beto.Codigo.Should().Be("Compliance.RestrictiveList.Match");
        beto.Listas!.Outcome.Should().Be(ScreeningOutcome.Match);
        var ana = De(v, ApplicationCheckKind.RestrictiveLists, c.Partes.Single(p => p.PersonId == _o.Ana.Id).Id);
        ana.Resultado.Should().Be(CheckOutcome.Unavailable, "no cuenta como consulta: bloquea hasta repetirla");
        v.Hallazgos.Should().Equal(MotivoNoExceptuable.RestrictiveListMatch);
    }

    [Fact]
    public async Task Las_centrales_en_una_linea_que_las_exige()
    {
        var linea = await Linea("CENTRAL", v => v with { RequiresBureauQuery = true });
        var entrada = _o.Solicitud() with { CreditLinePublicId = linea.PublicId };

        var (sin, _) = await Evaluar(entrada);
        De(sin, ApplicationCheckKind.CreditBureau).Codigo.Should().Be("Lending.Bureau.QueryRequired");
        De(sin, ApplicationCheckKind.CreditBureau).Resultado.Should().Be(CheckOutcome.Blocking);

        Consulta(ExternalOutcome.ChannelUnavailable, "TRANSUNION", simulada: false);
        var (caida, _) = await Evaluar(entrada);
        De(caida, ApplicationCheckKind.CreditBureau).Resultado.Should().Be(CheckOutcome.Unavailable);

        Consulta(ExternalOutcome.Ok, "SIMULADO", simulada: true);
        _o.Opciones.AmbienteDePruebas = false;
        var (simulada, _) = await Evaluar(entrada);
        De(simulada, ApplicationCheckKind.CreditBureau).Codigo.Should().Be("Lending.Adapter.SimulatedInProduction");

        _o.Opciones.AmbienteDePruebas = true;
        var (pruebas, _) = await Evaluar(entrada);
        De(pruebas, ApplicationCheckKind.CreditBureau).Resultado.Should().Be(CheckOutcome.Passed, "fuera de producción el simulado sí vale");
        De(pruebas, ApplicationCheckKind.CreditBureau).Central.Should().NotBeNull();
    }

    private void Consulta(ExternalOutcome resultado, string canal, bool simulada)
    {
        _o.Db.BureauQueries.Add(new BureauQuery
        {
            PersonId = _o.Ana.Id, Purpose = BureauQueryPurpose.Application, BureauCode = "TRANSUNION", ChannelCode = canal,
            AuthorizationConsentPublicId = Guid.NewGuid(), Outcome = resultado, Score = 700m, IsSimulated = simulada,
            QueriedAt = _o.G.UtcNow.AddMinutes(_o.Db.BureauQueries.Local.Count + 1), QueriedByUserId = _o.Asesora.Id,
            ValidUntil = OriginacionDePrueba.Hoy.AddDays(30),
        });
        _o.Db.SaveChanges();
    }

    [Fact]
    public async Task La_capacidad_de_pago_insuficiente_no_se_exceptua()
    {
        var (v, c) = await Evaluar(_o.Solicitud() with { ApplicantCapacity = new CapacityInput(1_000_000m, 900_000m, 50_000m) });

        var cap = De(v, ApplicationCheckKind.PaymentCapacity, c.Solicitante.Id);
        cap.Resultado.Should().Be(CheckOutcome.Blocking);
        cap.Exceptuable.Should().BeFalse();
        cap.Codigo.Should().Be("Lending.Capacity.PaymentInsufficient");
        var detalle = Detalle(cap);
        detalle.GetProperty("available").GetDecimal().Should().BeNegative();
        detalle.GetProperty("simulatedInstallment").GetDecimal().Should().BeGreaterThan(0m);
        v.Hallazgos.Should().Equal(MotivoNoExceptuable.PaymentCapacity);
    }

    [Fact]
    public async Task La_capacidad_de_descuento_en_nomina_propia_contra_el_limite_nacional()
    {
        var (v, c) = await Evaluar(_o.Solicitud() with
        {
            PaymentForm = RepaymentChannel.OwnPayroll, ApplicantCapacity = new CapacityInput(250_000m, 0m, 0m),
        });

        var descuento = De(v, ApplicationCheckKind.DeductionCapacity, c.Solicitante.Id);
        descuento.Resultado.Should().Be(CheckOutcome.Blocking);
        descuento.Codigo.Should().Be("Lending.Capacity.DeductionInsufficient");
        var detalle = Detalle(descuento);
        detalle.GetProperty("fraction").GetDecimal().Should().Be(0.5m);
        detalle.GetProperty("allowed").GetDecimal().Should().Be(125_000m);

        var (porCaja, c2) = await Evaluar(_o.Solicitud());
        De(porCaja, ApplicationCheckKind.DeductionCapacity, c2.Solicitante.Id).Resultado.Should().Be(CheckOutcome.NotApplicable);
    }

    [Fact]
    public async Task El_endeudamiento_consolidado_suma_como_deudor_y_como_codeudor()
    {
        _o.Credito(_o.Ana, 2_000_000m, "CR-0001");
        _o.Credito(_o.Dario, 1_000_000m, "CR-0002", _o.Ana);

        var (v, c) = await Evaluar(_o.Solicitud());

        var detalle = Detalle(De(v, ApplicationCheckKind.ConsolidatedExposure, c.Solicitante.Id));
        detalle.GetProperty("asDebtor").GetDecimal().Should().Be(2_000_000m);
        detalle.GetProperty("asCoDebtor").GetDecimal().Should().Be(1_000_000m);
        detalle.GetProperty("consolidated").GetDecimal().Should().Be(6_000_000m);
        v.MontoConsolidado.Should().Be(6_000_000m, "lo pedido más lo que debe y lo que respalda: el enrutamiento va por el consolidado");
    }

    [Fact]
    public async Task El_cupo_que_no_alcanza_bloquea_y_se_puede_exceptuar()
    {
        _o.CupoDe[_o.Ana.Id] = new DisponibleDeCupoCalculado(true, 5_000_000m, 4_000_000m, 1_000_000m, Guid.NewGuid());

        var (v, _) = await Evaluar(_o.Solicitud());

        var cupo = De(v, ApplicationCheckKind.CreditLimit);
        cupo.Resultado.Should().Be(CheckOutcome.Blocking);
        cupo.Codigo.Should().Be("Lending.CreditLimit.Insufficient");
        cupo.Exceptuable.Should().BeTrue();
        Detalle(cupo).GetProperty("available").GetDecimal().Should().Be(1_000_000m);
    }

    [Fact]
    public async Task La_concentracion_excedida_lleva_limite_actual_pedido_exceso_y_base()
    {
        _o.Credito(_o.Ana, 1_500_000m, "CR-0001");
        _o.PatrimonioTecnico = _o.PatrimonioTecnico! with { Value = 40_000_000m };

        var (v, _) = await Evaluar(_o.Solicitud());

        var conc = De(v, ApplicationCheckKind.Concentration);
        conc.Resultado.Should().Be(CheckOutcome.Blocking);
        conc.Codigo.Should().Be("Lending.Concentration.LimitExceeded");
        conc.Exceptuable.Should().BeFalse("es un límite legal (FR-041), no una política");
        var deudor = Detalle(conc).GetProperty("debtor");
        deudor.GetProperty("limit").GetDecimal().Should().Be(4_000_000m);
        deudor.GetProperty("current").GetDecimal().Should().Be(1_500_000m);
        deudor.GetProperty("requested").GetDecimal().Should().Be(3_000_000m);
        deudor.GetProperty("excess").GetDecimal().Should().Be(500_000m);
        deudor.GetProperty("base").GetDecimal().Should().Be(40_000_000m);
        v.Concentracion!.Error!.Code.Should().Be("Lending.Concentration.LimitExceeded");
    }

    [Fact]
    public async Task Sin_patrimonio_tecnico_vigente_bloquea_y_alerta_al_contador()
    {
        // Un indicador de hace un año: más viejo que Contabilidad.IndicadoresVigenciaMeses (6) no sirve (C33).
        _o.PatrimonioTecnico = _o.PatrimonioTecnico! with { CutoffDate = new DateOnly(2025, 11, 30) };

        var (v, _) = await Evaluar(_o.Solicitud());

        var conc = De(v, ApplicationCheckKind.Concentration);
        conc.Resultado.Should().Be(CheckOutcome.Blocking);
        conc.Codigo.Should().Be("Lending.Concentration.TechnicalCapitalUnavailable");
        _o.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.PatrimonioTecnicoNoDisponible
                                             && a.Body.Contains("2025-11-30"));
    }

    [Fact]
    public async Task La_usura_del_dia_no_se_exceptua()
    {
        var dto = await _o.BorradorAsync(_o.Solicitud() with { ExpectedDisbursementDate = new DateOnly(2027, 1, 5) });
        await _o.L.UsuraAsync(LineasDePrueba.ClaveConsumo, 0.18m, new DateOnly(2027, 1, 1));
        var s = await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == dto.PublicId);
        var c = (await _o.Evaluacion().CargarAsync(s, default)).Value;

        var v = await _o.Validaciones().EvaluarAsync(new ContextoDeValidacion(s, c.Partes, c.Personas, c.Linea, c.Simulacion, CheckStage.BeforeDecision,
            OriginacionDePrueba.Hoy), default);

        var usura = De(v, ApplicationCheckKind.Usury);
        usura.Resultado.Should().Be(CheckOutcome.Blocking);
        usura.Exceptuable.Should().BeFalse();
        usura.Codigo.Should().Be("Lending.Usury.Exceeded");
        v.Hallazgos.Should().Contain(MotivoNoExceptuable.Usury);
    }

    [Fact]
    public async Task Los_requisitos_de_la_linea_con_la_fecha_de_la_carga_de_aportes()
    {
        var linea = await Linea("REQUIS", v => v with
        {
            Requirements = new RequirementsInput(MinAssociateSeniorityMonths: 48),
            CreditLimit = new CreditLimitInput(MinContributions: 1_000_000m),
        });
        _o.Db.ContributionBalanceLoads.Add(ContributionBalanceLoad.Registrar(_o.Ana.Id, new DateOnly(2026, 11, 30), 500_000m, 0m, Guid.NewGuid(),
            _o.G.UtcNow.AddDays(-1), _o.Asesora.Id));
        await _o.Db.SaveChangesAsync();

        var (v, _) = await Evaluar(_o.Solicitud() with { CreditLinePublicId = linea.PublicId });

        var req = v.Pendientes.Single(p => p.Clase == ApplicationCheckKind.LineRequirements && p.Codigo == "Lending.Application.RequirementsNotMet");
        req.Resultado.Should().Be(CheckOutcome.Blocking);
        var faltas = Detalle(req).GetProperty("unmet").EnumerateArray().ToList();
        faltas.Should().Contain(f => f.GetProperty("requirement").GetString() == "MinAssociateSeniorityMonths" && f.GetProperty("actual").GetInt32() == 35);
        faltas.Should().Contain(f => f.GetProperty("requirement").GetString() == "MinContributionsBalance"
                                     && f.GetProperty("asOfDate").GetString() == "2026-11-30" && f.GetProperty("actual").GetDecimal() == 500_000m);
    }

    [Fact]
    public async Task Menos_codeudores_de_los_que_exige_la_regla_de_garantias_bloquea()
    {
        var linea = await Linea("CODEUD", v => v with { CollateralRules = [new CollateralRuleInput(AmountFrom: 2_000_000m, CoDebtors: 1)] });

        var (sin, _) = await Evaluar(_o.Solicitud() with { CreditLinePublicId = linea.PublicId });
        var falta = sin.Pendientes.Single(p => p.Codigo == "Lending.Collateral.CoDebtorsInsufficient");
        falta.Clase.Should().Be(ApplicationCheckKind.LineRequirements);
        var detalle = Detalle(falta);
        detalle.GetProperty("required").GetInt32().Should().Be(1);
        detalle.GetProperty("present").GetInt32().Should().Be(0);
        detalle.GetProperty("ruleOrder").GetInt32().Should().Be(1);

        var (con, _) = await Evaluar(_o.Solicitud(3_000_000m, 24, null, _o.Beto) with { CreditLinePublicId = linea.PublicId });
        con.Pendientes.Should().NotContain(p => p.Codigo == "Lending.Collateral.CoDebtorsInsufficient");

        var (pequena, _) = await Evaluar(_o.Solicitud(1_000_000m) with { CreditLinePublicId = linea.PublicId });
        pequena.Pendientes.Should().NotContain(p => p.Codigo == "Lending.Collateral.CoDebtorsInsufficient", "la regla casa desde 2.000.000");
    }

    [Fact]
    public async Task Un_vinculado_es_advertencia_y_llega_al_aprobador()
    {
        _o.Db.RelatedParties.Add(new RelatedParty { PersonId = _o.Ana.Id, Basis = RelatedPartyBasis.BoardMember, ValidFrom = new DateOnly(2026, 1, 1) });
        await _o.Db.SaveChangesAsync();

        var (v, _) = await Evaluar(_o.Solicitud());

        De(v, ApplicationCheckKind.RelatedParty).Resultado.Should().Be(CheckOutcome.Warning);
        v.HayVinculado.Should().BeTrue();
        v.ConAlertas.Should().BeTrue();
    }

    [Fact]
    public async Task Un_bloqueo_vigente_de_cualquier_parte_es_Debtor_Blocked()
    {
        _o.BloqueosVigentes.Add(DebtorBlock.Nuevo(_o.Beto.Id, DebtorBlockReason.Garnishment, "oficio-77", new DateOnly(2026, 11, 1),
            "Embargo del juzgado 3", _o.Asesora.Id));

        var (v, c) = await Evaluar(_o.Solicitud(3_000_000m, 24, null, _o.Beto));

        var bloqueo = v.Pendientes.Single(p => p.Codigo == "Lending.Debtor.Blocked");
        bloqueo.PartyId.Should().Be(c.Partes.Single(p => p.PersonId == _o.Beto.Id).Id);
        bloqueo.Resultado.Should().Be(CheckOutcome.Blocking);
        bloqueo.Exceptuable.Should().BeFalse();
    }

    [Fact]
    public async Task Sin_autorizacion_para_el_estudio_no_se_exceptua()
    {
        var dto = await _o.BorradorAsync(_o.Solicitud(3_000_000m, 24, null, _o.Beto));
        _o.QuitarAutorizacion(_o.Beto, IngenIA365ERP.Domain.Enums.Compliance.DataAuthorizationPurpose.CreditProcessing);
        var s = await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == dto.PublicId);
        var c = (await _o.Evaluacion().CargarAsync(s, default)).Value;

        var v = await _o.Validaciones().EvaluarAsync(new ContextoDeValidacion(s, c.Partes, c.Personas, c.Linea, c.Simulacion, CheckStage.BeforeDecision,
            OriginacionDePrueba.Hoy), default);

        var auto = De(v, ApplicationCheckKind.DataAuthorization, c.Partes.Single(p => p.PersonId == _o.Beto.Id).Id);
        auto.Resultado.Should().Be(CheckOutcome.Blocking);
        auto.Exceptuable.Should().BeFalse();
        v.Hallazgos.Should().Equal(MotivoNoExceptuable.HolderAuthorization);
    }

    [Fact]
    public async Task Volver_a_correr_algunas_clases_solo_evalua_esas()
    {
        var dto = await _o.BorradorAsync();
        var s = await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == dto.PublicId);
        var c = (await _o.Evaluacion().CargarAsync(s, default)).Value;

        var v = await _o.Validaciones().EvaluarAsync(new ContextoDeValidacion(s, c.Partes, c.Personas, c.Linea, c.Simulacion, CheckStage.BeforeDecision,
            OriginacionDePrueba.Hoy) { Solo = new HashSet<ApplicationCheckKind> { ApplicationCheckKind.Usury, ApplicationCheckKind.CreditLimit } }, default);

        v.Pendientes.Select(p => p.Clase).Distinct().Should().BeEquivalentTo([ApplicationCheckKind.Usury, ApplicationCheckKind.CreditLimit]);
        _o.Db.ScreeningResults.Local.Should().BeEmpty("sin listas no se consulta");
    }
}
