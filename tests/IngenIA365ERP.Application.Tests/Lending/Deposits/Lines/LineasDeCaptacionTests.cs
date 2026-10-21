using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Tests.Lending.Sessions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Lines;

/// <summary>
/// Feature 014, T068 (US1; api.md §3; data-model §1.1, §2.1, §2.2, §2.5; FR-007, FR-018, FR-027–FR-029): los comandos y consultas del catálogo común de
/// líneas para captación y aportes. Alta activa y en borrador, perfil sin captación (US1-1), código duplicado entre naturalezas (US1-4), versión
/// retroactiva, cruzada o de otra clase, la versión a una fecha antes y después de un cambio (US1-2), tope de la cuota de un fondo de empleados con su
/// fuente (US1-3), una sola línea de aportes activa, la entrega que trae cada naturaleza, la lista por naturaleza y permiso, cerrar y reabrir con el
/// permiso de la naturaleza, y los validadores. InMemory con hoy = 2026-12-01 (<see cref="GobiernoDePrueba"/>).
/// </summary>
public sealed class LineasDeCaptacionTests : IDisposable
{
    private static readonly DateOnly Hoy = GobiernoDePrueba.Hoy;

    private readonly GobiernoDePrueba _g = new();
    private readonly IContextoDeLineasDeCaptacion _contexto = Substitute.For<IContextoDeLineasDeCaptacion>();
    private PerfilParaLineas _perfil = new(true, false, false, "AHORRO_CREDITO", Guid.NewGuid());
    private TopesDeAportes _topes = TopesDeAportes.Ninguno;

