using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Integration;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending.Captaciones;

/// <summary>
/// Feature 014, US8 (T148): el escenario contable de los hechos de captaciones y aportes. Parte del de los casos dorados de la Cartera
/// (<see cref="EscenarioContableDeCartera"/>: tipos de comprobante y cruces sembrados —con <c>MA</c>, <c>CC</c>, <c>AO</c>, <c>RA</c> y <c>CTA</c>—, el mapeo
/// de la semilla, noviembre abierto, las personas y la matriz de crédito con sus puentes de recaudo por clase) y le suma un CUIF de ensayo de captaciones
/// con su matriz: capital a la vista activo, exento e inactivo, CDAT de 6 a 12 meses, interés por pagar, gasto, retención, GMF, aportes ordinarios y
/// mínimos, devoluciones por naturaleza, cargos y pérdidas. Los códigos son de ensayo, no del CUIF sembrado.
/// </summary>
public sealed class EscenarioDeCaptaciones
{
    public static readonly DateOnly Dia = new(2026, 11, 14);

    private const AccountingModules Car = AccountingModules.Accounting | AccountingModules.Lending;

    public EscenarioContableDeCartera E { get; } = new();

    public ChartOfAccount Ahorro { get; }
    public ChartOfAccount AhorroExenta { get; }
    public ChartOfAccount AhorroInactiva { get; }
    public ChartOfAccount Cdat612 { get; }
    public ChartOfAccount InteresPorPagar { get; }
    public ChartOfAccount Gasto { get; }
    public ChartOfAccount Retencion { get; }
    public ChartOfAccount Gmf { get; }
    public ChartOfAccount AportesOrdinarios { get; }
    public ChartOfAccount AportesMinimos { get; }
    public ChartOfAccount DevolucionDeDepositos { get; }
    public ChartOfAccount DevolucionDeAportes { get; }
    public ChartOfAccount IngresoPorCargos { get; }
    public ChartOfAccount CargoPorCobrar { get; }
    public ChartOfAccount Perdidas { get; }

    /// <summary>Los puentes de recaudo del escenario de crédito (por clase).</summary>
    public ChartOfAccount PuenteCaja => E.Cuenta("11059501");
    public ChartOfAccount PuenteNomina => E.Cuenta("25059501");
    public ChartOfAccount PuenteInterno => E.Cuenta("24959501");

    public Guid Titular => E.Personas["ana"].PublicId;
    public Guid Otro => E.Personas["luis"].PublicId;
    public Guid Pagador => E.Personas["pagador"].PublicId;
    public Guid Sucursal => E.Sucursales["principal"].PublicId;

    public EscenarioDeCaptaciones()
    {
        var d = E.D;
        Ahorro = Cuenta("21050591", AccountNature.Credit, tercero: true, cruce: true);
        AhorroExenta = Cuenta("21050592", AccountNature.Credit, tercero: true, cruce: true);
        AhorroInactiva = Cuenta("21051091", AccountNature.Credit, tercero: true, cruce: true);
        Cdat612 = Cuenta("21101091", AccountNature.Credit, tercero: true, cruce: true);
        InteresPorPagar = Cuenta("21059591", AccountNature.Credit, tercero: true, cruce: true);
        Gasto = Cuenta("61500591", AccountNature.Debit);
        Retencion = Cuenta("24353591", AccountNature.Credit);
        Gmf = Cuenta("24300591", AccountNature.Credit);
        AportesOrdinarios = Cuenta("31050591", AccountNature.Credit, tercero: true, cruce: true);
        AportesMinimos = Cuenta("31100091", AccountNature.Credit, tercero: true);
        DevolucionDeDepositos = Cuenta("24651091", AccountNature.Credit, tercero: true);
        DevolucionDeAportes = Cuenta("24650591", AccountNature.Credit, tercero: true);
        IngresoPorCargos = Cuenta("42950591", AccountNature.Credit);
        CargoPorCobrar = Cuenta("16909591", AccountNature.Debit, tercero: true, cruce: true);
        Perdidas = Cuenta("35100591", AccountNature.Credit);
        d.Db.SaveChanges();

        foreach (var marca in new[] { "Standard", "Garnished" }) Regla(R.CapitalDeCaptacion, Ahorro, clase: "Demand", marca: marca);
        Regla(R.CapitalDeCaptacion, AhorroExenta, clase: "Demand", marca: "GmfExempt");
        Regla(R.CapitalDeCaptacionInactiva, AhorroInactiva, clase: "Demand");
        Regla(R.CapitalDeCaptacion, Cdat612, clase: "TermDeposit", plazo: "SixToTwelve", marca: "Standard");
        Regla(R.InteresPorPagar, InteresPorPagar, clase: "Demand");
        Regla(R.InteresPorPagar, InteresPorPagar, clase: "TermDeposit");
        Regla(R.GastoPorIntereses, Gasto, clase: "Demand");
        Regla(R.GastoPorIntereses, Gasto, clase: "TermDeposit");
        Regla(R.RetencionPorPagar, Retencion, clase: "Demand");
        Regla(R.GmfPorPagar, Gmf, clase: "Demand");
        Regla(R.AportesSociales, AportesOrdinarios, claseDeAporte: "Ordinary");
        Regla(R.AportesSociales, AportesMinimos, claseDeAporte: "Minimum");
        Regla(R.DevolucionPorPagar, DevolucionDeDepositos, naturaleza: "Deposit", motivo: "DepositWithdrawal");
        Regla(R.DevolucionPorPagar, DevolucionDeAportes, naturaleza: "Contribution", motivo: "MembershipRefund");
        Regla(R.DevolucionPorPagar, DevolucionDeDepositos, naturaleza: "Deposit", motivo: "MembershipRefund");
        foreach (var clase in R.MotivosDelCargo) Regla(R.IngresoPorCargos, IngresoPorCargos, motivo: clase);
        Regla(R.CargoDeCuentaPorCobrar, CargoPorCobrar);
        Regla(R.PerdidaAbsorbida, Perdidas);
    }

