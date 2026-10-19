using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using static IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones.EscenarioDeCaptaciones;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones;

/// <summary>
/// Feature 014, US8 (T142; contracts/contabilidad.md §3 de la 014): la tabla hecho × rol de <see cref="OperacionesDeCartera"/> contra lo que el constructor
/// arma. Para cada uno de los 30 hechos de negocio de captaciones hay una muestra que se desglosa sin fallos (cuadra sola o con su contrapartida) y cuyas
/// partidas caen en roles que la tabla le reconoce al hecho: un rol que el constructor usa y la tabla no nombra dejaría a la contadora sin poder fijar el
/// hecho en una regla de resultado. Las muestras de K9 y K10 (CDAT, asamblea, no reclamados, embargos) prueban el constructor antes de que su historia los
/// emita.
/// </summary>
public class HechoPorRolDeCaptacionesTests
{
    private static readonly EscenarioDeCaptaciones S = new();

    public static IEnumerable<object[]> Tipos() => OperacionesDeCartera.DeCaptaciones.Where(o => !o.EsInformativo).Select(o => new object[] { o.Codigo });

    private static DepositFactLineV1 Linea(DepositFactDimensionsV1 d, params DepositConceptAmountV1[] valores) =>
        new() { Account = Cuenta("AHVISTA-1"), HolderPersonPublicId = S.Titular, Dimensions = d, Amounts = valores };