    public LineasDeCaptacionTests()
    {
        _contexto.Entrega.Returns(_ => Entrega);
        _contexto.PerfilAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(_ => Task.FromResult(_perfil));
        _contexto.TopesDeAportesAsync(Arg.Any<DateOnly>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_topes)));
        _contexto.AnticipacionDelAvisoAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(0);
        _g.ActuarComo(_g.Usuario(_g.Persona("Juana", "Captaciones", "9001"), "jefa@coop.test"));
        _g.Conceder(PermisosDeLineas.VerCaptacion, PermisosDeLineas.AdministrarCaptacion, PermisosDeLineas.VerAportes, PermisosDeLineas.AdministrarAportes);
    }

    private EntregaDeCartera Entrega { get; set; } = EntregaDeCartera.K8;

    public void Dispose() => _g.Dispose();

    // ------------------------------------------------------------------------------------------------------- piezas --

    private AltaDeVersionDeCaptacion Alta() => new(_g.Db, _g.Reloj);
    private AvisoDeCambioDeTasa Aviso() => new(_g.Db, _contexto);
    private CreateDepositLineCommandHandler CrearCaptacion() => new(_g.Db, Alta(), _contexto, _g.Reloj);
    private AddDepositLineVersionCommandHandler VersionarCaptacion() => new(_g.Db, Alta(), Aviso(), _contexto, _g.Alcance, _g.Auditoria, _g.Actor, _g.Reloj);
    private CreateContributionLineCommandHandler CrearAportes() => new(_g.Db, Alta(), _contexto, _g.Reloj);
    private AddContributionLineVersionCommandHandler VersionarAportes() => new(_g.Db, Alta(), _contexto, _g.Alcance, _g.Auditoria, _g.Reloj);
    private GetLendingLineQueryHandler Leer() => new(_g.Db, _g.Alcance, _g.Permisos, _g.Auditoria, _g.Reloj);
    private ListLendingLinesQueryHandler Listar() => new(_g.Db, _g.Alcance, _g.Permisos, _g.Reloj);
    private CloseLendingLineCommandHandler Cerrar() => new(_g.Db, _g.Actor, _g.Alcance, _g.Permisos, _g.Auditoria, _g.Reloj);
    private ReopenLendingLineCommandHandler Reabrir() => new(_g.Db, _contexto, _g.Alcance, _g.Permisos, _g.Auditoria, _g.Reloj);

    /// <summary>Una versión a la vista con tramos de saldo: 1 % hasta $ 999.999,99 y 3 % desde $ 1.000.000 (quickstart §2, US1).</summary>
    private static DepositLineVersionInput ALaVista(DateOnly? desde = null, decimal tasaAlta = 0.03m) => new()
    {
        ValidFrom = desde ?? Hoy,
        Reason = "Tasa aprobada por el consejo, acta 12",
        AllowsCoHolders = true,
        AllowsMinors = true,
        AllowsPartialWithdrawals = true,
        Rate = new DepositRateInput
        {
            Kind = RateKind.Fixed,
            Tiers = [new DepositRateTierInput(BalanceFrom: 0m, BalanceTo: 999_999.99m, EffectiveAnnual: 0.01m), new DepositRateTierInput(BalanceFrom: 1_000_000m, EffectiveAnnual: tasaAlta)],
        },
        Minimums = new DepositMinimumsInput(10_000m, 10_000m, 100_000m),
        Limits = new DepositLimitsInput(20_000_000m, 30_000_000m, 10_000_000m, 10_000_000m),
    };

    /// <summary>Una versión de aportes con cuota del 3 % del ingreso, mensual (quickstart §2, US1).</summary>
    private static ContributionLineVersionInput TresPorCiento(DateOnly? desde = null, decimal fraccion = 0.03m) => new()
    {
        ValidFrom = desde ?? Hoy,
        Reason = "Estatutos, art. 20",
        MinimumNonReducible = new ContributionAmountInput(ContributionAmountMode.FixedAmount, 500_000m),
        Installment = new ContributionInstallmentInput { Kind = ContributionAmountMode.IncomeFraction, Fraction = fraccion, DueDay = 5 },
        RefundTermDays = 60,
    };

    private static CreateDepositLineCommand AltaDeCaptacion(string codigo, bool activa = true, DepositLineVersionInput? version = null,
        DepositClass clase = DepositClass.Demand) =>
        new(codigo, $"Ahorro {codigo}", clase, activa, version ?? ALaVista()) { OperationKey = Guid.NewGuid() };

    /// <summary>Deja las captaciones y los aportes activados (la fila única de la Cartera): desde ahí una versión no puede empezar antes de hoy.</summary>
    private async Task ActivarCaptacionesAsync()
    {
        _g.Db.LendingSetups.Add(new LendingSetup
        {
            Id = LendingSetup.IdDeLaFilaUnica, ActivationDate = Hoy.AddMonths(-6), DepositsActivationDate = Hoy.AddMonths(-1), ContributionsActivationDate = Hoy.AddMonths(-1),
        });
        await _g.Db.SaveChangesAsync();
    }

    private async Task<LendingLineDto> CaptacionAsync(string codigo = "AHVISTA", bool activa = true)
    {
        var r = await CrearCaptacion().Handle(AltaDeCaptacion(codigo, activa), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<LendingLineDto> AportesAsync(string codigo = "APORTES", bool activa = true, ContributionLineVersionInput? version = null)
    {
        var r = await CrearAportes().Handle(new CreateContributionLineCommand(codigo, "Aportes sociales", activa, version ?? TresPorCiento())
            { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    // ------------------------------------------------------------------------------------------------------- captación --

    [Fact]
    public void El_alta_de_captacion_activa_nace_con_su_version_sus_tramos_y_su_huella()
    {
        var r = CrearCaptacion().Handle(AltaDeCaptacion(" ahvista "), default).GetAwaiter().GetResult();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Code.Should().Be("AHVISTA");
        r.Value.Nature.Should().Be(LineNature.Deposit);
        r.Value.DepositClass.Should().Be(DepositClass.Demand);
        r.Value.Status.Should().Be(CreditLineStatus.Active);
        var v = r.Value.DepositVersion!;
        v.VersionNumber.Should().Be(1);
        v.Tiers.Should().HaveCount(2);
        v.TierMode.Should().Be(RateTierMode.WholeBalance);
        v.WithholdingRateParameterCode.Should().Be(ParametrosDeCartera.RetencionTarifaAhorro);
        var guardada = _g.Db.DepositLineVersions.Single();
        guardada.ContentSha256.Should().Be(guardada.HuellaDeLoGuardado(_g.Db.DepositLineRateTiers.ToList()));
    }

    [Fact]
    public async Task US1_1_sin_perfil_que_autorice_captar_no_se_activa_ni_se_guarda_nada()
    {
        _perfil = new PerfilParaLineas(false, false, false, "MULTIACTIVA", Guid.NewGuid());

        var r = await CrearCaptacion().Handle(AltaDeCaptacion("AHVISTA"), default);

        r.Error.Code.Should().Be("Lending.Savings.LineNotAllowedByProfile");
        r.Error.Message.Should().Contain("MULTIACTIVA").And.Contain("aportes");
        (await _g.Db.CreditLines.CountAsync()).Should().Be(0);
        (await _g.Db.DepositLineVersions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task En_borrador_se_crea_sin_perfil_y_reabrir_la_activa_cuando_el_perfil_lo_autoriza()
    {
        _perfil = new PerfilParaLineas(false, false, false, "MULTIACTIVA", null);
        var linea = await CaptacionAsync(activa: false);
        linea.Status.Should().Be(CreditLineStatus.Draft);

        (await Reabrir().Handle(new ReopenLendingLineCommand(linea.PublicId, "Activar"), default)).Error.Code
            .Should().Be("Lending.Savings.LineNotAllowedByProfile");

        _perfil = new PerfilParaLineas(true, false, false, "AHORRO_CREDITO", null);
        var activa = await Reabrir().Handle(new ReopenLendingLineCommand(linea.PublicId, "Activar"), default);
        activa.Value.Status.Should().Be(CreditLineStatus.Active);
    }

    [Fact]
    public async Task Sin_K8_la_captacion_se_crea_pero_no_se_activa()
    {
        Entrega = EntregaDeCartera.K7;

        var activa = await CrearCaptacion().Handle(AltaDeCaptacion("AHVISTA"), default);
        var enBorrador = await CrearCaptacion().Handle(AltaDeCaptacion("AHVISTA", activa: false), default);

        activa.Error.Code.Should().Be("Lending.Savings.DeliveryNotAvailable");
        GobiernoDePrueba.Datos(activa.Error).GetProperty("availableFrom").GetString().Should().Be("K8");
        enBorrador.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task US1_4_un_codigo_de_cualquier_naturaleza_ya_existente_es_CodigoDuplicado_con_el_nombre_antes_de_guardar()
    {
        _g.Db.CreditLines.Add(CreditLine.Nueva("CONLIB", "Consumo libre inversión", null));
        await _g.Db.SaveChangesAsync();

        var r = await CrearCaptacion().Handle(AltaDeCaptacion("conlib"), default);
        var aportes = await CrearAportes().Handle(new CreateContributionLineCommand("CONLIB", "Aportes", true, TresPorCiento()), default);

        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        r.Error.Message.Should().Contain("Consumo libre inversión");
        aportes.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        (await _g.Db.CreditLines.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Un_perfil_que_capta_solo_de_asociados_fuerza_exige_asociado()
    {
        _perfil = new PerfilParaLineas(true, true, false, "AHORRO_CREDITO", null);

        var r = await CrearCaptacion().Handle(AltaDeCaptacion("AHVISTA", version: ALaVista() with { RequiresMembership = false }), default);

        r.Value.DepositVersion!.RequiresMembership.Should().BeTrue();
    }

    [Fact]
    public async Task Una_version_mal_formada_responde_con_su_campo_y_no_guarda_nada()
    {
        var cruzados = ALaVista() with
        {
            Rate = new DepositRateInput
            {
                Tiers = [new DepositRateTierInput(BalanceFrom: 0m, BalanceTo: 1_000_000m, EffectiveAnnual: 0.01m),
                    new DepositRateTierInput(BalanceFrom: 1_000_000m, EffectiveAnnual: 0.03m)],
            },
        };
        var cdatIncompleto = ALaVista() with { AllowsPartialWithdrawals = false, Rate = new DepositRateInput { EffectiveAnnual = 0.09m },
            TermDeposit = new TermDepositLineInput { MinTermDays = 30, MaxTermDays = 540, Timings = [] } };

        (await CrearCaptacion().Handle(AltaDeCaptacion("A1", version: cruzados), default)).Error.Code.Should().Be("Lending.Savings.RateTiersOverlap");
        var incompleto = await CrearCaptacion().Handle(AltaDeCaptacion("A2", version: cdatIncompleto, clase: DepositClass.TermDeposit), default);
        incompleto.Error.Code.Should().Be("Lending.TermDeposit.LineIncomplete");
        GobiernoDePrueba.Datos(incompleto.Error).GetProperty("field").GetString().Should().Be("interestTimings");
        await ActivarCaptacionesAsync();
        (await CrearCaptacion().Handle(AltaDeCaptacion("A3", version: ALaVista(Hoy.AddDays(-1))), default)).Error.Code
            .Should().Be("Lending.DepositLine.Retroactive");
        (await _g.Db.CreditLines.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_cdat_que_pasa_a_la_vista_nombra_una_linea_a_la_vista_viva()
    {
        var vista = await CaptacionAsync();
        var permanente = await CrearCaptacion().Handle(AltaDeCaptacion("PERM", clase: DepositClass.Permanent,
            version: ALaVista() with { AllowsPartialWithdrawals = false }), default);
        permanente.IsSuccess.Should().BeTrue(permanente.IsFailure ? permanente.Error.Message : string.Empty);
        TermDepositLineInput Cdat(Guid destino) => new()
        {
            MinTermDays = 30, MaxTermDays = 540, AfterMaturity = AfterMaturityTreatment.MoveToDemand, DemandLinePublicId = destino,
        };
        DepositLineVersionInput Version(Guid destino) => ALaVista() with
        {
            AllowsPartialWithdrawals = false, Rate = new DepositRateInput { EffectiveAnnual = 0.09m }, TermDeposit = Cdat(destino),
        };

        var aPermanente = await CrearCaptacion().Handle(AltaDeCaptacion("CDAT1", version: Version(permanente.Value.PublicId), clase: DepositClass.TermDeposit), default);
        var aVista = await CrearCaptacion().Handle(AltaDeCaptacion("CDAT2", version: Version(vista.PublicId), clase: DepositClass.TermDeposit), default);

        aPermanente.Error.Code.Should().Be("Lending.DepositLine.ReferencedLineInvalid");
        aVista.IsSuccess.Should().BeTrue(aVista.IsFailure ? aVista.Error.Message : string.Empty);
        aVista.Value.DepositVersion!.DemandLine!.Code.Should().Be("AHVISTA");
        aVista.Value.DepositVersion.TermDeposit!.InterestPeriodicities.Should().Equal(RepaymentPeriodicity.SinglePayment);
    }

    // -------------------------------------------------------------------------------------------------------- versiones --

    [Fact]
    public async Task US1_2_la_version_con_fecha_futura_rige_desde_ese_dia_y_la_consulta_anterior_devuelve_la_anterior()
    {
        var linea = await CaptacionAsync();
        var desde = Hoy.AddDays(14);

        var v2 = await VersionarCaptacion().Handle(new AddDepositLineVersionCommand(linea.PublicId, ALaVista(desde, tasaAlta: 0.035m))
            { OperationKey = Guid.NewGuid() }, default);

        v2.IsSuccess.Should().BeTrue(v2.IsFailure ? $"{v2.Error.Code}: {v2.Error.Message}" : string.Empty);
        v2.Value.VersionNumber.Should().Be(2);
        var antes = await Leer().Handle(new GetLendingLineQuery(linea.PublicId, desde.AddDays(-1)), default);
        var despues = await Leer().Handle(new GetLendingLineQuery(linea.PublicId, desde), default);
        antes.Value.DepositVersion!.VersionNumber.Should().Be(1);
        antes.Value.DepositVersion.ValidTo.Should().Be(desde.AddDays(-1));
        antes.Value.DepositVersion.Tiers[1].EffectiveAnnual.Should().Be(0.03m);
        despues.Value.DepositVersion!.Tiers[1].EffectiveAnnual.Should().Be(0.035m);
        despues.Value.Versions.Should().HaveCount(2);
        despues.Value.Versions.Single(x => x.IsCurrent).VersionNumber.Should().Be(2);
    }

    [Fact]
    public async Task La_consulta_con_lo_registrado_hasta_un_instante_no_ve_lo_que_se_registro_despues()
    {
        var linea = await CaptacionAsync();
        var registradaLaPrimera = _g.UtcNow;
        _g.UtcNow = _g.UtcNow.AddHours(2);
        await VersionarCaptacion().Handle(new AddDepositLineVersionCommand(linea.PublicId, ALaVista(Hoy, tasaAlta: 0.04m)) { OperationKey = Guid.NewGuid() }, default);

        var ahora = await Leer().Handle(new GetLendingLineQuery(linea.PublicId, Hoy), default);
        var antes = await Leer().Handle(new GetLendingLineQuery(linea.PublicId, Hoy, registradaLaPrimera.AddMinutes(1)), default);

        ahora.Value.DepositVersion!.VersionNumber.Should().Be(2);
        antes.Value.DepositVersion!.VersionNumber.Should().Be(1);
        antes.Value.Versions.Should().ContainSingle();
    }

    [Fact]
    public async Task Una_version_que_empieza_antes_de_la_ultima_cruza_y_la_clase_no_cambia()
    {
        var linea = await CaptacionAsync();
        await VersionarCaptacion().Handle(new AddDepositLineVersionCommand(linea.PublicId, ALaVista(Hoy.AddDays(30))) { OperationKey = Guid.NewGuid() }, default);

        var cruzada = await VersionarCaptacion().Handle(new AddDepositLineVersionCommand(linea.PublicId, ALaVista(Hoy.AddDays(10))), default);
        var otraClase = await VersionarCaptacion().Handle(new AddDepositLineVersionCommand(linea.PublicId,
            ALaVista(Hoy.AddDays(40)) with { DepositClass = DepositClass.Permanent }), default);
        await ActivarCaptacionesAsync();
        var retroactiva = await VersionarCaptacion().Handle(new AddDepositLineVersionCommand(linea.PublicId, ALaVista(Hoy.AddDays(-1))), default);

        cruzada.Error.Code.Should().Be("Lending.DepositLine.VersionOverlaps");
        otraClase.Error.Code.Should().Be("Lending.DepositLine.ClassImmutable");
        // Con las captaciones activadas la versión de hace un día es retroactiva; se evalúa primero lo retroactivo (regla de la versión), después el cruce.
        retroactiva.Error.Code.Should().Be("Lending.DepositLine.Retroactive");
        (await _g.Db.DepositLineVersions.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Antes_de_activar_una_linea_admite_versiones_con_vigencia_pasada_y_despues_no()
    {
        // Respuesta 106 A del dueño (E250): la historia de tarifas de la puesta en marcha se carga con su fecha; activadas las captaciones, no.
        var antes = await CrearCaptacion().Handle(AltaDeCaptacion("PAS1", version: ALaVista(Hoy.AddMonths(-8))), default);
        antes.IsSuccess.Should().BeTrue(antes.IsFailure ? antes.Error.Message : string.Empty);
        var aportes = await CrearAportes().Handle(new CreateContributionLineCommand("PAP1", "Aportes", false, TresPorCiento(Hoy.AddMonths(-8))) { OperationKey = Guid.NewGuid() }, default);
        aportes.IsSuccess.Should().BeTrue(aportes.IsFailure ? aportes.Error.Message : string.Empty);

        await ActivarCaptacionesAsync();
        var despues = await CrearCaptacion().Handle(AltaDeCaptacion("PAS2", version: ALaVista(Hoy.AddMonths(-8))), default);
        despues.Error.Code.Should().Be("Lending.DepositLine.Retroactive");
    }

    [Fact]
    public async Task Las_versiones_van_sobre_su_naturaleza()
    {
        var aportes = await AportesAsync();
        _g.Db.CreditLines.Add(CreditLine.Nueva("CONLIB", "Consumo", null));
        await _g.Db.SaveChangesAsync();
        var credito = await _g.Db.CreditLines.SingleAsync(l => l.Code == "CONLIB");

        (await VersionarCaptacion().Handle(new AddDepositLineVersionCommand(aportes.PublicId, ALaVista()), default)).Error.Code
            .Should().Be(CreditLine.CodigoNaturalezaDistinta);
        (await VersionarCaptacion().Handle(new AddDepositLineVersionCommand(credito.PublicId, ALaVista()), default)).Error.Code
            .Should().Be("Lending.Line.UseCreditLinesRoute");
        (await VersionarAportes().Handle(new AddContributionLineVersionCommand(Guid.NewGuid(), TresPorCiento()), default)).Error.Code
            .Should().Be("Lending.Line.NotFound");
    }

    [Fact]
    public async Task Fuera_del_alcance_la_linea_es_inexistente()
    {
        var linea = await CaptacionAsync();
        _g.AlcanceActual = AlcanceDeCartera.Vacio;

        (await Leer().Handle(new GetLendingLineQuery(linea.PublicId), default)).Error.Code.Should().Be("Lending.Line.NotFound");
        (await VersionarCaptacion().Handle(new AddDepositLineVersionCommand(linea.PublicId, ALaVista(Hoy.AddDays(5))), default)).Error.Code
            .Should().Be("Lending.Line.NotFound");
        (await Listar().Handle(new ListLendingLinesQuery(), default)).Value.Should().BeEmpty();
    }

    // ----------------------------------------------------------------------------------------------------------- aportes --

    [Fact]
    public async Task La_linea_de_aportes_nace_sin_tasa_con_su_cuota_del_tres_por_ciento_del_ingreso()
    {
        var linea = await AportesAsync();

        linea.Nature.Should().Be(LineNature.Contribution);
        linea.Status.Should().Be(CreditLineStatus.Active);
        linea.ContributionVersion!.Installment.Kind.Should().Be(ContributionAmountMode.IncomeFraction);
        linea.ContributionVersion.Installment.Fraction.Should().Be(0.03m);
        linea.ContributionVersion.MinimumNonReducible.Amount.Should().Be(500_000m);
        var guardada = await _g.Db.ContributionLineVersions.SingleAsync();
        guardada.ContentSha256.Should().Be(guardada.HuellaDeLoGuardado());
    }

    [Fact]
    public async Task Una_sola_linea_de_aportes_activa()
    {
        await AportesAsync();

        var segunda = await CrearAportes().Handle(new CreateContributionLineCommand("APORTES2", "Otros aportes", true, TresPorCiento()), default);
        var enBorrador = await AportesAsync("APORTES3", activa: false);

        segunda.Error.Code.Should().Be("Lending.Contributions.LineAlreadyActive");
        (await Reabrir().Handle(new ReopenLendingLineCommand(enBorrador.PublicId, "Activar"), default)).Error.Code
            .Should().Be("Lending.Contributions.LineAlreadyActive");
    }

    [Fact]
    public async Task Sin_K7_los_aportes_no_se_activan()
    {
        Entrega = EntregaDeCartera.K6b;

        var r = await CrearAportes().Handle(new CreateContributionLineCommand("APORTES", "Aportes", true, TresPorCiento()), default);

        r.Error.Code.Should().Be("Lending.Savings.DeliveryNotAvailable");
        GobiernoDePrueba.Datos(r.Error).GetProperty("availableFrom").GetString().Should().Be("K7");
    }

    [Fact]
    public async Task US1_3_en_un_fondo_de_empleados_la_cuota_sobre_el_tope_se_rechaza_con_el_tope_y_su_fuente()
    {
        _perfil = new PerfilParaLineas(false, false, true, "FONDO_EMPLEADOS", null);
        _topes = new TopesDeAportes(0.10m, "Ley 2496 de 2025", 0.10m, "Ley 2496 de 2025", null, null, null);
        var permanente = await CrearCaptacion().Handle(AltaDeCaptacion("PERM", activa: false, clase: DepositClass.Permanent,
            version: ALaVista() with { AllowsPartialWithdrawals = false }), default);
        var version = TresPorCiento(fraccion: 0.12m) with { PermanentSavingsShare = new PermanentSavingsShareInput(permanente.Value.PublicId, 0.5m) };

        var r = await CrearAportes().Handle(new CreateContributionLineCommand("APORTES", "Aportes", true, version), default);

        r.Error.Code.Should().Be("Lending.Contributions.InstallmentCapExceeded");
        var datos = GobiernoDePrueba.Datos(r.Error);
        datos.GetProperty("capPercent").GetDecimal().Should().Be(0.10m);
        datos.GetProperty("source").GetString().Should().Be("Ley 2496 de 2025");
        (await _g.Db.ContributionLineVersions.CountAsync()).Should().Be(0);

        var conLaMitad = await CrearAportes().Handle(new CreateContributionLineCommand("APORTES", "Aportes", true,
            version with { Installment = version.Installment with { Fraction = 0.10m } }), default);
        conLaMitad.IsSuccess.Should().BeTrue(conLaMitad.IsFailure ? conLaMitad.Error.Message : string.Empty);
        conLaMitad.Value.ContributionVersion!.PermanentSavingsLine!.Code.Should().Be("PERM");
    }

    [Fact]
    public async Task El_ahorro_permanente_de_los_aportes_es_una_linea_permanente()
    {
        var vista = await CaptacionAsync();
        var version = TresPorCiento() with { PermanentSavingsShare = new PermanentSavingsShareInput(vista.PublicId, 0.3m) };

        (await CrearAportes().Handle(new CreateContributionLineCommand("APORTES", "Aportes", true, version), default)).Error.Code
            .Should().Be("Lending.DepositLine.ReferencedLineInvalid");
    }

    [Fact]
    public async Task La_version_de_aportes_versiona_y_no_cruza()
    {
        var linea = await AportesAsync();

        var v2 = await VersionarAportes().Handle(new AddContributionLineVersionCommand(linea.PublicId, TresPorCiento(Hoy.AddDays(31), 0.04m))
            { OperationKey = Guid.NewGuid() }, default);
        var cruzada = await VersionarAportes().Handle(new AddContributionLineVersionCommand(linea.PublicId, TresPorCiento(Hoy.AddDays(5))), default);

        v2.Value.VersionNumber.Should().Be(2);
        cruzada.Error.Code.Should().Be("Lending.ContributionLine.VersionOverlaps");
        (await Leer().Handle(new GetLendingLineQuery(linea.PublicId, Hoy.AddDays(30)), default)).Value.ContributionVersion!.Installment.Fraction
            .Should().Be(0.03m);
    }

    // ------------------------------------------------------------------------------------------------- lista y permisos --

    [Fact]
    public async Task La_lista_trae_solo_las_naturalezas_que_quien_pregunta_puede_ver()
    {
        await CaptacionAsync();
        await AportesAsync();
        _g.Db.CreditLines.Add(CreditLine.Nueva("CONLIB", "Consumo", null));
        await _g.Db.SaveChangesAsync();

        _g.Conceder(PermisosDeLineas.VerCaptacion);
        var soloCaptacion = (await Listar().Handle(new ListLendingLinesQuery(), default)).Value;
        _g.Conceder(PermisosDeLineas.VerCaptacion, PermisosDeLineas.VerAportes, PermisosDeLineas.VerCredito);
        var todas = (await Listar().Handle(new ListLendingLinesQuery(), default)).Value;
        var deAportes = (await Listar().Handle(new ListLendingLinesQuery(Nature: LineNature.Contribution), default)).Value;
        var aLaVista = (await Listar().Handle(new ListLendingLinesQuery(DepositClass: DepositClass.Demand), default)).Value;

        soloCaptacion.Should().ContainSingle().Which.Code.Should().Be("AHVISTA");
        soloCaptacion[0].CurrentVersion!.Summary.Should().Contain("Ahorro a la vista").And.Contain("2 tramos");
        todas.Select(l => l.Code).Should().Equal("AHVISTA", "APORTES", "CONLIB");
        deAportes.Should().ContainSingle().Which.CurrentVersion!.Summary.Should().Contain("3 % del ingreso");
        aLaVista.Should().ContainSingle().Which.DepositClass.Should().Be(DepositClass.Demand);
        (await Listar().Handle(new ListLendingLinesQuery(Nature: LineNature.Contribution, DepositClass: DepositClass.Demand), default)).Error.Code
            .Should().Be("Lending.Report.FilterInvalid");
    }

    [Fact]
    public async Task Sin_el_View_de_su_naturaleza_la_linea_es_inexistente()
    {
        var linea = await AportesAsync();
        _g.Conceder(PermisosDeLineas.VerCaptacion);

        (await Leer().Handle(new GetLendingLineQuery(linea.PublicId), default)).Error.Code.Should().Be("Lending.Line.NotFound");
    }

    [Fact]
    public async Task Cerrar_y_reabrir_exigen_el_Manage_de_la_naturaleza()
    {
        var linea = await AportesAsync();
        _g.Conceder(PermisosDeLineas.VerAportes, PermisosDeLineas.AdministrarCaptacion);

        var sinPermiso = await Cerrar().Handle(new CloseLendingLineCommand(linea.PublicId, "Fin del plan"), default);
        sinPermiso.Error.Code.Should().Be("Lending.Permission.Required");

        _g.Conceder(PermisosDeLineas.VerAportes, PermisosDeLineas.AdministrarAportes);
        var cerrada = await Cerrar().Handle(new CloseLendingLineCommand(linea.PublicId, "Fin del plan"), default);
        cerrada.Value.Status.Should().Be(CreditLineStatus.Closed);
        cerrada.Value.CloseNotes.Should().Be("Fin del plan");
        (await Cerrar().Handle(new CloseLendingLineCommand(linea.PublicId, "Otra vez"), default)).Error.Code.Should().Be(CreditLine.CodigoYaCerrada);
        (await VersionarAportes().Handle(new AddContributionLineVersionCommand(linea.PublicId, TresPorCiento(Hoy.AddDays(3))), default)).Error.Code
            .Should().Be(CreditLine.CodigoCerrada);
        (await Reabrir().Handle(new ReopenLendingLineCommand(linea.PublicId, "Vuelve"), default)).Value.Status.Should().Be(CreditLineStatus.Active);
    }

    // ------------------------------------------------------------------------------------------------------ validadores --

    [Fact]
    public void Los_validadores_rechazan_la_forma()
    {
        new CreateDepositLineCommandValidator().Validate(AltaDeCaptacion("con espacio")).IsValid.Should().BeFalse();
        new CreateDepositLineCommandValidator().Validate(AltaDeCaptacion("AHVISTA", version: ALaVista() with { Reason = "" })).IsValid.Should().BeFalse();
        new CreateDepositLineCommandValidator().Validate(AltaDeCaptacion("AHVISTA")).IsValid.Should().BeTrue();
        new CreateContributionLineCommandValidator().Validate(new CreateContributionLineCommand("APORTES", "", true, TresPorCiento())).IsValid.Should().BeFalse();
        new AddContributionLineVersionCommandValidator().Validate(new AddContributionLineVersionCommand(Guid.NewGuid(),
            TresPorCiento() with { ValidFrom = default })).IsValid.Should().BeFalse();
        new CloseLendingLineCommandValidator().Validate(new CloseLendingLineCommand(Guid.NewGuid(), " ")).IsValid.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------- el contexto de verdad --

    [Fact]
    public async Task El_contexto_lee_el_perfil_y_los_topes_con_la_fuente_del_limite_que_rige()
    {
        _g.Perfil.VigenteAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new PerfilRegulatorioDto(Guid.NewGuid(), new DateOnly(2026, 1, 1), "FONDO_EMPLEADOS", false, 1, "CATEGORIA_A", 2, false, null, null,
                "Resolución", null, "Perfil", null, "Prueba", GobiernoDePrueba.Ahora.AddYears(-1)) { DepositsFromAssociatesOnly = true });
        _g.Limite(CatalogoDeLimitesLegales.AportesTopeCuotaFondoEmpleados, 0.10m, "Ley 2496 de 2025");
        _g.Limite(CatalogoDeLimitesLegales.AportesFraccionMinima, 0.10m, "Ley 2496 de 2025");
        _g.Limite(CatalogoDeLimitesLegales.AportesLimiteParticipacionPersonaNatural, 0.10m, "Ley 79 de 1988");
        _g.Limite(CatalogoDeLimitesLegales.AportesLimiteParticipacionPersonaJuridica, 0.49m, "Ley 79 de 1988");
        var contexto = new ContextoDeLineasDeCaptacion(_g.Perfil, new ReglasDeParametrosDeCartera(_g.Valores, _g.Parametros()), EntregaDeCartera.K7);

        var perfil = await contexto.PerfilAsync(Hoy, default);
        var topes = await contexto.TopesDeAportesAsync(Hoy, perfil.EsFondoDeEmpleados, default);

        contexto.Entrega.Should().Be(EntregaDeCartera.K7);
        perfil.Should().Be(perfil with { AutorizaCaptar = false, SoloAsociados = true, EsFondoDeEmpleados = true, TipoDeOrganizacion = "FONDO_EMPLEADOS" });
        topes.IsSuccess.Should().BeTrue(topes.IsFailure ? topes.Error.Message : string.Empty);
        topes.Value.TopeDeCuota.Should().Be(0.10m);
        topes.Value.FuenteDelTope.Should().Be("Ley 2496 de 2025");
        topes.Value.FraccionMinima.Should().Be(0.10m);
        topes.Value.LimiteDeParticipacion.Should().Be(0.10m, "el más estricto entre persona natural y jurídica");
        topes.Value.ClaveDelLimite.Should().Be(ParametrosDeCartera.AportesLimiteParticipacionPersonaNatural);
        (await contexto.AnticipacionDelAvisoAsync(Hoy, default)).Should().Be(0, "sin valor del reglamento");
    }
}
