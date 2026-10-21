using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace IngenIA365ERP.Application.Tests.Lending.Integration;

/// <summary>
/// Feature 013, T0146 (C106; DT §2.9; contracts/contabilidad.md §8, §10.1, §13, §16.1; interfaces.md §4.1, §9.1): las piezas de Cartera que
/// se usan antes de su historia tienen firma estable y comportamiento mínimo. <see cref="IContabilidadParaCartera"/> declara exactamente sus
/// cinco métodos y el provisional responde el estado real del mes, las cuentas bancarias del plan, «no aplica», vacío y «no disponible»;
/// <see cref="ModoDePasoDeCartera"/> da el defecto del tipo de hecho, <c>NoPasa</c> sin contabilidad y <c>Always</c> en informativos;
/// <see cref="ValidacionPreviaContableDeCartera"/> nunca bloquea y sella el resultado; <see cref="FechaContable"/> salta los meses cerrados;
/// <see cref="CategoriaContableDeCartera"/> da la de crédito nuevo; y <see cref="TesoreriaDePrueba"/> consume sus dos mensajes y sólo se
/// registra fuera de producción. InMemory.
/// </summary>
public class PiezasTempranasDeCarteraTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    public PiezasTempranasDeCarteraTests()
    {
        _reloj.HoyLocal.Returns(new DateOnly(2026, 12, 1));
        _reloj.UtcNow.Returns(new DateTime(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc));
    }

    private ContabilidadParaCarteraProvisional Provisional() => new(_db);

    /// <summary>Contabilidad iniciada con el ejercicio 2026: enero y febrero cerrados, el resto abierto; 2027 no existe.</summary>
    private void IniciarContabilidad(params int[] mesesCerrados)
    {
        _db.AccountingSetups.Add(new AccountingSetup { NiifGroup = 2, MovementLevel = 5, FirstFiscalYear = 2026, InitializedBy = "test", CreatedBy = "test" });
        var ejercicio = new FiscalYear { Year = 2026, CreatedBy = "test" };
        _db.FiscalYears.Add(ejercicio);
        for (byte mes = 1; mes <= 12; mes++)
        {
            var inicio = new DateOnly(2026, mes, 1);
            _db.AccountingPeriods.Add(new AccountingPeriod
            {
                FiscalYear = ejercicio, Month = mes, StartDate = inicio, EndDate = inicio.AddMonths(1).AddDays(-1),
                Status = mesesCerrados.Contains(mes) ? PeriodStatus.Closed : PeriodStatus.Open, CreatedBy = "test",
            });
        }
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    // ------------------------------------------------------------------------------------- el puerto de Contabilidad --

    [Fact]
    public void El_puerto_declara_exactamente_sus_cinco_metodos()
    {
        typeof(IContabilidadParaCartera).GetMethods().Select(m => m.Name).Order(StringComparer.Ordinal).Should().Equal(
            "CuentasBancariasAsync", "EstadoDelPeriodoAsync", "EvaluarAsync", "IndicadoresDeLaEntidadAsync", "SaldosDeCuentasMapeadasAsync");
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_el_estado_del_periodo_dice_no_iniciada()
    {
        var r = await Provisional().EstadoDelPeriodoAsync(new DateOnly(2026, 12, 1), default);

        r.Value.Should().Be(new EstadoDelPeriodoDto(false, EstadoDePeriodoContable.NotFound, null));
    }

    [Fact]
    public async Task El_estado_del_periodo_es_el_real_del_mes_y_salta_los_cerrados_pero_no_los_que_no_existen()
    {
        IniciarContabilidad(1, 2);

        var cerrado = (await Provisional().EstadoDelPeriodoAsync(new DateOnly(2026, 1, 15), default)).Value;
        cerrado.Should().Be(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Closed, new DateOnly(2026, 3, 1)));

        var abierto = (await Provisional().EstadoDelPeriodoAsync(new DateOnly(2026, 6, 10), default)).Value;
        abierto.Should().Be(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.Open, new DateOnly(2026, 6, 1)));

        var inexistente = (await Provisional().EstadoDelPeriodoAsync(new DateOnly(2027, 3, 5), default)).Value;
        inexistente.Should().Be(new EstadoDelPeriodoDto(true, EstadoDePeriodoContable.NotFound, new DateOnly(2027, 3, 1)),
            "un mes que no existe no se salta: la fecha contable queda en él (contabilidad.md §8)");
    }

    [Fact]
    public async Task Las_cuentas_bancarias_son_las_del_plan_con_banco_activas()
    {
        var banco = new Bank { Name = "Banco Uno", TransferCode = "1001", CreatedBy = "test" };
        _db.Banks.Add(banco);
        _db.ChartOfAccounts.Add(new ChartOfAccount { Code = "11100501", Name = "Banco Uno corriente", Bank = banco, BankAccountNumber = "123-4", IsActive = true, CreatedBy = "test" });
        _db.ChartOfAccounts.Add(new ChartOfAccount { Code = "11100502", Name = "Banco Uno vieja", Bank = banco, IsActive = false, CreatedBy = "test" });
        _db.ChartOfAccounts.Add(new ChartOfAccount { Code = "11050501", Name = "Caja general", IsActive = true, CreatedBy = "test" });
        await _db.SaveChangesAsync();

        var cuentas = (await Provisional().CuentasBancariasAsync(null, default)).Value;

        cuentas.Should().ContainSingle();
        cuentas[0].Should().BeEquivalentTo(new { AccountCode = "11100501", BankName = "Banco Uno", AccountNumber = "123-4", TransferCode = "1001" });
        (await Provisional().CuentasBancariasAsync(Guid.NewGuid(), default)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Evaluar_no_aplica_los_saldos_vienen_vacios_y_los_indicadores_no_estan_disponibles()
    {
        var evaluacion = await Provisional().EvaluarAsync([Hecho()], default);
        evaluacion.Error.Code.Should().Be(ContabilidadParaCarteraProvisional.CodigoEvaluacionNoDisponible);

        var combinacion = new CombinacionDeSaldo("Capital", "CONSUMO1", "CONSUMO", null, "A", null, null, null, null, null, null, null, Guid.NewGuid());
        var saldos = (await Provisional().SaldosDeCuentasMapeadasAsync(new DateOnly(2026, 11, 30), [combinacion, combinacion], default)).Value;
        saldos.Sets.Should().BeEmpty();
        saldos.UnresolvedCombinations.Should().Equal(0, 1);

        var indicadores = (await Provisional().IndicadoresDeLaEntidadAsync(new DateOnly(2026, 11, 30), default)).Value;
        indicadores.TechnicalCapital.Should().BeNull();
        indicadores.SolvencyRatio.Should().BeNull();
    }

    // -------------------------------------------------------------------------------------------- modo de paso --

    private ModoDePasoDeCartera Modo() => new(_db, Provisional());

    private void SembrarTiposDeHecho()
    {
        _db.LendingFactTypes.AddRange(
            new LendingFactType { Code = PagoAplicadoV1.Type, ShortCode = "PAP", Name = "Pago aplicado", DefaultMode = "EnLinea", CreatedBy = "test" },
            new LendingFactType { Code = InteresCausadoV1.Type, ShortCode = "ICA", Name = "Interés causado", DefaultMode = "PorLotes", DefaultBatchTrigger = "FinDeCausacion", CreatedBy = "test" },
            new LendingFactType { Code = CreditoAprobadoSinDesembolsarV1.Type, ShortCode = "CAD", Name = "Aprobado sin desembolsar", DefaultMode = "NoPasa", CreatedBy = "test" });
        _db.SaveChanges();
    }

    [Fact]
    public async Task El_modo_es_el_defecto_del_tipo_de_hecho_con_la_contabilidad_iniciada()
    {
        IniciarContabilidad();
        SembrarTiposDeHecho();
        var fecha = new DateOnly(2026, 12, 1);

        (await Modo().ResolverAsync(PagoAplicadoV1.Type, fecha, default)).Value.Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Online));
        (await Modo().ResolverAsync(InteresCausadoV1.Type, fecha, default)).Value.Should().Be(
            new ModoDeEntrega.Sellado(DeliveryMode.Batch, "CAR.ICA|FinDeCausacion||PorDia", "AccrualRun:2026-12-02"),
            "US7 (T0653, C09): un hecho FinDeCausacion fuera de la corrida va al lote del día siguiente");
        (await Modo().ResolverAsync(CreditoAprobadoSinDesembolsarV1.Type, fecha, default)).Value.Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_no_pasa_y_los_informativos_y_otros_destinos_siempre_se_entregan()
    {
        SembrarTiposDeHecho();
        var fecha = new DateOnly(2026, 12, 1);

        (await Modo().ResolverAsync(PagoAplicadoV1.Type, fecha, default)).Value.Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.NotPosted));
        (await Modo().ResolverAsync(CarteraActivadaV1.Type, fecha, default)).Value.Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Always));
        (await Modo().ResolverAsync(CargaDeCarteraAplicadaV1.Type, fecha, default)).Value.Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Always));
        (await Modo().ResolverAsync(OrdenDeGiroEmitidaV1.Type, fecha, default)).Value.Should().Be(new ModoDeEntrega.Sellado(DeliveryMode.Always));
    }

    // ---------------------------------------------------------------------------------------- validación previa --

    private static MensajeContableDto Hecho() =>
        new(new IntegrationEnvelopeV1 { MessageId = Guid.NewGuid(), Type = PagoAplicadoV1.Type, OriginModule = "CAR" }, new object());

    private ValidacionPreviaContableDeCartera Validacion(IContabilidadParaCartera puerto) => new(puerto, new LectorDeParametros(_db), _reloj);

    [Fact]
    public async Task Sin_hechos_no_aplica_y_el_provisional_ya_no_es_un_caso_aparte()
    {
        (await Validacion(Provisional()).EvaluarAsync([], default)).Outcome.Should().Be(PrevalidationOutcome.NotApplicable);
        (await Validacion(Provisional()).EvaluarAsync([Hecho()], default)).Outcome.Should().Be(PrevalidationOutcome.NoResponse,
            "US7 (T0655): el adaptador real reemplaza al provisional y su «no disponible» ya no tiene trato propio");
    }

    [Fact]
    public async Task Si_el_puerto_falla_o_lanza_sella_sin_respuesta_y_no_bloquea()
    {
        var lanza = Substitute.For<IContabilidadParaCartera>();
        lanza.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TimeoutException("caída"));
        var falla = Substitute.For<IContabilidadParaCartera>();
        falla.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ResultadoDeEvaluacionContableDto>(new Error("Generic.Conflict", "otra cosa")));

        (await Validacion(lanza).EvaluarAsync([Hecho()], default)).Outcome.Should().Be(PrevalidationOutcome.NoResponse);
        (await Validacion(falla).EvaluarAsync([Hecho()], default)).Outcome.Should().Be(PrevalidationOutcome.NoResponse);
    }

    [Fact]
    public async Task Con_hallazgos_sella_no_contabilizable_con_ellos_y_sin_ellos_contabilizable()
    {
        var hallazgo = new HallazgoDeCarteraDto(PagoAplicadoV1.Type, "Capital", null, null, "Accounting.LendingRule.Missing", "Falta la regla",
            new QuienCorrigeDto("Contabilidad", "/contabilidad/cartera/matriz", "Accounting.LendingRules.Manage"));
        var noContabilizable = Substitute.For<IContabilidadParaCartera>();
        noContabilizable.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ResultadoDeEvaluacionContableDto(false, [hallazgo], [])));
        var contabilizable = Substitute.For<IContabilidadParaCartera>();
        contabilizable.EvaluarAsync(Arg.Any<IReadOnlyList<MensajeContableDto>>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ResultadoDeEvaluacionContableDto(true, [], [])));

        var no = await Validacion(noContabilizable).EvaluarAsync([Hecho()], default);
        no.Outcome.Should().Be(PrevalidationOutcome.NotPostable);
        no.Errors.Should().Equal(hallazgo);
        (await Validacion(contabilizable).EvaluarAsync([Hecho()], default)).Outcome.Should().Be(PrevalidationOutcome.Postable);
    }

    // -------------------------------------------------------------------------------------- fecha y categoría --

    [Fact]
    public async Task La_fecha_contable_es_la_fecha_valor_con_su_mes_abierto_y_si_no_el_primer_dia_del_primer_mes_abierto()
    {
        var fecha = new FechaContable(Provisional());
        (await fecha.CalcularAsync(new DateOnly(2026, 1, 20), default)).Value.Should().Be(new DateOnly(2026, 1, 20), "sin contabilidad iniciada");

        IniciarContabilidad(1, 2);
        (await fecha.CalcularAsync(new DateOnly(2026, 4, 20), default)).Value.Should().Be(new DateOnly(2026, 4, 20));
        (await fecha.CalcularAsync(new DateOnly(2026, 1, 20), default)).Value.Should().Be(new DateOnly(2026, 3, 1));
        (await fecha.CalcularAsync(new DateOnly(2027, 1, 20), default)).Value.Should().Be(new DateOnly(2027, 1, 20), "el mes que no existe no se salta");
    }

    [Fact]
    public async Task Sin_cierres_la_categoria_contable_es_la_de_credito_nuevo()
    {
        var categoria = await new CategoriaContableDeCartera().VigenteAsync(loanId: 41, new DateOnly(2026, 12, 1), default);

        categoria.Should().Be(new CategoriaContableVigente("A", RatingCause.NewLoan, null));
    }

    // ----------------------------------------------------------------------------------------- tesorería de prueba --

    private static UnidadDeConsumo Unidad() =>
        new(IntegrationDestinations.Treasury, Guid.NewGuid(), ClavesDeEvento.Confirmacion, [Guid.NewGuid()], null, 0) { OriginModule = "CAR" };

    [Fact]
    public async Task La_tesoreria_de_prueba_consume_sus_dos_mensajes_y_responde_procesado_sin_estado_propio()
    {
        var tesoreria = new TesoreriaDePrueba();

        tesoreria.Destino.Should().Be(IntegrationDestinations.Treasury);
        tesoreria.Acepta(OrdenDeGiroEmitidaV1.Type, 1).Should().BeTrue();
        tesoreria.Acepta(AnulacionDeOrdenDeGiroV1.Type, 1).Should().BeTrue();
        tesoreria.Acepta(OrdenDeGiroEmitidaV1.Type, 2).Should().BeTrue("feature 014 (T055): la v2 de captaciones, para TreasuryDestinationRegistered");
        tesoreria.Acepta(OrdenDeGiroEmitidaV1.Type, 3).Should().BeFalse();
        tesoreria.Acepta(PagoAplicadoV1.Type, 1).Should().BeFalse();

        var unidades = new[] { Unidad(), Unidad() };
        var primera = await tesoreria.ConsumirAsync(new TrabajoDeConsumo(null, unidades), default);
        var segunda = await tesoreria.ConsumirAsync(new TrabajoDeConsumo(null, unidades), default);

        primera.Select(r => r.Unidad).Should().Equal(unidades);
        primera.Should().OnlyContain(r => r.Resultado is ResultadoDeConsumo.Processed);
        segunda.Select(r => r.Resultado).Should().Equal(primera.Select(r => r.Resultado), "no guarda nada entre una entrega y otra");
        typeof(TesoreriaDePrueba).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
            .Should().BeEmpty("la tesorería de prueba no tiene estado propio");
    }

    [Fact]
    public void Los_dobles_se_registran_solo_si_se_piden_fuera_de_produccion_y_en_produccion_la_api_no_arranca()
    {
        var fuera = new ServiceCollection();
        DoblesDePruebaDeCarteraEnDi.Registrar(fuera, pedidos: true, esProduccion: false).Should().BeTrue();
        fuera.Should().ContainSingle(d => d.ServiceType == typeof(IDestinoDeMensajes) && d.ImplementationType == typeof(TesoreriaDePrueba));

        var sinPedir = new ServiceCollection();
        DoblesDePruebaDeCarteraEnDi.Registrar(sinPedir, pedidos: false, esProduccion: false).Should().BeFalse();
        DoblesDePruebaDeCarteraEnDi.Registrar(sinPedir, pedidos: false, esProduccion: true).Should().BeFalse();
        sinPedir.Should().BeEmpty();

        var produccion = new ServiceCollection();
        var arrancar = () => DoblesDePruebaDeCarteraEnDi.Registrar(produccion, pedidos: true, esProduccion: true);
        arrancar.Should().Throw<InvalidOperationException>().WithMessage("*producción*");
        produccion.Should().BeEmpty();
    }
}