    /// <summary>Una muestra del contenido de cada hecho de negocio de captaciones, como lo emitiría su historia.</summary>
    public static object Muestra(string tipo)
    {
        var v = S.AlaVista();
        var a = S.Aportes();
        var t = S.Cdat();
        var cta = Cuenta("AHVISTA-1");
        var titular = S.Titular;
        var cobro = Recaudador("Receipts", S.Pagador);
        return tipo switch
        {
            ConsignacionEnCuentaAplicadaV1.Type => new ConsignacionEnCuentaAplicadaV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = v, Collector = cobro, Amounts = [Valor("Principal", 10m)] },
            AporteRecibidoV1.Type => new AporteRecibidoV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = a, Collector = cobro, Amounts = [Valor("ContributionOrdinary", 10m, claseDeAporte: "Ordinary")] },
            RetiroDeCuentaAplicadoV1.Type => new RetiroDeCuentaAplicadoV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = v, Collector = cobro, Amounts = [Valor("Principal", -10m)] },
            TrasladoEntreCuentasAplicadoV1.Type => new TrasladoEntreCuentasAplicadoV1
            {
                Account = cta, HolderPersonPublicId = titular, Dimensions = v, Amounts = [Valor("Principal", -10m)], Target = Cuenta("APORTES-1"),
                TargetHolderPersonPublicId = titular, TargetDimensions = a, TargetAmounts = [Valor("ContributionOrdinary", 10m, claseDeAporte: "Ordinary")],
            },
            GmfRetenidoV1.Type => new GmfRetenidoV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = v, Amounts = [Valor("Gmf", 1m)], Gmf = new GmfDetailV1 { GeneratingDepositClass = "Demand" } },
            InteresDeCaptacionCausadoV1.Type => new InteresDeCaptacionCausadoV1 { Lines = [Linea(v, Valor("InterestPayable", 1m))] },
            AjusteDeCausacionDeCaptacionV1.Type => new AjusteDeCausacionDeCaptacionV1 { Lines = [Linea(v, Valor("InterestPayable", -1m))] },
            InteresDeCaptacionLiquidadoV1.Type => new InteresDeCaptacionLiquidadoV1 { Lines = [Linea(v, Valor("InterestPayable", -5m), Valor("Principal", 5m))] },
            RetencionPracticadaV1.Type => new RetencionPracticadaV1 { Lines = [Linea(v, Valor("Principal", -1m), Valor("Withholding", 1m))] },
            CdatConstituidoV1.Type => new CdatConstituidoV1
            {
                Account = cta, HolderPersonPublicId = titular, Dimensions = t, Amounts = [Valor("Principal", 100m)],
                Funding = new TermDepositFundingV1 { Kind = "Collector", Collector = cobro },
            },
            InteresesDeCdatPagadosV1.Type => new InteresesDeCdatPagadosV1
            {
                Account = cta, HolderPersonPublicId = titular, Dimensions = t, Amounts = [Valor("InterestPayable", -3m)],
                Destination = new TermDepositDestinationV1 { Kind = "PayoutOrder", OrderPublicId = Guid.NewGuid() },
            },
            CdatProrrogadoV1.Type => new CdatProrrogadoV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = t, Amounts = [Valor("InterestPayable", -3m), Valor("Principal", 3m)] },
            CdatRenovadoV1.Type => new CdatRenovadoV1
            {
                Account = cta, HolderPersonPublicId = titular, Dimensions = t, Amounts = [Valor("InterestPayable", -3m), Valor("Principal", 13m)],
                AmountChange = new TermDepositAmountChangeV1 { Kind = "Increase", Amount = 10m, Funding = new TermDepositFundingV1 { Kind = "Collector", Collector = cobro } },
            },
            CdatRedimidoV1.Type => new CdatRedimidoV1
            {
                Account = cta, HolderPersonPublicId = titular, Dimensions = t, Amounts = [Valor("Principal", -100m), Valor("InterestPayable", -3m)],
                Destination = new TermDepositDestinationV1 { Kind = "PayoutOrder", OrderPublicId = Guid.NewGuid() },
            },
            CdatCanceladoV1.Type => new CdatCanceladoV1
            {
                Account = cta, HolderPersonPublicId = titular, Dimensions = t, Amounts = [Valor("Principal", -100m)],
                Destination = new TermDepositDestinationV1 { Kind = "PayoutOrder", OrderPublicId = Guid.NewGuid() },
            },
            PenalidadAplicadaV1.Type => new PenalidadAplicadaV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = t, Amounts = [Valor("InterestPayable", -1m)] },
            CaptacionReclasificadaV1.Type => new CaptacionReclasificadaV1
            {
                Account = cta, HolderPersonPublicId = titular, PreviousDimensions = v, Dimensions = S.AlaVista(inactiva: true), Amounts = [Valor("Principal", 10m)],
            },
            BonificacionReconocidaV1.Type => new BonificacionReconocidaV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = v, Amounts = [Valor("Bonus", 2m)] },
            CargoDeCuentaCausadoV1.Type => new CargoDeCuentaCausadoV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = v, Charge = new AccountChargeRefV1 { Kind = "MaintenanceFee" }, Amounts = [Valor("Principal", -1m)] },
            CargoPendienteCobradoV1.Type => new CargoPendienteCobradoV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = v, Amounts = [Valor("ChargeReceivable", -1m), Valor("Principal", -1m)] },
            CargoDeCuentaCondonadoV1.Type => new CargoDeCuentaCondonadoV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = v, Charge = new AccountChargeRefV1 { Kind = "MaintenanceFee" }, Amounts = [Valor("ChargeReceivable", -1m)] },
            RevalorizacionDeAportesAplicadaV1.Type => new RevalorizacionDeAportesAplicadaV1 { Lines = [Linea(a, Valor("ContributionRevaluation", 4m, claseDeAporte: "Revalued"))] },
            ExcedentesDistribuidosV1.Type => new ExcedentesDistribuidosV1 { Lines = [Linea(a, Valor("ContributionOrdinary", 4m, claseDeAporte: "Ordinary"))] },
            AportesAmortizadosV1.Type => new AportesAmortizadosV1 { Lines = [Linea(a, Valor("ContributionOrdinary", -4m, claseDeAporte: "Ordinary"))] },
            CruceAlRetiroAplicadoV1.Type => new CruceAlRetiroAplicadoV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = a, Amounts = [Valor("ContributionOrdinary", -4m, claseDeAporte: "Ordinary")] },
            PerdidaAplicadaAlRetiroV1.Type => new PerdidaAplicadaAlRetiroV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = a, Amounts = [Valor("ContributionOrdinary", -4m, claseDeAporte: "Ordinary")] },
            DevolucionOrdenadaV1.Type => new DevolucionOrdenadaV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = a, OrderKind = "MembershipRefund", PayeePersonPublicId = titular, Amounts = [Valor("ContributionOrdinary", -4m, claseDeAporte: "Ordinary")] },
            SaldoTrasladadoANoReclamadosV1.Type => new SaldoTrasladadoANoReclamadosV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = v, Amounts = [Valor("Principal", -4m)] },
            EmbargoConsignadoV1.Type => new EmbargoConsignadoV1 { Account = cta, HolderPersonPublicId = titular, Dimensions = v, Amounts = [Valor("Principal", -4m)], Garnishment = new GarnishmentRefV1 { CourtPersonPublicId = S.Pagador } },
            OperacionDeCuentaReversadaV1.Type => new OperacionDeCuentaReversadaV1
            {
                Account = cta, HolderPersonPublicId = titular, Dimensions = v, CollectorClass = "Receipts", Original = new OriginalFactRefV1 { Type = ConsignacionEnCuentaAplicadaV1.Type },
                Mirrors = [new DepositFactMirrorV1 { Type = ConsignacionEnCuentaAplicadaV1.Type, Amounts = [Valor("Principal", -10m)] }],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Sin muestra: agregue una al sumar un hecho de captaciones."),
        };
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public void Cada_hecho_de_negocio_se_desglosa_cuadrado_y_en_roles_que_la_tabla_le_reconoce(string tipo)
    {
        var unidad = S.Unidad("Prueba", "N-1", Muestra(tipo));

        var desglose = ConstructorDeLineasDeCartera.Desglosar(unidad);

        desglose.Fallos.Select(f => $"{f.Error.Code}: {f.Error.Message}").Should().BeEmpty(tipo);
        desglose.Partidas.Should().NotBeEmpty(tipo);
        var operacion = OperacionesDeCartera.Buscar(tipo)!;
        desglose.Partidas.Select(p => p.Rol).Distinct().Should().OnlyContain(r => operacion.TieneRol(r), $"{tipo}: la tabla hecho × rol nombra los roles que arma");
        desglose.Partidas.Should().OnlyContain(p => decimal.Round(p.Importe, 2) == p.Importe);
        desglose.Partidas.Where(p => RolesDeCuentaDeCartera.Buscar(p.Rol)!.Cruce == CruceDeRol.Cuenta)
            .Should().OnlyContain(p => p.Cuenta != null, "las líneas de los roles de cuenta llevan el número de la cuenta (cruce CTA)");
    }

    [Fact]
    public void Las_muestras_cubren_los_treinta_hechos_de_negocio_de_captaciones()
    {
        Tipos().Should().HaveCount(30);
        OperacionesDeCartera.DeCaptaciones.Should().HaveCount(33);
    }

    [Fact]
    public void Los_hechos_de_captaciones_que_mueven_el_capital_admiten_el_de_la_cuenta_inactiva()
    {
        foreach (var tipo in new[] { GmfRetenidoV1.Type, InteresDeCaptacionLiquidadoV1.Type, RetencionPracticadaV1.Type, CargoDeCuentaCausadoV1.Type,
                     CargoPendienteCobradoV1.Type, CargoDeCuentaCondonadoV1.Type, EmbargoConsignadoV1.Type, RetiroDeCuentaAplicadoV1.Type })
            OperacionesDeCartera.Buscar(tipo)!.TieneRol(RolesDeCuentaDeCartera.CapitalDeCaptacionInactiva).Should().BeTrue(tipo);
        OperacionesDeCartera.Buscar(OperacionDeCuentaReversadaV1.Type)!.Acompanantes.Should().Contain(PagoReversadoV1.Type,
            "D45: el reverso del cruce y el del pago pueden ir en un solo comprobante");
    }
}
