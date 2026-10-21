using FluentAssertions;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Deposits.Accrual;
using IngenIA365ERP.Application.Lending.Deposits.Lines;
using IngenIA365ERP.Application.Lending.Deposits.Liquidity;
using IngenIA365ERP.Application.Lending.Deposits.Reports;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits;
using IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;
using IngenIA365ERP.Application.Lending.Regulatory;
using IngenIA365ERP.Application.Lending.Regulatory.Captaciones;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Reports;

/// <summary>
/// Feature 014, US17 (T219–T221, T223; FR-107, FR-111, FR-112; research §25): los formatos regulatorios de captaciones sobre el libro real —la prueba
/// independiente (el formato de captaciones de un mes cuadra con los saldos del libro y sale con la advertencia «formato sin cotejar», que hay que reconocer
/// para generar), regenerar deja la versión anterior reemplazada y lo cargado no se regenera, sin cierre del mes no hay formato, con alcance parcial tampoco,
/// los saldos diarios terminan en el capital del ahorro a la vista, la exógena 1019 respeta el umbral en UVT y la 1020 lleva el CDAT del mes, el fondo de
/// liquidez lleva sus inversiones, y las cuentas por estado—. Sobre <see cref="AportesDePrueba"/> (hoy 2026-12-01). (nuevo)
/// </summary>
public sealed class InformesDeCaptacionesTests : IDisposable
{
    private const decimal Uvt = 49_799m;
    private readonly AportesDePrueba _a = new();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly List<UploadAttachmentCommand> _subidas = [];

