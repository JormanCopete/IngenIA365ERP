using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Scoring;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 013, T0369 (FR-035, FR-036, data-model §2.1): radicar. Sin un documento obligatorio que casa por monto, tipo de solicitante, forma de pago
/// y parte → <c>Lending.Application.DocumentsMissing</c> con <c>missing[{ code, name }]</c>; sin autorización <c>CreditProcessing</c> de solicitante
/// o codeudor → <c>Lending.Authorization.Missing</c> con <c>{ personPublicId, purpose, howToFix }</c>; usuario sin persona →
/// <c>Lending.Security.UserWithoutPerson</c>. Si pasa: el radicado por <c>NumeradorDeCartera</c> al final del cerrojo, las validaciones de FR-036
/// para solicitante y codeudores, el puntaje, el enrutamiento a <c>PendingDecision</c> con su huella y la intervención <c>Radicar</c>, en una
/// transacción. La preferencia de abono queda sellada en la solicitud: su fila nace con el crédito o el cupo (CK de alcance).
/// </summary>
public sealed class SubmitCreditApplicationCommandHandlerTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync() => _o = await OriginacionDePrueba.CrearAsync(version: OriginacionDePrueba.VersionDeLinea() with
    {
        Documents =
        [
            new DocumentRequirementInput("CEDULA", "Copia de cédula"),
            new DocumentRequirementInput("CEDCOD", "Cédula del codeudor", AppliesTo: CreditPartyRole.CoDebtor),
            new DocumentRequirementInput("LIBRANZA", "Autorización de descuento", PaymentForm: RepaymentChannel.OwnPayroll),
            new DocumentRequirementInput("AVALUO", "Avalúo", AmountFrom: 20_000_000m),
            new DocumentRequirementInput("REFER", "Referencias", Mandatory: false),
        ],
    });

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private async Task<CreditApplicationDto> ConDocumentos(CreditApplicationDto s)
    {
        foreach (var (codigo, persona) in new[] { ("CEDULA", (Domain.Entities.Core.Person?)null), ("CEDCOD", _o.Beto) })
        {
            if (!s.Requirements.Any(r => r.Code == codigo)) continue;
            if (codigo == "CEDCOD" && s.Parties.All(p => p.Role != CreditPartyRole.CoDebtor)) continue;
            var r = await _o.Adjuntar().Handle(new AttachCreditApplicationDocumentCommand(s.PublicId, codigo, _o.Adjunto(s.PublicId).PublicId,
                OriginacionDePrueba.Hoy, persona?.PublicId) { OperationKey = Guid.NewGuid() }, default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        }
        _o.Db.ChangeTracker.Clear();
        return s;
    }

    private Task<IngenIA365ERP.Application.Common.Models.Result<CreditApplicationDto>> Radicar(CreditApplicationDto s) =>
        _o.Radicar().Handle(_o.Radicacion(s), default);

    [Fact]
    public async Task Sin_un_documento_obligatorio_que_casa_no_pasa_a_analisis_y_dice_cuales_faltan()
    {
        var s = await _o.BorradorAsync(_o.Solicitud(3_000_000m, 24, null, _o.Beto));

        var r = await Radicar(s);

        r.Error.Code.Should().Be("Lending.Application.DocumentsMissing");
        var faltan = OriginacionDePrueba.Datos(r.Error).GetProperty("missing").EnumerateArray().ToList();
        faltan.Select(f => f.GetProperty("code").GetString()).Should().BeEquivalentTo(["CEDULA", "CEDCOD"],
            "ni la libranza (otra forma de pago), ni el avalúo (otro monto), ni las referencias (opcionales)");
        faltan.Single(f => f.GetProperty("code").GetString() == "CEDCOD").GetProperty("personPublicId").GetGuid().Should().Be(_o.Beto.PublicId);
        faltan.Should().OnlyContain(f => !string.IsNullOrWhiteSpace(f.GetProperty("name").GetString()));
        (await _o.Db.CreditApplications.AsNoTracking().SingleAsync()).Status.Should().Be(CreditApplicationStatus.Draft);
    }

    [Fact]
    public async Task Un_documento_viejo_o_rechazado_no_cuenta()
    {
        var s = await _o.BorradorAsync();
        await ConDocumentos(s);
        var documento = await _o.Db.CreditApplicationDocuments.SingleAsync();
        documento.RejectionReason = "Ilegible";
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();

        (await Radicar(s)).Error.Code.Should().Be("Lending.Application.DocumentsMissing");
    }

    [Fact]
    public async Task Sin_autorizacion_para_el_estudio_del_credito_de_un_codeudor_es_Authorization_Missing()
    {
        var s = await ConDocumentos(await _o.BorradorAsync(_o.Solicitud(3_000_000m, 24, null, _o.Beto)));
        _o.QuitarAutorizacion(_o.Beto, DataAuthorizationPurpose.CreditProcessing);

        var r = await Radicar(s);

        r.Error.Code.Should().Be("Lending.Authorization.Missing");
        var datos = OriginacionDePrueba.Datos(r.Error);
        datos.GetProperty("personPublicId").GetGuid().Should().Be(_o.Beto.PublicId);
        datos.GetProperty("purpose").GetString().Should().Be("CreditProcessing");
        datos.GetProperty("howToFix").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Un_usuario_sin_persona_no_radica()
    {
        var s = await ConDocumentos(await _o.BorradorAsync());
        _o.G.ActuarComo(_o.SinPersona);

        (await Radicar(s)).Error.Code.Should().Be("Lending.Security.UserWithoutPerson");
    }

    [Fact]
    public async Task Radicar_numera_valida_puntua_enruta_y_registra_la_intervencion()
    {
        var puntaje = await _o.L.Puntuar().Handle(new SetCreditLineScoringCommand(_o.Linea.PublicId, OriginacionDePrueba.Hoy, "Modelo de puntaje", 600m,
            "EXCEP",
            [
                new ScoringVariableDto("ANTIG", "Antigüedad", FuentesDePuntaje.AntiguedadComoAsociado, 0.5m,
                    [new ScoringRangeDto(0m, 12m, 300m), new ScoringRangeDto(12m, 36m, 600m), new ScoringRangeDto(36m, null, 900m)]),
                new ScoringVariableDto("RELCI", "Cuota/ingreso", FuentesDePuntaje.CapacidadDePago, 0.5m,
                    [new ScoringRangeDto(0m, 0.3m, 900m), new ScoringRangeDto(0.3m, 0.5m, 600m), new ScoringRangeDto(0.5m, null, 200m)]),
            ]) { OperationKey = Guid.NewGuid() }, default);
        puntaje.IsSuccess.Should().BeTrue(puntaje.IsFailure ? puntaje.Error.Message : string.Empty);
        _o.Db.ChangeTracker.Clear();
        var s = await ConDocumentos(await _o.BorradorAsync(_o.Solicitud(3_000_000m, 24, null, _o.Beto)));

        var r = await Radicar(s);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var dto = r.Value;
        dto.Number.Should().Be("SOL-000001");
        dto.Status.Should().Be(CreditApplicationStatus.PendingDecision);
        dto.RoutingSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        dto.ConsolidatedExposure.Should().Be(3_000_000m);
        dto.SubmittedAt.Should().NotBeNull();
        dto.PrepaymentPreference.Should().Be(PrepaymentChoice.ReduceTerm);

        var fila = await _o.Db.CreditApplications.AsNoTracking().SingleAsync();
        fila.SubmittedByUserId.Should().Be(_o.Asesora.Id);
        var nivel = await _o.Db.AttributionLevels.AsNoTracking().SingleAsync(n => n.Id == fila.RequiredLevelId);
        nivel.Code.Should().Be("ANALISTA", "3.000.000 sin otras deudas van al analista");
        fila.AttributionMatrixId.Should().Be(nivel.MatrixId);

        // Validaciones de FR-036 para solicitante y codeudor, con su evidencia.
        var validaciones = await _o.Db.CreditApplicationChecks.AsNoTracking().Where(c => c.ApplicationId == fila.Id).ToListAsync();
        var partes = await _o.Db.CreditApplicationParties.AsNoTracking().Where(p => p.ApplicationId == fila.Id).ToListAsync();
        validaciones.Should().OnlyContain(c => c.Stage == CheckStage.AtSubmission && c.EvidenceSha256.Length == 64);
        foreach (var parte in partes)
        {
            validaciones.Should().Contain(c => c.PartyId == parte.Id && c.Kind == ApplicationCheckKind.RestrictiveLists && c.ScreeningResultId != null);
            validaciones.Should().Contain(c => c.PartyId == parte.Id && c.Kind == ApplicationCheckKind.PaymentCapacity && c.Outcome == CheckOutcome.Passed);
            validaciones.Should().Contain(c => c.PartyId == parte.Id && c.Kind == ApplicationCheckKind.DataAuthorization && c.Outcome == CheckOutcome.Passed);
        }
        validaciones.Select(c => c.Kind).Distinct().Should().Contain(Enum.GetValues<ApplicationCheckKind>(), "las trece clases quedan registradas");
        (await _o.Db.ScreeningResults.CountAsync()).Should().Be(2);

        // El puntaje: antigüedad 35 meses (600 × 0,5) y cuota/ingreso bajo 0,3 (900 × 0,5) = 750, sobre el mínimo.
        var calculado = await _o.Db.CreditScores.AsNoTracking().SingleAsync();
        calculado.Score.Should().Be(750m);
        calculado.BelowMinimum.Should().BeFalse();
        validaciones.Should().ContainSingle(c => c.Kind == ApplicationCheckKind.Scoring && c.CreditScoreId == calculado.Id);
        dto.Score!.Value.Should().Be(750m);

        // La intervención Radicar del expediente y el cerrojo de los dos deudores.
        var intervencion = await _o.Db.CaseParticipations.AsNoTracking().SingleAsync();
        intervencion.Action.Should().Be(AccionesDeSegregacion.Radicar);
        intervencion.CaseRootPublicId.Should().Be(s.PublicId);
        await _o.Cerrojo.Received(1).TomarSetupAsync(false, Arg.Any<CancellationToken>());
        await _o.Cerrojo.Received(1).BloquearDeudoresAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.Order().SequenceEqual(new[] { _o.Ana.Id, _o.Beto.Id }.Order())), ModoDeEspera.Esperar,
            Arg.Any<CancellationToken>());

        // La preferencia queda en la solicitud; su fila nace con el crédito (CK_LND_PrepaymentPreferences_Scope exige crédito o cupo).
        (await _o.Db.PrepaymentPreferences.CountAsync()).Should().Be(0);
        fila.PrepaymentChoice.Should().Be(PrepaymentChoice.ReduceTerm);
    }

    [Fact]
    public async Task El_enrutamiento_que_ve_el_decisor_es_el_sellado()
    {
        var s = await _o.RadicadaAsync(_o.Solicitud(15_000_000m, 36));

        var r = await _o.Enrutado().Handle(new GetCreditApplicationRoutingQuery(s.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.EffectiveLevelCode.Should().Be("GERENCIA");
        r.Value.ContentSha256.Should().Be(s.RoutingSha256, "sin cambios, la huella vigente es la sellada al radicar");
        r.Value.ConsolidatedAmount.Should().Be(15_000_000m);
        r.Value.NonExceptionable.Should().BeEmpty();

        // Una deuda nueva del solicitante cambia el consolidado y la huella: decidir tiene que reenrutar.
        _o.Credito(_o.Ana, 8_000_000m, "CR-0001");
        var despues = await _o.Enrutado().Handle(new GetCreditApplicationRoutingQuery(s.PublicId), default);
        despues.Value.ConsolidatedAmount.Should().Be(23_000_000m);
        despues.Value.EffectiveLevelCode.Should().Be("COMITE");
        despues.Value.ContentSha256.Should().NotBe(s.RoutingSha256);
    }

    [Fact]
    public async Task Sin_ingreso_declarado_la_capacidad_queda_pendiente_del_analisis()
    {
        var s = await _o.RadicadaAsync(_o.Solicitud() with { ApplicantCapacity = null });

        s.Checks.Should().Contain(c => c.Kind == ApplicationCheckKind.PaymentCapacity && c.Outcome == CheckOutcome.Unavailable);
        _o.G.ActuarComo(_o.Analista);
        var analizada = await _o.Analizar().Handle(new RecordCreditApplicationAnalysisCommand(s.PublicId, 6_000_000m, 2_000_000m, 0m)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        analizada.IsSuccess.Should().BeTrue(analizada.IsFailure ? analizada.Error.Message : string.Empty);
        analizada.Value.Checks.Should().Contain(c => c.Kind == ApplicationCheckKind.PaymentCapacity && c.Stage == CheckStage.BeforeDecision
                                                    && c.Outcome == CheckOutcome.Passed);
        (await _o.Db.CaseParticipations.AsNoTracking().Select(c => c.Action).ToListAsync())
            .Should().BeEquivalentTo([AccionesDeSegregacion.Radicar, AccionesDeSegregacion.Analizar]);
    }

    [Fact]
    public async Task La_concentracion_excedida_niega_la_radicacion()
    {
        var s = await ConDocumentos(await _o.BorradorAsync());
        _o.PatrimonioTecnico = _o.PatrimonioTecnico! with { Value = 20_000_000m };

        var r = await Radicar(s);

        r.Error.Code.Should().Be("Lending.Concentration.LimitExceeded");
        var datos = OriginacionDePrueba.Datos(r.Error);
        datos.GetProperty("limit").GetDecimal().Should().Be(2_000_000m);
        datos.GetProperty("excess").GetDecimal().Should().Be(1_000_000m);
        datos.GetProperty("scope").GetString().Should().Be("Debtor");
    }

    [Fact]
    public async Task Sin_patrimonio_tecnico_vigente_niega_y_alerta_al_contador()
    {
        var s = await ConDocumentos(await _o.BorradorAsync());
        _o.PatrimonioTecnico = null;

        var r = await Radicar(s);

        r.Error.Code.Should().Be("Lending.Concentration.TechnicalCapitalUnavailable");
        _o.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.PatrimonioTecnicoNoDisponible);
    }

    [Fact]
    public async Task Una_usura_nueva_por_debajo_de_la_tasa_niega_la_radicacion()
    {
        var s = await ConDocumentos(await _o.BorradorAsync(_o.Solicitud() with { ExpectedDisbursementDate = new DateOnly(2027, 1, 5) }));
        await _o.L.UsuraAsync(LineasDePrueba.ClaveConsumo, 0.18m, new DateOnly(2027, 1, 1));

        (await Radicar(s)).Error.Code.Should().Be("Lending.Usury.Exceeded");
    }

    [Fact]
    public async Task Solo_se_radica_un_borrador_o_una_devuelta()
    {
        var s = await _o.RadicadaAsync();

        var otraVez = await Radicar(s);
        otraVez.Error.Code.Should().Be("Lending.Application.InvalidStatus");

        var fila = await _o.Db.CreditApplications.SingleAsync();
        fila.Devolver();
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
        var reenviada = await Radicar(s);
        reenviada.IsSuccess.Should().BeTrue(reenviada.IsFailure ? reenviada.Error.Message : string.Empty);
        reenviada.Value.Number.Should().Be("SOL-000001", "el radicado no cambia al reenviar");
    }
}
