using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Liquidity;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Liquidity;

/// <summary>
/// Feature 014, US15 (T216; FR-025, FR-109, FR-110; research §24): el fondo de liquidez sobre el libro real —la prueba independiente (CDAT de distintos
/// vencimientos y ahorro permanente sólo retirable al desvincularse: el requerido por banda igual al cálculo manual), la transición que deja regir el régimen
/// general, US15-1 (constituido menor que el requerido: alerta al tesorero y a la gerencia con la diferencia; una foto por fecha), el colchón, la inversión que
/// deja de contar, la categoría que falta en el perfil, la medición del IRL con los activos líquidos netos descontados, los depósitos asegurados hasta el tope
/// y los días de corte del ciclo—. Sobre <see cref="AportesDePrueba"/> (hoy 2026-12-01) con la entrega K11. (nuevo)
/// </summary>
public sealed class FondoDeLiquidezTests : IDisposable
{
    private const decimal Uvt = 49_799m;

    private const string ReglasDePrueba =
        "{\"*\": {\"regimen\": \"General\", \"porcentajeDepositos\": 0.10, \"porcentajePermanenteSoloAlRetirarse\": 0.02}, "
        + "\"Plena\": {\"regimen\": \"PorMadurez\", \"porcentajeDepositos\": 0.10, \"porcentajePermanenteSoloAlRetirarse\": 0.05, "
        + "\"bandas\": [{\"hastaDias\": 30, \"porcentaje\": 0.10}, {\"hastaDias\": 540, \"porcentaje\": 0.05}, {\"hastaDias\": null, \"porcentaje\": 0}]}}";

    private readonly AportesDePrueba _a = new();
    private readonly IAlertas _alertas = Substitute.For<IAlertas>();

    public FondoDeLiquidezTests()
    {
        _a.C.Entrega = EntregaDeCartera.K11;
        _a.C.EntregaDeParametros = EntregaDeCartera.K11;
        G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, Uvt, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        G.Limite(CatalogoDeLimitesLegales.FondoLiquidezDepositos, 0.10m, "Decreto 976 de 2026");
        G.Limite(CatalogoDeLimitesLegales.FondoLiquidezPermanenteSoloAlRetiro, 0.02m, "Decreto 976 de 2026");
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
        _alertas.LevantarAsync(Arg.Any<AlertaALevantar>(), Arg.Any<CancellationToken>())
            .Returns(_ => Result.Success(new AlertaLevantada(Guid.NewGuid(), DesenlaceDeAlerta.Levantada, 2, false)));
        _a.Cdat = Constitucion;
        Perfil("Plena", asegurada: true);
        Parametro(ParametrosDeCartera.FondoDeLiquidezPorcentajesPorCategoria, ReglasDePrueba);
        G.ActuarComo(_a.C.Jefe);
    }

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private Sessions.GobiernoDePrueba G => _a.G;

    private FondoDeLiquidezDeCartera Fondo() => new(_a.Db, G.Perfil, _a.C.Parametros(), G.Valores);

    private FotosDelFondoDeLiquidez Fotos() => new(_a.Db, Fondo(), G.Actor, G.Reloj, _alertas);