    private ChartOfAccount Cuenta(string codigo, AccountNature naturaleza, bool tercero = false, bool cruce = false)
    {
        var cuenta = E.D.Cuenta(codigo, naturaleza, Car, tercero: tercero, cruce: cruce);
        E.Cuentas[codigo] = cuenta;
        return cuenta;
    }

    /// <summary>Una regla de captaciones sembrada directo (sin las validaciones del alta).</summary>
    public LendingPostingRule Regla(string rol, ChartOfAccount cuenta, string? clase = null, string? marca = null, string? plazo = null,
        string? claseDeAporte = null, string? naturaleza = null, string? motivo = null, string? recaudador = null, string operacion = LendingPostingRule.Cualquiera,
        DateOnly? desde = null)
    {
        var regla = new LendingPostingRule(operacion, rol, cuenta.Id, desde ?? MatrizDePruebaDeCartera.Enero1, reasonCode: motivo, collectorClass: recaudador,
            notes: "captaciones", lineNature: naturaleza, depositClass: clase, issueTermBand: plazo, contributionClass: claseDeAporte, accountFlag: marca)
        { CreatedBy = "test" };
        E.D.Db.LendingPostingRules.Add(regla);
        E.D.Db.SaveChanges();
        return regla;
    }

    // ------------------------------------------------------------------------------------------------- contenidos --

    public static MemberAccountRefV1 Cuenta(string numero) => new() { AccountPublicId = Guid.NewGuid(), AccountNumber = numero };

    public DepositFactDimensionsV1 AlaVista(string marca = "Standard", bool inactiva = false) => new()
    {
        CreditLineCode = "AHVISTA", LineNature = "Deposit", DepositClass = "Demand", AccountFlag = marca, Inactive = inactiva, BranchPublicId = Sucursal,
    };

    public DepositFactDimensionsV1 Aportes() => new()
    {
        CreditLineCode = "APORTES", LineNature = "Contribution", AccountFlag = "Standard", BranchPublicId = Sucursal,
    };

    public DepositFactDimensionsV1 Cdat(string plazo = "SixToTwelve") => new()
    {
        CreditLineCode = "CDAT", LineNature = "Deposit", DepositClass = "TermDeposit", IssueTermBand = plazo, AccountFlag = "Standard", BranchPublicId = Sucursal,
    };

    public static DepositConceptAmountV1 Valor(string concepto, decimal monto, string? motivo = null, string? claseDeAporte = null) =>
        new() { Concept = concepto, Amount = monto, ReasonCode = motivo, ContributionClass = claseDeAporte };

    public static CollectorRefV1 Recaudador(string clase, Guid pagador) => new() { CollectorCode = "TST", CollectorClass = clase, PayerPersonPublicId = pagador };

    // ------------------------------------------------------------------------------------------------ unidades --

    /// <summary>Una unidad de hechos con el mismo origen (la operación) y su sobre de Cartera.</summary>
    public IReadOnlyList<HechoDeCartera> Unidad(string clase, string numero, params object[] contenidos)
    {
        var origen = Guid.NewGuid();
        return contenidos.Select(c => new HechoDeCartera(Sobre(TipoDe(c), origen, numero, Titular), c, clase)).ToList();
    }

    public IntegrationEnvelopeV1 Sobre(string tipo, Guid origen, string numero, Guid? persona, DateOnly? fecha = null) => new()
    {
        MessageId = Guid.NewGuid(), Type = tipo, Version = 1, Kind = IntegrationMessageKind.Business, OriginModule = "CAR", OriginEventKey = "Confirmation",
        Origin = new MessageOriginV1 { Kind = MessageOriginKind.Document, Number = numero, PublicId = origen, OperationDate = fecha ?? Dia },
        ChainRootPublicId = origen, BranchPublicId = Sucursal, PersonPublicId = persona, Currency = "COP", ExchangeRate = 1m,
        OriginUser = new UserRefV1 { CentralUserId = Guid.NewGuid(), Name = "Usuario de ensayo" }, EmittedAt = new DateTimeOffset(2026, 11, 14, 15, 0, 0, TimeSpan.Zero),
    };

    public static string TipoDe(object contenido) => (string)contenido.GetType().GetField("Type")!.GetValue(null)!;

    public async Task<ConstruccionDeCartera> ConstruirAsync(IReadOnlyList<HechoDeCartera> unidad)
    {
        var catalogos = await CatalogosDeCartera.CargarAsync(E.D.Db, new ResolutorDeReglasDeCartera(E.D.Db), new TiposDeComprobanteDeCartera(E.D.Db), [unidad],
            E.D.Clock.TodayUtc, default);
        return ConstructorDeLineasDeCartera.Construir(unidad, catalogos);
    }

    /// <summary>Un hecho como lo entrega la bandeja (para el consumidor): el sobre con su contenido serializado y la entrega a Contabilidad.</summary>
    public static MensajeEntrante Entrante(HechoDeCartera h)
    {
        var bytes = OpcionesDeMensajes.Serializar(h.Contenido!);
        return new MensajeEntrante(h.Sobre with { Payload = h.Contenido }, h.Contenido, Encoding.UTF8.GetString(bytes), Convert.ToHexStringLower(SHA256.HashData(bytes)),
            PrevalidationOutcome.Postable,
            new EntregaEntrante(IntegrationDestinations.Accounting, DeliveryMode.Online, DeliveryStatus.Pending, null, null, null, 0, null))
        {
            OriginDocumentClassText = h.ClaseDeOrigen,
        };
    }
}