    public InformesDeCaptacionesTests()
    {
        _a.C.Entrega = EntregaDeCartera.K11;
        _a.C.EntregaDeParametros = EntregaDeCartera.K11;
        G.Valores.LeerAsync(CodigosDeValorNacional.Uvt, Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorNacionalVigente(Guid.NewGuid(), Guid.NewGuid(), CodigosDeValorNacional.Uvt, string.Empty, Uvt, "Pesos", null,
                new DateOnly(2026, 1, 1), null, null, "Resolución DIAN de prueba", 1, Sessions.GobiernoDePrueba.Ahora.AddYears(-1), EstadoDeRevision.Validada)));
        G.Limite(CatalogoDeLimitesLegales.Exogena1019UmbralUvt, 24m, "Resolución DIAN 000162 de 2023");
        _a.C.Cerrojo.BloquearCuentasAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<ModoDeEspera>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult<IReadOnlyList<int>>(ci.ArgAt<IReadOnlyCollection<int>>(0).ToList()));
        _sender.Send(Arg.Do<UploadAttachmentCommand>(_subidas.Add), Arg.Any<CancellationToken>()).Returns(_ => Result.Success(Guid.NewGuid()));
        _a.Cdat = Constitucion;
        G.Perfil.VigenteAsync(Arg.Any<DateOnly>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns(new PerfilRegulatorioDto(Guid.NewGuid(), new DateOnly(2026, 1, 1), "CAC", true, 1, null, 2, false, null, null, "Resolución de prueba", null,
                "Perfil de prueba", null, "Prueba", Sessions.GobiernoDePrueba.Ahora.AddYears(-1), MayTakeDeposits: true, LiquidityFundCategory: "Plena"));
        G.ActuarComo(_a.C.Jefe);
    }

    public void Dispose() => _a.Dispose();

    // ---------------------------------------------------------------------------------------------------------- piezas --

    private Sessions.GobiernoDePrueba G => _a.G;

    private FondoDeLiquidezDeCartera Fondo() => new(_a.Db, G.Perfil, _a.C.Parametros(), G.Valores);

    private ProyeccionDeCaptaciones Proyeccion() => new(_a.Db, Fondo(), G.Valores, G.Perfil, new LectorDeDocumentoNormativo(_a.Db, _a.Alertas));

    private PreparacionDeArchivoRegulatorio Preparacion() => new(CatalogoDeLayoutsDeCartera.Embebido, null!, null!, Proyeccion());

    private GeneracionDeArchivoRegulatorio Generacion() => new(_a.Db, Preparacion(), _a.Numerador(), _sender, _a.Alertas, G.Auditoria, G.Actor, G.Reloj);

    private Task<Result<RegulatoryValidationDto>> ValidarAsync(string formato, int anio, int? mes) =>
        new ValidateDepositRegulatoryFileQueryHandler(Preparacion(), G.Alcance).Handle(new ValidateDepositRegulatoryFileQuery(formato, anio, mes), default);

    private async Task<Result<RegulatoryFileDetailDto>> GenerarAsync(string formato, int anio, int? mes, bool reconocer)
    {
        var r = await new GenerateDepositRegulatoryFileCommandHandler(Generacion(), G.Alcance)
            .Handle(new GenerateDepositRegulatoryFileCommand(formato, anio, mes, reconocer) { OperationKey = Guid.NewGuid() }, default);
        _a.Db.ChangeTracker.Clear();
        return r;
    }

    private Task<Result<TablaExportable>> VistaPreviaAsync(string formato, int anio, int? mes) =>
        new DepositRegulatoryPreviewReportQueryHandler(Preparacion(), G.Alcance, G.Reloj)
            .Handle(new DepositRegulatoryPreviewReportQuery(new FiltrosDeInformeDeCartera(), formato, anio, mes), default);

    private int _corrida = 9100;

    private async Task CierreAsync(int anio, int mes)
    {
        _a.Db.MonthlyCloses.Add(new MonthlyClose
        {
            Kind = MonthlyCloseKind.Monthly, PeriodYear = (short)anio, PeriodMonth = (byte)mes, Version = 1, CloseDate = MonthlyClose.UltimoDiaDe(anio, mes),
            ProcessRunId = ++_corrida,
        });
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();
    }

    private async Task<MemberAccountDto> CuentaConSaldoAsync(string codigo, DepositClass clase, decimal saldo, Domain.Entities.Core.Person? titular = null,
        DepositLineVersionInput? version = null)
    {
        version ??= clase == DepositClass.Permanent
            ? CuentasDePrueba.ALaVista() with { AllowsPartialWithdrawals = false, Permanent = new PermanentLineInput(WithdrawableOnlyOnMembershipWithdrawal: true) }
            : CuentasDePrueba.ALaVista();
        var linea = await _a.C.LineaDeCaptacionAsync(codigo, version, clase);
        var cuenta = await _a.C.AbiertaAsync(_a.C.Apertura(linea, titular ?? _a.C.Ana));
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, saldo, ModalidadDeConsignacion.Free, medio: ClaseDeMedioDeRecaudo.Transfer));
        return cuenta;
    }

    private LectorDeCdat Lector() => new(_a.Db, _a.Calendario, _a.LectorDeInteres());

    private NegociacionDeTasa Negociacion() => new(_a.Db, new MatrizVigenteDeAtribuciones(_a.Db), new AtribucionDelDecisor(_a.Db, G.Permisos), G.Reloj);

    private CotizacionDeCdat Cotizacion() => new(_a.Db, Lector(), Negociacion(), new ReglasDeRetencion(_a.C.Parametros(), G.Valores));

    private ConstitucionDeCdat Constitucion() => new(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.Movimientos(), _a.Retiro(), Lector(),
        _a.C.Cerrojo, G.Actor, G.Reloj);

    private AperturaDeCuentas Apertura() => new(_a.Db, _a.C.Contexto, _a.C.Titularidad(), _a.C.Parametros(), _a.C.Listas, G.Intervenciones(),
        _a.RegistroDeCuentas(), _a.Numerador(), _a.C.Cerrojo, G.Alcance, G.Auditoria, G.Reloj);

    private async Task<TermDepositDto> CdatAsync(MemberAccountDto ahorro, decimal monto, int plazo)
    {
        var linea = await _a.C.LineaDeCaptacionAsync("CDAT", new DepositLineVersionInput
        {
            ValidFrom = CuentasDePrueba.Hoy,
            Reason = "Reglamento de CDAT, acta 30",
            Rate = new DepositRateInput { Kind = RateKind.Fixed, EffectiveAnnual = 0.10m },
            DayCount = DayCountConvention.Actual360,
            Minimums = new DepositMinimumsInput(1_000_000m),
            SettlementPeriodicity = RepaymentPeriodicity.SinglePayment,
            TermDeposit = new TermDepositLineInput
            {
                MinTermDays = 30, MaxTermDays = 720, InterestPeriodicities = [RepaymentPeriodicity.SinglePayment], Timings = [InterestTiming.Arrears],
            },
        }, DepositClass.TermDeposit);
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

    // --------------------------------------------------------------------------------------------------------- pruebas --

    [Fact]
    public async Task US17_prueba_independiente_el_formato_de_captaciones_cuadra_con_el_libro_y_sale_con_formato_sin_cotejar()
    {
        await CuentaConSaldoAsync("AHVISTA", DepositClass.Demand, 20_000_000m);
        await CuentaConSaldoAsync("PERMANENTE", DepositClass.Permanent, 1_500_000m, _a.C.Beto);
        await CierreAsync(2026, 12);

        var v = await ValidarAsync(FormatosDeCaptaciones.Captaciones, 2026, 12);

        v.IsSuccess.Should().BeTrue(v.IsFailure ? $"{v.Error.Code}: {v.Error.Message}" : string.Empty);
        v.Value.Rows.Should().Be(2);
        v.Value.Blocking.Should().Be(0, string.Join(" | ", v.Value.Issues.Select(i => i.Message)));
        v.Value.Warnings.Should().Contain(w => w.Code == AdvertenciasRegulatorias.LayoutSinCotejar);

        // Cuadra con los saldos del libro al cierre del mes (la vista de saldos de captaciones).
        var saldos = await new DepositBalancesReportQueryHandler(_a.Db, G.Alcance, G.Reloj)
            .Handle(new DepositBalancesReportQuery(new FiltrosDeInformeDeCartera { AsOf = new DateOnly(2026, 12, 31) }), default);
        var capitalDelLibro = (decimal)saldos.Value.Totales!.Valores[2]!;
        var sinCotejar = await GenerarAsync(FormatosDeCaptaciones.Captaciones, 2026, 12, reconocer: false);
        sinCotejar.IsFailure.Should().BeTrue();
        sinCotejar.Error.Code.Should().Be(AdvertenciasRegulatorias.LayoutSinCotejar, "el formato sin cotejar se reconoce antes de generar (US17-1)");

        var g = await GenerarAsync(FormatosDeCaptaciones.Captaciones, 2026, 12, reconocer: true);

        g.IsSuccess.Should().BeTrue(g.IsFailure ? $"{g.Error.Code}: {g.Error.Message}" : string.Empty);
        g.Value.File.Status.Should().Be(RegulatoryFileStatus.Generated);
        g.Value.File.Kind.Should().Be(RegulatoryFileKind.DepositsSicses);
        g.Value.File.FormatCode.Should().Be(FormatosDeCaptaciones.Captaciones);
        g.Value.File.FileName.Should().Contain("_SICSES-CAPTACIONES_2026-12_v1.txt");
        g.Value.File.Warnings.Should().Contain(w => w.Code == AdvertenciasRegulatorias.LayoutSinCotejar);
        g.Value.Totals.GetProperty("Totales.Capital").GetDecimal().Should().Be(capitalDelLibro);
        var texto = System.Text.Encoding.UTF8.GetString(_subidas.Single().Content);
        texto.Should().Contain("|20000000.00|").And.Contain("|1500000.00|");
        texto.Should().NotContain("SIN COTEJAR", "la advertencia nunca va dentro del archivo");
    }

    [Fact]
    public async Task Regenerar_deja_la_anterior_reemplazada_y_lo_cargado_no_se_regenera()
    {
        await CuentaConSaldoAsync("AHVISTA", DepositClass.Demand, 5_000_000m);
        await CierreAsync(2026, 12);
        var v1 = (await GenerarAsync(FormatosDeCaptaciones.SaldosDiarios, 2026, 12, true)).Value;
        var v2 = (await GenerarAsync(FormatosDeCaptaciones.SaldosDiarios, 2026, 12, true)).Value;
        v2.File.Version.Should().Be(2);
        (await _a.Db.RegulatoryFiles.SingleAsync(f => f.PublicId == v1.File.PublicId)).Status.Should().Be(RegulatoryFileStatus.Superseded);

        // El de otro formato del mismo mes no compite: su versión empieza en 1.
        (await GenerarAsync(FormatosDeCaptaciones.Captaciones, 2026, 12, true)).Value.File.Version.Should().Be(1);

        var cargado = await new MarkRegulatoryFileUploadedCommandHandler(_a.Db, G.Auditoria, G.Actor, G.Reloj)
            .Handle(new MarkRegulatoryFileUploadedCommand(v2.File.PublicId, "RAD-2027-0001", G.UtcNow) { OperationKey = Guid.NewGuid() }, default);
        cargado.IsSuccess.Should().BeTrue();
        _a.Db.ChangeTracker.Clear();
        var otra = await GenerarAsync(FormatosDeCaptaciones.SaldosDiarios, 2026, 12, true);
        otra.Error.Code.Should().Be(ErroresDeArchivosRegulatorios.CodigoYaCargado);

        var lista = await new ListRegulatoryFilesQueryHandler(_a.Db).Handle(new ListRegulatoryFilesQuery(Format: FormatosDeCaptaciones.SaldosDiarios), default);
        lista.Value.Items.Should().HaveCount(2).And.OnlyContain(f => f.FormatCode == FormatosDeCaptaciones.SaldosDiarios);
    }

    [Fact]
    public async Task Sin_cierre_del_mes_o_con_alcance_parcial_no_hay_formato_y_uno_desconocido_se_nombra()
    {
        await CuentaConSaldoAsync("AHVISTA", DepositClass.Demand, 5_000_000m);

        (await ValidarAsync(FormatosDeCaptaciones.Captaciones, 2026, 12)).Error.Code.Should().Be(ErroresDeFormatosDeCaptaciones.CodigoPeriodoSinCerrar);
        (await ValidarAsync(FormatosDeCaptaciones.Exogena1019, 2026, null)).Error.Code.Should().Be(ErroresDeFormatosDeCaptaciones.CodigoPeriodoSinCerrar,
            "la exógena exige el año cerrado (el cierre de diciembre)");
        (await ValidarAsync("sicses-otro", 2026, 12)).Error.Code.Should().Be(ErroresDeFormatosDeCaptaciones.CodigoFormatoDesconocido);
        (await ValidarAsync(FormatosDeCaptaciones.Captaciones, 2026, null)).IsFailure.Should().BeTrue("el SICSES es mensual");

        await CierreAsync(2026, 12);
        G.AlcanceActual = AlcanceDeCartera.Vacio with { Oficinas = new HashSet<int> { _a.C.Oficina.Id }, TodasLasLineas = true };
        (await ValidarAsync(FormatosDeCaptaciones.Captaciones, 2026, 12)).Error.Code.Should().Be(ErroresDeFormatosDeCaptaciones.CodigoSinAlcanceTotal);
        (await VistaPreviaAsync(FormatosDeCaptaciones.Captaciones, 2026, 12)).Error.Code.Should().Be(ErroresDeFormatosDeCaptaciones.CodigoSinAlcanceTotal);
    }

    [Fact]
    public async Task Los_saldos_diarios_terminan_en_el_capital_del_ahorro_a_la_vista_y_la_vista_previa_muestra_el_archivo()
    {
        await CuentaConSaldoAsync("AHVISTA", DepositClass.Demand, 7_250_000m);
        await CuentaConSaldoAsync("PERMANENTE", DepositClass.Permanent, 1_000_000m, _a.C.Beto);
        await CierreAsync(2026, 12);

        var t = await VistaPreviaAsync(FormatosDeCaptaciones.SaldosDiarios, 2026, 12);

        t.IsSuccess.Should().BeTrue(t.IsFailure ? $"{t.Error.Code}: {t.Error.Message}" : string.Empty);
        t.Value.Columnas.Select(c => c.Nombre).Should().Equal("Fecha", "Valor");
        t.Value.Filas.Should().HaveCount(31);
        t.Value.Filas[0].Valores.Should().Equal("01/12/2026", "7250000.00");
        // El permanente no es ahorro ordinario: no entra.
        t.Value.Filas[^1].Valores.Should().Equal("31/12/2026", "7250000.00");
        t.Value.Notas.Should().Contain(n => n.Contains("sin cotejar", StringComparison.Ordinal));
    }

    [Fact]
    public async Task La_exogena_1019_lleva_a_quien_llega_al_umbral_con_las_cifras_del_anio()
    {
        await CuentaConSaldoAsync("AHVISTA", DepositClass.Demand, 3_000_000m);
        await CuentaConSaldoAsync("PERMANENTE", DepositClass.Permanent, 100_000m, _a.C.Beto);
        await CierreAsync(2026, 12);

        var v = await ValidarAsync(FormatosDeCaptaciones.Exogena1019, 2026, null);
        var t = await VistaPreviaAsync(FormatosDeCaptaciones.Exogena1019, 2026, null);

        v.IsSuccess.Should().BeTrue(v.IsFailure ? $"{v.Error.Code}: {v.Error.Message}" : string.Empty);
        v.Value.Rows.Should().Be(1, "Beto no llega a 24 UVT ni en saldo ni en créditos del mes");
        var fila = t.Value.Filas.Single();
        var columnas = t.Value.Columnas.Select(c => c.Nombre).ToList();
        string Valor(string columna) => (string)fila.Valores[columnas.IndexOf(columna)]!;
        Valor("Número de identificación").Should().Be(_a.C.Ana.TaxId);
        Valor("Saldo final").Should().Be("3000000");
        Valor("Total de movimientos crédito").Should().Be("3000000");
        Valor("Número de movimientos crédito").Should().Be("1");
        Valor("Saldo máximo").Should().Be("3000000");
        Valor("Calidad del titular (1 principal, 2 secundario)").Should().Be("1");
        t.Value.Subtitulo.Should().StartWith("Año 2026");
    }

    [Fact]
    public async Task La_exogena_1020_lleva_la_emision_del_cdat_en_su_mes_y_el_fondo_sus_saldos_por_clase()
    {
        var ahorro = await CuentaConSaldoAsync("AHVISTA", DepositClass.Demand, 10_000_000m);
        await CdatAsync(ahorro, 2_000_000m, 90);
        await CierreAsync(2026, 12);

        var t = await VistaPreviaAsync(FormatosDeCaptaciones.Exogena1020, 2026, null);

        t.IsSuccess.Should().BeTrue(t.IsFailure ? $"{t.Error.Code}: {t.Error.Message}" : string.Empty);
        var fila = t.Value.Filas.Single();
        var columnas = t.Value.Columnas.Select(c => c.Nombre).ToList();
        string Valor(string columna) => (string)fila.Valores[columnas.IndexOf(columna)]!;
        Valor("Mes").Should().Be("12");
        Valor("Tipo de movimiento").Should().Be("1", "se constituyó en diciembre");
        Valor("Valor de la inversión").Should().Be("2000000");
        Valor("Saldo final").Should().Be("2000000");

        var fondo = await ValidarAsync(FormatosDeCaptaciones.FondoDeLiquidez, 2026, 12);
        fondo.IsSuccess.Should().BeTrue(fondo.IsFailure ? $"{fondo.Error.Code}: {fondo.Error.Message}" : string.Empty);
        fondo.Value.Blocking.Should().Be(0);
        var previa = await VistaPreviaAsync(FormatosDeCaptaciones.FondoDeLiquidez, 2026, 12);
        previa.Value.Notas.Should().Contain(n => n.StartsWith("Header: ", StringComparison.Ordinal) && n.Contains("ahorro a término = 2000000.00"),
            "el CDAT es el depósito a término (a la vista queda lo demás, menos el GMF del traslado)");
    }

    [Fact]
    public async Task Las_cuentas_por_estado_llevan_su_saldo_y_sus_marcas()
    {
        await CuentaConSaldoAsync("AHVISTA", DepositClass.Demand, 4_000_000m);
        var aportes = await _a.CuentaDeAportesAsync(_a.C.Beto);

        var r = await new DepositAccountsByStatusReportQueryHandler(_a.Db, G.Alcance, G.Reloj)
            .Handle(new DepositAccountsByStatusReportQuery(new FiltrosDeInformeDeCartera()), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Filas.Should().HaveCount(2);
        r.Value.Filas.Should().Contain(f => (string?)f.Valores[5] == "Activa" && (decimal)f.Valores[6]! == 4_000_000m);
        r.Value.Filas.Should().Contain(f => (string?)f.Valores[0] == aportes.Number);
        var soloInactivas = await new DepositAccountsByStatusReportQueryHandler(_a.Db, G.Alcance, G.Reloj)
            .Handle(new DepositAccountsByStatusReportQuery(new FiltrosDeInformeDeCartera(), "Inactive"), default);
        soloInactivas.Value.Filas.Should().BeEmpty();
        (await new DepositAccountsByStatusReportQueryHandler(_a.Db, G.Alcance, G.Reloj)
            .Handle(new DepositAccountsByStatusReportQuery(new FiltrosDeInformeDeCartera(), "Dormida"), default)).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Las_vistas_de_US17_se_registran_con_sus_datos_personales_y_su_entrega()
    {
        DepositAccountsByStatusReportQueryHandler.Vista.PersonalData.Should().BeTrue();
        DepositRegulatoryPreviewReportQueryHandler.Todas.Select(v => v.Key).Should().Equal(FormatosDeCaptaciones.Todos.Select(f => f.Codigo));
        DepositRegulatoryPreviewReportQueryHandler.SicsesSaldosDiarios.PersonalData.Should().BeFalse();
        DepositRegulatoryPreviewReportQueryHandler.SicsesFondoDeLiquidez.PersonalData.Should().BeFalse();
        DepositRegulatoryPreviewReportQueryHandler.Exogena1019.PersonalData.Should().BeTrue();
        DepositRegulatoryPreviewReportQueryHandler.Exogena1019.OwnFilters.Should().Equal("year");
        DepositRegulatoryPreviewReportQueryHandler.Todas.Should().OnlyContain(v => v.AvailableFrom == EntregaDeCartera.K11);
    }
}