    private void Perfil(string? categoria, bool asegurada = false) =>
        G.Perfil.VigenteAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new PerfilRegulatorioDto(Guid.NewGuid(), new DateOnly(2026, 1, 1), "CAC", true, 1, null, 2, false, null, null, "Resolución de prueba", null,
                "Perfil de prueba", null, "Prueba", Sessions.GobiernoDePrueba.Ahora.AddYears(-1), MayTakeDeposits: true, LiquidityFundCategory: categoria,
                DepositInsuranceEnrolled: asegurada));

    private void Parametro(string clave, string valor, DateOnly? desde = null)
    {
        _a.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = clave, ScopeKind = ParameterScopeKind.None, Value = valor, ValidFrom = desde ?? new DateOnly(2026, 1, 1),
            Reason = "prueba", CreatedAt = G.UtcNow.AddDays(-10),
        });
        _a.Db.SaveChanges();
        _a.Db.ChangeTracker.Clear();
    }

    private LectorDeCdat Lector() => new(_a.Db, _a.Calendario, _a.LectorDeInteres());

    private NegociacionDeTasa Negociacion() => new(_a.Db, new MatrizVigenteDeAtribuciones(_a.Db), new AtribucionDelDecisor(_a.Db, G.Permisos), G.Reloj);

    private CotizacionDeCdat Cotizacion() => new(_a.Db, Lector(), Negociacion(), new ReglasDeRetencion(_a.C.Parametros(), G.Valores));

    private ConstitucionDeCdat Constitucion() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Movimientos(), _a.Retiro(), Lector(),
        _a.C.Cerrojo, G.Actor, G.Reloj);

    private AperturaDeCuentas Apertura() => new(_a.Db, _a.C.Contexto, _a.C.Titularidad(), _a.C.Parametros(), _a.C.Listas, G.Intervenciones(),
        _a.RegistroDeCuentas(), _a.Numerador(), _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);

    private async Task<LendingLineDto> LineaDeCdatAsync() =>
        await _a.C.LineaDeCaptacionAsync("CDAT", new DepositLineVersionInput
        {
            ValidFrom = CuentasDePrueba.Hoy,
            Reason = "Reglamento de CDAT, acta 30",
            Rate = new DepositRateInput { Kind = RateKind.Fixed, EffectiveAnnual = 0.10m },
            DayCount = DayCountConvention.Actual360,
            Minimums = new DepositMinimumsInput(1_000_000m),
            SettlementPeriodicity = RepaymentPeriodicity.SinglePayment,
            TermDeposit = new TermDepositLineInput
            {
                MinTermDays = 30, MaxTermDays = 720, InterestPeriodicities = [RepaymentPeriodicity.SinglePayment],
                Timings = [InterestTiming.Arrears],
            },
        }, DepositClass.TermDeposit);

    private async Task<MemberAccountDto> CuentaConSaldoAsync(string codigo, DepositClass clase, decimal saldo, DepositLineVersionInput? version = null)
    {
        var linea = await _a.C.LineaDeCaptacionAsync(codigo, version ?? CuentasDePrueba.ALaVista(), clase);
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, _a.C.Ana));
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, saldo, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Transfer));
        return cuenta;
    }

    private async Task<TermDepositDto> CdatAsync(LendingLineDto linea, MemberAccountDto ahorro, decimal monto, int plazo)
    {
        var r = await new ConstituteTermDepositCommandHandler(_a.Db, _a.C.Contexto, Apertura(), Cotizacion(), Constitucion(), Lector(), G.Intervenciones(),
            _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj).Handle(new ConstituteTermDepositCommand(new ConstituteTermDepositInput
        {
            LinePublicId = linea.PublicId, HolderPersonPublicId = _a.C.Ana.PublicId, OfficePublicId = _a.C.Oficina.PublicId, Amount = monto, TermDays = plazo,
            InterestPeriodicity = RepaymentPeriodicity.SinglePayment, Timing = InterestTiming.Arrears,
            PaymentDestination = new TermDepositDestinationInput(TermDepositDestinationKind.Account, ahorro.PublicId),
            Funding = new TermDepositFundingInput(TermDepositFundingKind.TransferFromAccount, ahorro.PublicId, [_a.C.Ana.PublicId]),
        }) { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>El ahorro a la vista (20 millones), dos CDAT (2 millones a 30 días y 3 millones a 180) y el permanente sólo retirable al retiro (1 millón).</summary>
    private async Task<(MemberAccountDto Vista, TermDepositDto Corto, TermDepositDto Medio, MemberAccountDto Permanente)> CarteraDeDepositosAsync()
    {
        var vista = await CuentaConSaldoAsync("AHVISTA", DepositClass.Demand, 20_000_000m);
        var linea = await LineaDeCdatAsync();
        var corto = await CdatAsync(linea, vista, 2_000_000m, 30);
        var medio = await CdatAsync(linea, vista, 3_000_000m, 180);
        var permanente = await CuentaConSaldoAsync("PERMANENTE", DepositClass.Permanent, 1_000_000m, CuentasDePrueba.ALaVista() with
        {
            AllowsPartialWithdrawals = false, Permanent = new PermanentLineInput(WithdrawableOnlyOnMembershipWithdrawal: true),
        });
        return (vista, corto, medio, permanente);
    }

    private async Task<Guid> SoporteAsync(Guid dueno)
    {
        var adjunto = new Domain.Entities.Core.Attachment
        {
            TenantId = 1, OwnerEntityType = AdjuntosDeModulo.SoporteDelFondoDeLiquidez, OwnerEntityPublicId = dueno, FileName = "extracto.pdf",
            ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = new string('a', 64), StoragePath = "x/" + Guid.NewGuid().ToString("N"),
            Status = EstadoDeAdjunto.Available,
        };
        _a.Db.Attachments.Add(adjunto);
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
        return adjunto.PublicId;
    }

    private async Task<LiquidityFundHoldingDto> InversionAsync(decimal valor, LiquidityHoldingKind clase = LiquidityHoldingKind.BankDeposit, DateOnly? desde = null)
    {
        var reservado = Guid.NewGuid();
        var r = await new RegisterLiquidityFundHoldingCommandHandler(_a.Db, G.Alcance, G.Actor, G.Reloj).Handle(new RegisterLiquidityFundHoldingCommand(clase,
            _a.C.Beto.PublicId, "CTA-998877", valor, desde ?? CuentasDePrueba.Hoy.AddDays(-5), null, await SoporteAsync(reservado), "Cuenta del fondo", reservado)
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _a.Db.ChangeTracker.Clear();
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<(decimal Capital, decimal Causado)> SaldoAsync(Guid cuenta)
    {
        var id = await _a.Db.MemberAccounts.AsNoTracking().Where(a => a.PublicId == cuenta).Select(a => a.Id).SingleAsync();
        var s = (await LectorDeCuentas.SaldosAsync(_a.Db, [id], default))[id];
        return (s.GetValueOrDefault(MemberAccountConcept.Principal), s.GetValueOrDefault(MemberAccountConcept.InterestPayable));
    }

    private async Task<decimal> InteresDelPlanAsync(Guid cdat) =>
        await (from v in _a.Db.MemberAccountTermsVersions.AsNoTracking()
               join a in _a.Db.MemberAccounts.AsNoTracking() on v.MemberAccountId equals a.Id
               join p in _a.Db.TermDepositScheduleItems.AsNoTracking() on v.Id equals p.TermsVersionId
               where a.PublicId == cdat
               select p.Interest).SumAsync();

    // --------------------------------------------------------------------------------------------------------- pruebas --

    /// <summary>Segunda ronda del 2026-10-27 (D-l, T0504): el último saldo que informó Tesorería hasta la fecha va al lado del constituido.</summary>
    [Fact]
    public async Task El_saldo_que_informo_Tesoreria_va_al_lado_del_constituido()
    {
        var hoy = G.Reloj.HoyLocal;
        foreach (var (dia, total) in new[] { (hoy.AddDays(-5), 1_000_000m), (hoy.AddDays(-1), 1_250_000m), (hoy.AddDays(3), 9_999_999m) })
            _a.Db.IntegrationMessages.Add(new IngenIA365ERP.Domain.Entities.Integration.Transactions.IntegrationMessage
            {
                Type = IngenIA365ERP.Application.Common.Integration.Contracts.Treasury.SaldoDelFondoDeLiquidezV1.Type, OriginModule = "TES", OperationDate = dia,
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new IngenIA365ERP.Application.Common.Integration.Contracts.Treasury.SaldoDelFondoDeLiquidezV1
                    { Date = dia, Total = total }, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)),
            });
        await _a.Db.SaveChangesAsync();

        var informado = await GetLiquidityFundPositionQueryHandler.SaldoInformadoPorTesoreriaAsync(_a.Db, hoy, default);

        informado!.Total.Should().Be(1_250_000m, "el último hasta la fecha, no uno posterior");
        informado.Date.Should().Be(hoy.AddDays(-1));
    }

    [Fact]
    public async Task US15_prueba_independiente_el_requerido_por_banda_es_el_del_calculo_manual()
    {
        var (vista, corto, medio, permanente) = await CarteraDeDepositosAsync();

        var r = await new GetLiquidityFundPositionQueryHandler(Fondo(), G.Alcance, G.Reloj, _a.Db).Handle(new GetLiquidityFundPositionQuery(null), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var p = r.Value;
        p.TreasuryReportedBalance.Should().BeNull("Tesorería todavía no informó el saldo del fondo");
        p.Category.Should().Be("Plena");
        p.Regime.Stage.Should().Be(nameof(RegimenDeFondoDeLiquidez.PorMadurez));

        // A mano: base = capital + intereses causados + intereses por causar del plan; 10 % a la vista y de 0 a 30 días, 5 % de 31 a 540 y en el permanente.
        var (capVista, cauVista) = await SaldoAsync(vista.PublicId);
        var (capCorto, cauCorto) = await SaldoAsync(corto.Account.PublicId);
        var (capMedio, cauMedio) = await SaldoAsync(medio.Account.PublicId);
        var (capPerm, cauPerm) = await SaldoAsync(permanente.PublicId);
        decimal Al(decimal b, decimal pct) => Math.Round(b * pct, 2, MidpointRounding.AwayFromZero);
        var baseCorto = capCorto + cauCorto + Math.Max(0m, await InteresDelPlanAsync(corto.Account.PublicId) - cauCorto);
        var baseMedio = capMedio + cauMedio + Math.Max(0m, await InteresDelPlanAsync(medio.Account.PublicId) - cauMedio);

        p.Base.Should().ContainSingle(b => b.DepositClass == "ALaVista").Which.Required.Should().Be(Al(capVista + cauVista, 0.10m));
        var bandaCorta = p.Base.Single(b => b.DepositClass == "Cdat" && b.Band == "0 a 30 días");
        bandaCorta.Base.Should().Be(baseCorto);
        bandaCorta.Required.Should().Be(Al(baseCorto, 0.10m));
        var bandaMedia = p.Base.Single(b => b.DepositClass == "Cdat" && b.Band == "31 a 540 días");
        bandaMedia.Base.Should().Be(baseMedio);
        bandaMedia.Required.Should().Be(Al(baseMedio, 0.05m));
        p.Base.Single(b => b.DepositClass == "PermanenteSoloAlRetirarse").Required.Should().Be(Al(capPerm + cauPerm, 0.05m));
        p.Required.Should().Be(p.Base.Sum(b => b.Required));
        p.Constituted.Should().Be(0m);
        p.Alert.Should().Be("Shortfall");
    }

    [Fact]
    public async Task En_transicion_rige_el_regimen_general_y_el_nuevo_se_informa()
    {
        Parametro(ParametrosDeCartera.FondoDeLiquidezPorcentajesPorCategoria, ParametrosDeCartera.DefectoDelFondoDeLiquidez, new DateOnly(2026, 6, 1));
        await CarteraDeDepositosAsync();

        var p = (await Fondo().PosicionAsync(CuentasDePrueba.Hoy, default)).Value;

        p.Etapa.Should().Be("Transicion");
        p.Calculo.Regimen.Should().Be(RegimenDeFondoDeLiquidez.General);
        p.Calculo.Renglones.Should().OnlyContain(x => x.Banda == null);
        p.Calculo.Renglones.Single(x => x.Clase == ClaseDeDepositoParaLiquidez.PermanenteSoloAlRetirarse).Porcentaje.Should().Be(0.02m);
        p.Calculo.RegimenNuevoEnTransicion.Should().NotBeNull();
        p.Calculo.FinDeLaTransicion.Should().Be(new DateOnly(2030, 2, 6));
    }

    [Fact]
    public async Task US15_1_constituido_menor_que_el_requerido_alerta_al_tesorero_y_a_la_gerencia_con_la_diferencia_y_una_foto_por_fecha()
    {
        await CarteraDeDepositosAsync();
        await InversionAsync(100_000m);

        var r = await new TakeLiquidityFundSnapshotCommandHandler(Fotos(), G.Alcance, G.Reloj)
            .Handle(new TakeLiquidityFundSnapshotCommand(null, "Corte del tesorero") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var foto = r.Value;
        foto.Constituted.Should().Be(100_000m);
        foto.Shortfall.Should().Be(foto.Required - 100_000m).And.BePositive();
        foto.AlertPublicId.Should().NotBeNull();
        await _alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(x => x.TypeCode == TiposDeAlerta.FondoDeLiquidezInsuficiente
                && x.Body.Contains(foto.Shortfall.ToString("N2")) && x.RecipientPermissions!.Contains("Lending.Decisions.Management")),
            Arg.Any<CancellationToken>());
        TiposDeAlerta.Buscar(TiposDeAlerta.FondoDeLiquidezInsuficiente)!.Destinatarios.Should().Contain("Lending.LiquidityFund.Manage");
        Application.Security.Roles.Plantillas.PerfilesSugeridos.DeCaptaciones().Concat(Application.Security.Roles.Plantillas.PerfilesSugeridos.DeCreditos())
            .Single(p => p.Key == "cartera.gerente").Patrones.Should().Contain("Lending.LiquidityFund.View", "la gerencia recibe la alerta (US15-1)");

        var otra = await new TakeLiquidityFundSnapshotCommandHandler(Fotos(), G.Alcance, G.Reloj)
            .Handle(new TakeLiquidityFundSnapshotCommand(null, "Otra vez") { OperationKey = Guid.NewGuid() }, default);
        otra.Value.PublicId.Should().Be(foto.PublicId, "una fecha tiene una sola foto");
        (await _a.Db.LiquidityFundSnapshots.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Cubierto_pero_sin_colchon_avisa_en_el_margen_y_cubierto_con_colchon_no_avisa()
    {
        await CuentaConSaldoAsync("AHVISTA", DepositClass.Demand, 10_000_000m);
        var requerido = (await Fondo().PosicionAsync(CuentasDePrueba.Hoy, default)).Value.Calculo.Requerido;
        var inversion = await InversionAsync(requerido);

        var margen = (await Fondo().PosicionAsync(CuentasDePrueba.Hoy, default)).Value;
        margen.Nivel.Should().Be(NivelDelFondoDeLiquidez.EnElMargen, "el colchón por defecto es del 5 %");

        await InversionAsync(requerido);
        (await Fondo().PosicionAsync(CuentasDePrueba.Hoy, default)).Value.Nivel.Should().Be(NivelDelFondoDeLiquidez.Cubierto);

        // Terminar la primera inversión la saca del constituido desde ese día (y no antes).
        var fin = await new EndLiquidityFundHoldingCommandHandler(_a.Db, G.Alcance, G.Actor, G.Reloj)
            .Handle(new EndLiquidityFundHoldingCommand(inversion.PublicId, CuentasDePrueba.Hoy, "Redimido el CDT") { OperationKey = Guid.NewGuid() }, default);
        fin.IsSuccess.Should().BeTrue(fin.IsFailure ? $"{fin.Error.Code}: {fin.Error.Message}" : string.Empty);
        (await Fondo().ConstituidoAsync(CuentasDePrueba.Hoy, default)).Should().Be(requerido);
        (await Fondo().ConstituidoAsync(CuentasDePrueba.Hoy.AddDays(-1), default)).Should().Be(requerido * 2);
        var otraVez = await new EndLiquidityFundHoldingCommandHandler(_a.Db, G.Alcance, G.Actor, G.Reloj)
            .Handle(new EndLiquidityFundHoldingCommand(inversion.PublicId, CuentasDePrueba.Hoy, "Otra vez") { OperationKey = Guid.NewGuid() }, default);
        otraVez.Error.Code.Should().Be(ErroresDelFondoDeLiquidez.CodigoYaTerminada);
    }

    [Fact]
    public async Task Sin_soporte_o_sin_entidad_no_se_registra_la_inversion_y_registrada_bloquea_su_soporte()
    {
        var sin = await new RegisterLiquidityFundHoldingCommandHandler(_a.Db, G.Alcance, G.Actor, G.Reloj).Handle(new RegisterLiquidityFundHoldingCommand(
            LiquidityHoldingKind.BankDeposit, _a.C.Beto.PublicId, "CTA-1", 1_000m, CuentasDePrueba.Hoy, null, null, null), default);
        sin.Error.Code.Should().Be(ErroresDelFondoDeLiquidez.CodigoSoporteRequerido);

        var reservado = Guid.NewGuid();
        var entidad = await new RegisterLiquidityFundHoldingCommandHandler(_a.Db, G.Alcance, G.Actor, G.Reloj).Handle(new RegisterLiquidityFundHoldingCommand(
            LiquidityHoldingKind.BankDeposit, Guid.NewGuid(), "CTA-1", 1_000m, CuentasDePrueba.Hoy, null, await SoporteAsync(reservado), null, reservado), default);
        entidad.Error.Code.Should().Be(ErroresDelFondoDeLiquidez.CodigoInvalido);

        var inversion = await InversionAsync(1_000m);
        var permisos = Substitute.For<Application.Payroll.Services.IPermissionChecker>();
        permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        (await AdjuntosDeModulo.PuedeBorrarAsync(_a.Db, AdjuntosDeModulo.SoporteDelFondoDeLiquidez, inversion.PublicId, default, permisos))!
            .Code.Should().Be(AttachmentErrorCodes.OwnerLocked);
    }

    [Fact]
    public async Task Sin_categoria_en_el_perfil_la_posicion_responde_ProfileCategoryMissing()
    {
        Perfil(null);

        var r = await Fondo().PosicionAsync(CuentasDePrueba.Hoy, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDelFondoDeLiquidez.CodigoSinCategoria);
    }

    [Fact]
    public async Task El_IRL_descuenta_los_activos_por_su_clase_lleva_el_CDAT_a_su_vencimiento_y_alerta_bajo_el_minimo()
    {
        var (_, corto, _, _) = await CarteraDeDepositosAsync();
        await InversionAsync(1_000_000m, LiquidityHoldingKind.TermInvestment);

        var m = await Fondo().RiesgoAsync(CuentasDePrueba.Hoy, MedidaDeRiesgoDeLiquidez.Irl, default);

        m.Medicion.ActivosLiquidosNetos.Should().Be(800_000m, "el CDT del fondo entra con el 20 % de descuento");
        m.Medicion.Bandas.Select(b => b.HastaDias).Should().Equal(15, 30, 60, 90);
        var (capCorto, _) = await SaldoAsync(corto.Account.PublicId);
        m.Medicion.Bandas[1].SalidasContractuales.Should().BeGreaterThanOrEqualTo(capCorto, "el CDAT a 30 días vence en la segunda banda");
        m.Medicion.Bandas[0].SalidasNoContractuales.Should().BePositive("la fracción del ahorro a la vista sale en la primera banda");
        m.Medicion.IrlMinimo.Should().BeLessThan(1m);
        m.BajoElUmbral.Should().BeTrue();

        var foto = await Fotos().TomarAsync(CuentasDePrueba.Hoy, null, null, default);
        foto.Value.Foto.MinimumIrl.Should().Be(m.Medicion.IrlMinimo);
        await _alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(x => x.TypeCode == TiposDeAlerta.RiesgoDeLiquidezBajo), Arg.Any<CancellationToken>());

        var brecha = await Fondo().RiesgoAsync(CuentasDePrueba.Hoy, MedidaDeRiesgoDeLiquidez.Brecha, default);
        brecha.Medicion.Bandas[^1].HastaDias.Should().BeNull("la brecha termina en la banda abierta");
        brecha.Medicion.FueraDelHorizonte.Should().Be(0m);
    }

    [Fact]
    public async Task Los_depositos_asegurados_van_por_ahorrador_hasta_el_tope_y_sin_aportes()
    {
        G.Valores.LeerAsync(CodigosDeValorNacional.TopeSeguroDepositos, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.TopeSeguroDepositos, string.Empty, 15_000_000m,
                "Pesos", null, new DateOnly(2024, 8, 19), null, null, "Fogacoop (prueba)", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        await CarteraDeDepositosAsync();

        var tabla = await new InsuredDepositsReportQueryHandler(_a.Db, Fondo(), G.Alcance, G.Reloj).Handle(new InsuredDepositsReportQuery(new FiltrosDeInformeDeCartera()),
            default);

        tabla.IsSuccess.Should().BeTrue(tabla.IsFailure ? $"{tabla.Error.Code}: {tabla.Error.Message}" : string.Empty);
        var fila = tabla.Value.Filas.Should().ContainSingle().Subject;
        ((int)fila.Valores[2]!).Should().Be(4, "la vista, los dos CDAT y el permanente de la misma titular");
        ((decimal)fila.Valores[3]!).Should().BeGreaterThan(15_000_000m);
        ((decimal)fila.Valores[4]!).Should().Be(15_000_000m);
        ((decimal)fila.Valores[5]!).Should().Be((decimal)fila.Valores[3]! - 15_000_000m);

        Perfil("Plena", asegurada: false);
        var sinSeguro = await new InsuredDepositsReportQueryHandler(_a.Db, Fondo(), G.Alcance, G.Reloj)
            .Handle(new InsuredDepositsReportQuery(new FiltrosDeInformeDeCartera()), default);
        sinSeguro.Value.Filas.Should().BeEmpty();
    }

    [Fact]
    public async Task El_saldo_de_una_cuenta_conjunta_se_reparte_entre_sus_titulares_para_el_seguro()
    {
        // Respuesta 101 C del dueño (E250): en partes iguales mientras Fogacoop no regule las cuentas conjuntas; el titular absorbe el residuo.
        _a.Db.MemberAccountParties.Add(new IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.MemberAccountParty { MemberAccountId = 777, PersonId = 2, Role = MemberAccountPartyRole.CoHolder, ValidFrom = CuentasDePrueba.Hoy.AddYears(-1) });
        _a.Db.MemberAccountParties.Add(new IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts.MemberAccountParty { MemberAccountId = 777, PersonId = 3, Role = MemberAccountPartyRole.CoHolder, ValidFrom = CuentasDePrueba.Hoy.AddYears(-1) });
        await _a.Db.SaveChangesAsync();
        var conjunta = new DepositoALaFecha(777, 1, 1, 1, ClaseDeDepositoParaLiquidez.ALaVista, 1_000_000m, 0m, 0m, null, null, false, []);

        var iguales = await Fondo().RepartirEntreTitularesAsync([conjunta], "PartesIguales", CuentasDePrueba.Hoy, default);
        var alTitular = await Fondo().RepartirEntreTitularesAsync([conjunta], "AlTitular", CuentasDePrueba.Hoy, default);

        iguales.Sum(x => x.Depositos).Should().Be(1_000_000m);
        iguales.Select(x => x.PersonaId).Should().BeEquivalentTo([1, 2, 3]);
        iguales.Single(x => x.PersonaId == 2).Depositos.Should().Be(333_333m);
        iguales.Single(x => x.PersonaId == 1).Depositos.Should().Be(333_334m, "el titular absorbe el residuo del redondeo a seis decimales de la proporción");
        alTitular.Should().ContainSingle().Which.Depositos.Should().Be(1_000_000m);
    }

    [Fact]
    public async Task El_ciclo_toma_la_foto_solo_en_los_dias_de_corte_de_la_entidad()
    {
        FondoDeLiquidezDelCiclo.EsDiaDeCorte("Mensual", new DateOnly(2026, 11, 30)).Should().BeTrue();
        FondoDeLiquidezDelCiclo.EsDiaDeCorte("Mensual", new DateOnly(2026, 12, 1)).Should().BeFalse();
        FondoDeLiquidezDelCiclo.EsDiaDeCorte("Semanal", new DateOnly(2026, 12, 6)).Should().BeTrue("es domingo");
        FondoDeLiquidezDelCiclo.EsDiaDeCorte("Diaria", new DateOnly(2026, 12, 2)).Should().BeTrue();

        await CuentaConSaldoAsync("AHVISTA", DepositClass.Demand, 1_000_000m);
        Parametro(ParametrosDeCartera.FondoDeLiquidezPeriodicidad, "Diaria");
        var corrida = new LendingProcessRun { Kind = LendingProcessKind.DailyCycle, CreatedBy = "test" };
        _a.Db.LendingProcessRuns.Add(corrida);
        await _a.Db.SaveChangesAsync();
        var ciclo = new FondoDeLiquidezDelCiclo(_a.Db, Fotos(), Fondo(), _a.C.Contexto);

        await ciclo.EjecutarAsync(corrida, CuentasDePrueba.Hoy, default);

        var foto = await _a.Db.LiquidityFundSnapshots.AsNoTracking().SingleAsync();
        foto.ProcessRunId.Should().Be(corrida.Id);
        foto.SnapshotDate.Should().Be(CuentasDePrueba.Hoy);

        _a.C.Entrega = EntregaDeCartera.K10;
        await ciclo.EjecutarAsync(corrida, CuentasDePrueba.Hoy.AddDays(1), default);
        (await _a.Db.LiquidityFundSnapshots.CountAsync()).Should().Be(1, "sin K11 desplegada el ciclo no toma la foto");
    }
}
