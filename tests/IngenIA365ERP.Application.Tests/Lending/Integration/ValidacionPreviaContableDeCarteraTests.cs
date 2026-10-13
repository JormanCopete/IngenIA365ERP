using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, US7, T0626 (contabilidad.md §13.1–§13.3; FR-127, T56, enmienda A8, C80): la validación previa contable de Cartera sobre un doble
/// de <see cref="IContabilidadParaCartera"/>. Por origen —pantalla, contrato, consumidor de Comercial, procesos y cobros provisionales—, con
/// hallazgos y sin respuesta (excepción o tiempo agotado): sólo en pantalla <c>Contabilidad.FaltaDeRegla = Bloquear</c> responde 422
/// <c>Lending.Prevalidation.NotPostable</c> con <c>data.errors[] { factType, role, combination, account, rule, whoFixes }</c> y
/// <c>PoliticaSinRespuesta = Bloquear</c> 422 <c>Lending.Prevalidation.NoResponse</c>; lo demás advierte y sella. <c>FaltaDeRegla</c> gobierna
/// todo hallazgo; una pregunta por combinación; los <c>NoPasa</c>, informativos y otros destinos no se preguntan; la reserva de número no se
/// evalúa; alerta <c>Cartera.HechoNoContabilizable</c> por combinación y día.
/// </summary>
public class ValidacionPreviaContableDeCarteraTests
{
    private static readonly DateOnly Hoy = new(2026, 12, 14);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IContabilidadParaCartera _contabilidad = Substitute.For<IContabilidadParaCartera>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();
    private readonly List<AlertaALevantar> _alertasLevantadas = [];
    private readonly List<IReadOnlyList<MensajeContableDto>> _preguntas = [];

    private static readonly Guid Suc1 = Guid.NewGuid();
    private static readonly Guid Suc2 = Guid.NewGuid();

    private static readonly HallazgoDeCarteraDto ReglaFaltante = new(CreditoDesembolsadoV1.Type, "Capital",
        new Dictionary<string, string> { ["modalityCode"] = "CONSUMO", ["riskCategory"] = "A" }, null, "Accounting.LendingRule.Missing",
        "No hay regla para Capital de CONSUMO en A.", new QuienCorrigeDto("Contabilidad", "/contabilidad/cartera/matriz", "Accounting.LendingRules.Manage"));

    private static readonly HallazgoDeCarteraDto CuentaInactiva = new(CreditoDesembolsadoV1.Type, "Capital", null, "14410501",
        "Accounting.Line.AccountInactive", "La cuenta 14410501 está inactiva.", new QuienCorrigeDto("Contabilidad", "/contabilidad/plan-de-cuentas", "Accounting.Accounts.Manage"));

    public ValidacionPreviaContableDeCarteraTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, null)));
        _alertas.LevantarAsync(Arg.Do<AlertaALevantar>(_alertasLevantadas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 1, false)));
        _db.LendingFactTypes.AddRange(
            new LendingFactType { Code = CreditoDesembolsadoV1.Type, ShortCode = "DES", Name = "Desembolso", DefaultMode = "EnLinea", CreatedBy = "test" },
            new LendingFactType { Code = CreditoAprobadoSinDesembolsarV1.Type, ShortCode = "CAD", Name = "Aprobado", DefaultMode = "NoPasa", CreatedBy = "test" });
        _db.SaveChanges();
    }

    private void Responde(params HallazgoDeCarteraDto[] hallazgos) =>
        _contabilidad.EvaluarAsync(Arg.Do<IReadOnlyList<MensajeContableDto>>(_preguntas.Add), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ResultadoDeEvaluacionContableDto(hallazgos.Length == 0, hallazgos, [])));

    private void Politica(string clave, string valor)
    {
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, Value = valor, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba",
            ScopeKind = ParameterScopeKind.None, CreatedBy = "test",
        });
        _db.SaveChanges();
    }

    private ValidacionPreviaContableDeCartera Validacion()
    {
        var lector = new LectorDeParametros(_db);
        return new ValidacionPreviaContableDeCartera(_contabilidad, lector, _reloj, null, new ModoDePasoDeCartera(_db, _contabilidad, lector, _reloj),
            _alertas, _db);
    }

    private static MensajeContableDto Hecho(string tipo = CreditoDesembolsadoV1.Type, Guid? sucursal = null, decimal monto = 1_000_000m,
        string modalidad = "CONSUMO") =>
        new(new IntegrationEnvelopeV1
            {
                MessageId = Guid.Empty, Type = tipo, Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
                BranchPublicId = sucursal ?? Suc1, Origin = new MessageOriginV1 { OperationDate = Hoy },
            },
            new CreditoDesembolsadoV1
            {
                Loan = new LoanRefV1 { LoanPublicId = Guid.NewGuid(), LoanNumber = "PG" + monto },
                Dimensions = new LoanFactDimensionsV1 { CreditLineCode = "CONS1", ModalityCode = modalidad, RiskCategory = "A", BranchPublicId = sucursal ?? Suc1 },
                Amounts = [new ConceptAmountV1 { Concept = "Principal", Amount = monto }],
            });

    // --------------------------------------------------------------------------------------------- por origen --

    [Fact]
    public async Task En_pantalla_con_FaltaDeRegla_Bloquear_responde_NotPostable_con_los_hallazgos_y_no_alerta()
    {
        Responde(ReglaFaltante);
        Politica(ParametrosDeCartera.ContabilidadFaltaDeRegla, "Bloquear");

        var d = await Validacion().DecidirAsync([Hecho()], OrigenDeValidacionPrevia.Pantalla, "SOL-1", default);

        d.Bloquea.Should().BeTrue();
        d.Bloqueo!.Code.Should().Be("Lending.Prevalidation.NotPostable");
        d.Bloqueo.Message.Should().Contain("contadora").And.NotContain("Accounting.");
        var errores = JsonSerializer.SerializeToElement(((ErrorConDatos)d.Bloqueo).Data, new JsonSerializerOptions(JsonSerializerDefaults.Web)).GetProperty("errors");
        var e = errores.EnumerateArray().Single();
        e.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("factType", "role", "combination", "account", "rule", "whoFixes");
        e.GetProperty("rule").GetString().Should().Be("Accounting.LendingRule.Missing");
        e.GetProperty("whoFixes").GetProperty("page").GetString().Should().Be("/contabilidad/cartera/matriz");
        _alertasLevantadas.Should().BeEmpty("lo que se bloquea no se confirma: no hay hecho que corregir");
    }

    [Fact]
    public async Task En_pantalla_con_Advertir_confirma_sella_NotPostable_y_alerta()
    {
        Responde(ReglaFaltante);

        var d = await Validacion().DecidirAsync([Hecho()], OrigenDeValidacionPrevia.Pantalla, "SOL-1", default);

        d.Bloquea.Should().BeFalse();
        d.Outcome.Should().Be(PrevalidationOutcome.NotPostable);
        d.Avisos.Should().ContainSingle(a => a.Contains("contadora"));
        _alertasLevantadas.Should().ContainSingle().Which.TypeCode.Should().Be(TiposDeAlerta.HechoNoContabilizable);
    }

    [Theory]
    [InlineData(OrigenDeValidacionPrevia.Contrato)]
    [InlineData(OrigenDeValidacionPrevia.ConsumidorDeComercial)]
    [InlineData(OrigenDeValidacionPrevia.Proceso)]
    [InlineData(OrigenDeValidacionPrevia.CobroProvisional)]
    public async Task Fuera_de_las_pantallas_nunca_bloquea_aunque_la_politica_diga_Bloquear(OrigenDeValidacionPrevia origen)
    {
        Responde(ReglaFaltante);
        Politica(ParametrosDeCartera.ContabilidadFaltaDeRegla, "Bloquear");
        Politica(ParametrosDeCartera.ContabilidadPoliticaSinRespuesta, "Bloquear");

        var d = await Validacion().DecidirAsync([Hecho()], origen, "PG-7", default);

        d.Bloquea.Should().BeFalse("enmienda A8: el contrato, el consumidor y los procesos siempre advierten");
        d.Outcome.Should().Be(PrevalidationOutcome.NotPostable);
        _alertasLevantadas.Should().ContainSingle();

        _contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TimeoutException("caída"));
        var sinRespuesta = await Validacion().DecidirAsync([Hecho()], origen, "PG-7", default);
        sinRespuesta.Bloquea.Should().BeFalse();
        sinRespuesta.Outcome.Should().Be(PrevalidationOutcome.NoResponse);
    }

    [Fact]
    public async Task FaltaDeRegla_gobierna_todo_hallazgo_incluida_la_cuenta_inactiva()
    {
        Responde(CuentaInactiva);
        Politica(ParametrosDeCartera.ContabilidadFaltaDeRegla, "Bloquear");

        var d = await Validacion().DecidirAsync([Hecho()], OrigenDeValidacionPrevia.Pantalla, "SOL-1", default);

        d.Bloqueo!.Code.Should().Be("Lending.Prevalidation.NotPostable");
    }

    [Fact]
    public async Task FaltaDeRegla_por_tipo_de_hecho_tambien_bloquea()
    {
        Responde(ReglaFaltante);
        _db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.ContabilidadFaltaDeRegla, Value = "Bloquear", ValidFrom = new DateOnly(2026, 1, 1),
            Reason = "prueba", ScopeKind = ParameterScopeKind.LendingFactType, ScopeId = _db.LendingFactTypes.Single(f => f.Code == CreditoDesembolsadoV1.Type).Id,
            CreatedBy = "test",
        });
        await _db.SaveChangesAsync();

        (await Validacion().DecidirAsync([Hecho()], OrigenDeValidacionPrevia.Pantalla, "SOL-1", default)).Bloquea.Should().BeTrue();
    }

    // --------------------------------------------------------------------------------------------- sin respuesta --

    [Fact]
    public async Task Sin_respuesta_en_pantalla_sigue_con_NoResponse_y_con_la_politica_Bloquear_responde_422()
    {
        _contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("caída"));

        var sigue = await Validacion().DecidirAsync([Hecho()], OrigenDeValidacionPrevia.Pantalla, "SOL-1", default);
        sigue.Bloquea.Should().BeFalse("ConfirmarConPendiente es el defecto (D7)");
        sigue.Outcome.Should().Be(PrevalidationOutcome.NoResponse);
        sigue.Avisos.Should().ContainSingle(a => a.Contains("no respondió"));

        Politica(ParametrosDeCartera.ContabilidadPoliticaSinRespuesta, "Bloquear");
        var bloquea = await Validacion().DecidirAsync([Hecho()], OrigenDeValidacionPrevia.Pantalla, "SOL-1", default);
        bloquea.Bloqueo!.Code.Should().Be("Lending.Prevalidation.NoResponse");
    }

    [Fact]
    public async Task El_tiempo_agotado_es_sin_respuesta()
    {
        Politica(ParametrosDeCartera.ContabilidadValidacionPreviaSegundos, "1");
        _contabilidad.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ci.Arg<CancellationToken>());
                return Result.Success(new ResultadoDeEvaluacionContableDto(true, [], []));
            });

        var r = await Validacion().EvaluarAsync([Hecho()], default);

        r.Outcome.Should().Be(PrevalidationOutcome.NoResponse);
    }

    // ------------------------------------------------------------------------------------------- qué se pregunta --

    [Fact]
    public async Task Se_reduce_a_una_pregunta_por_combinacion()
    {
        Responde();
        var quinientos = Enumerable.Range(1, 500).Select(i => Hecho(monto: i * 1000m)).ToList();
        quinientos.Add(Hecho(sucursal: Suc2));
        quinientos.Add(Hecho(modalidad: "COMERCIAL"));

        var r = await Validacion().EvaluarAsync(quinientos, default);

        r.Outcome.Should().Be(PrevalidationOutcome.Postable);
        _preguntas.Should().ContainSingle().Which.Should().HaveCount(3, "una por combinación de hecho, sucursal y dimensiones; los importes no cuentan");
    }

    [Fact]
    public async Task Los_hechos_NoPasa_los_informativos_y_los_de_otros_destinos_no_se_preguntan()
    {
        Responde(ReglaFaltante);

        var r = await Validacion().EvaluarAsync(
            [Hecho(CreditoAprobadoSinDesembolsarV1.Type), Hecho(CarteraActivadaV1.Type), Hecho(OrdenDeGiroEmitidaV1.Type)], default);

        r.Outcome.Should().Be(PrevalidationOutcome.NotApplicable);
        _preguntas.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_nada_pasa_y_no_se_pregunta()
    {
        Responde(ReglaFaltante);
        _contabilidad.EstadoDelPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null)));

        (await Validacion().EvaluarAsync([Hecho()], default)).Outcome.Should().Be(PrevalidationOutcome.NotApplicable);
        _preguntas.Should().BeEmpty();
    }

    [Fact]
    public void La_reserva_de_numero_no_se_evalua()
    {
        typeof(ReservasDeCartera).GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Should().NotContain(typeof(ValidacionPreviaContableDeCartera), "corre dentro de la confirmación de Comercial y no produce hecho (T22)");
    }

    // ----------------------------------------------------------------------------------------------------- alerta --

    [Fact]
    public async Task La_alerta_es_una_por_combinacion_y_dia_para_la_matriz_y_el_reproceso()
    {
        var otra = ReglaFaltante with { Combination = new Dictionary<string, string> { ["modalityCode"] = "COMERCIAL", ["riskCategory"] = "A" } };
        Responde(ReglaFaltante, ReglaFaltante, otra);

        await Validacion().DecidirAsync([Hecho()], OrigenDeValidacionPrevia.Contrato, "el lote de Payroll", default);

        _alertasLevantadas.Should().HaveCount(2);
        _alertasLevantadas.Select(a => a.DedupKey).Should().BeEquivalentTo(
            "Cartera.HechoNoContabilizable:CreditoDesembolsado:modalityCode=CONSUMO;riskCategory=A:2026-12-14",
            "Cartera.HechoNoContabilizable:CreditoDesembolsado:modalityCode=COMERCIAL;riskCategory=A:2026-12-14");
        _alertasLevantadas.Should().OnlyContain(a => a.RecipientPermissions!.SequenceEqual(new[] { "Accounting.LendingRules.Manage", "Lending.Messages.Reprocess" }));
    }
}
