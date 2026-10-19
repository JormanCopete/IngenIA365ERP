using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Feature 014, US8 (T142, T145; contabilidad.md §7 de la 014; research E108): los hechos <b>representativos</b> de una operación de captaciones o de
/// aportes, para preguntarle a Contabilidad si se contabilizaría <b>antes</b> de hacerla (la pantalla, el contrato, el proceso y la activación). Llevan las
/// dimensiones reales de la cuenta —línea, naturaleza, clase, plazo de emisión, marca, inactividad, sucursal— y los conceptos que la operación mueve con
/// importes simbólicos: la validación previa sólo mira hecho, sucursal, dimensiones, conceptos y signos (§13.1 de la 013), y el constructor de Contabilidad
/// arma con ellos las mismas partidas que con la operación real. Cada unidad lleva su propio origen y la fecha de la operación: sin ellos todas las
/// preguntas de un lote eran una sola unidad fechada el 1 de enero del año 1, donde ninguna regla rige. (nuevo)
/// </summary>
public static class HechosRepresentativosDeCaptaciones
{
    /// <summary>El número de cuenta de las preguntas que no son de una cuenta (la activación).</summary>
    public const string NumeroDeValidacion = "VALIDACION-PREVIA";

    /// <summary>
    /// Una cuenta tal como la ve la pregunta: sus dimensiones, su titular y su número. El titular y el número no cambian la regla que se resuelve, pero la
    /// cuenta contable que exige tercero o documento cruce los pide (AccountLineRules de la 009): sin ellos la pregunta respondería «falta el tercero».
    /// </summary>
    public sealed record CuentaRepresentativa(DepositFactDimensionsV1 D, Guid Titular, string Numero)
    {
        public MemberAccountRefV1 Referencia => new() { AccountNumber = Numero };
    }

    public static CuentaRepresentativa Cuenta(DepositFactDimensionsV1 d, Guid titular, string? numero = null) => new(d, titular, numero ?? NumeroDeValidacion);

    /// <summary>Las dimensiones de una cuenta como las emite <c>HechoDeCuenta.Dimensiones</c> (research C20).</summary>
    public static DepositFactDimensionsV1 Dimensiones(string linea, LineNature naturaleza, DepositClass? clase, Guid sucursal, bool embargada = false,
        bool exentaDeGmf = false, bool inactiva = false, IssueTermBand? plazo = null) => new()
    {
        CreditLineCode = linea,
        LineNature = naturaleza.ToString(),
        DepositClass = clase?.ToString(),
        IssueTermBand = plazo?.ToString(),
        AccountFlag = (embargada ? AccountFlag.Garnished : exentaDeGmf ? AccountFlag.GmfExempt : AccountFlag.Standard).ToString(),
        Inactive = inactiva,
        BranchPublicId = sucursal,
    };

    /// <summary>Una unidad (un origen nuevo, la fecha de la operación) con su principal primero y sus acompañantes.</summary>
    public static IReadOnlyList<MensajeContableDto> Unidad(DateOnly fecha, CuentaRepresentativa cuenta, params object[] contenidos)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        var origen = Guid.NewGuid();
        return contenidos.Select(c => new MensajeContableDto(new IntegrationEnvelopeV1
        {
            MessageId = Guid.NewGuid(), Type = TipoDe(c), Kind = IntegrationMessageKind.Business, OriginModule = ModulosDeOrigen.Cartera,
            OriginEventKey = ClavesDeEvento.Confirmacion, BranchPublicId = cuenta.D.BranchPublicId,
            PersonPublicId = cuenta.Titular == Guid.Empty ? null : cuenta.Titular,
            Origin = new MessageOriginV1 { Kind = MessageOriginKind.Document, PublicId = origen, Number = "VALIDACION-PREVIA", OperationDate = fecha },
        }, c)).ToList();
    }

    /// <summary>El <c>Type</c> de un contenido <c>…V1</c>.</summary>
    public static string TipoDe(object contenido) =>
        contenido.GetType().GetField("Type")?.GetValue(null) as string
        ?? throw new ArgumentException($"{contenido.GetType().Name} no es un hecho de la Cartera.", nameof(contenido));

    private static DepositConceptAmountV1 Valor(MemberAccountConcept concepto, decimal monto, string? motivo = null) => new()
    {
        Concept = concepto.ToString(), ContributionClass = EmisionDeCartera.ClaseDeAporte(concepto)?.ToString(), Amount = monto, ReasonCode = motivo,
    };

    /// <summary>El concepto representativo del capital de una naturaleza: el capital de una captación o el aporte ordinario.</summary>
    public static MemberAccountConcept ConceptoDeCapital(DepositFactDimensionsV1 d) =>
        d.LineNature == nameof(LineNature.Contribution) ? MemberAccountConcept.ContributionOrdinary : MemberAccountConcept.Principal;

    // ------------------------------------------------------------------------------------------------- movimientos --

    /// <summary>Una consignación (o un aporte) de un recaudador de la clase dada.</summary>
    public static object Consignacion(CuentaRepresentativa c, string claseDeRecaudador)
    {
        var cobro = new CollectorRefV1 { CollectorClass = claseDeRecaudador, PayerPersonPublicId = c.Titular };
        return c.D.LineNature == nameof(LineNature.Contribution)
            ? new AporteRecibidoV1
            {
                Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Collector = cobro,
                Amounts = [Valor(MemberAccountConcept.ContributionOrdinary, 1m)],
            }
            : new ConsignacionEnCuentaAplicadaV1
            {
                Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Collector = cobro, Amounts = [Valor(MemberAccountConcept.Principal, 1m)],
            };
    }

    /// <summary>Un retiro por el contrato (con la clase de su recaudador), por giro (sin clase: va a la devolución por pagar) o interno (débito automático).</summary>
    public static RetiroDeCuentaAplicadoV1 Retiro(CuentaRepresentativa c, string? claseDeRecaudador, bool interno = false) => new()
    {
        Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D,
        Collector = claseDeRecaudador is null ? null : new CollectorRefV1 { CollectorClass = claseDeRecaudador, PayerPersonPublicId = c.Titular },
        DisbursementOrderPublicId = claseDeRecaudador is null && !interno ? Guid.NewGuid() : null,
        Internal = interno ? new InternalWithdrawalV1 { Kind = "DirectDebit" } : null,
        Amounts = [Valor(MemberAccountConcept.Principal, -1m, "DepositWithdrawal")],
    };

    /// <summary>US11 (T189): la consignación de un embargo al juzgado (contra los embargos por consignar, con el juzgado de tercero).</summary>
    public static EmbargoConsignadoV1 EmbargoConsignado(CuentaRepresentativa c, Guid juzgado) => new()
    {
        Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Amounts = [Valor(MemberAccountConcept.Principal, -1m, "GarnishmentConsignment")],
        Garnishment = new GarnishmentRefV1 { PublicId = Guid.NewGuid(), OfficeReference = NumeroDeValidacion, CourtPersonPublicId = juzgado },
        OrderPublicId = Guid.NewGuid(),
    };

    /// <summary>El GMF de una disposición, con la clase del producto que lo genera.</summary>
    public static GmfRetenidoV1 Gmf(CuentaRepresentativa c) => new()
    {
        Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Amounts = [Valor(MemberAccountConcept.Gmf, 1m)],
        Gmf = new GmfDetailV1 { GeneratingDepositClass = c.D.DepositClass ?? string.Empty },
    };

    /// <summary>Un traslado entre dos cuentas (el destino con su concepto de capital).</summary>
    public static TrasladoEntreCuentasAplicadoV1 Traslado(CuentaRepresentativa origen, CuentaRepresentativa destino) => new()
    {
        Account = origen.Referencia, HolderPersonPublicId = origen.Titular, Dimensions = origen.D, Amounts = [Valor(MemberAccountConcept.Principal, -1m)],
        Target = destino.Referencia, TargetHolderPersonPublicId = destino.Titular, TargetDimensions = destino.D,
        TargetAmounts = [Valor(ConceptoDeCapital(destino.D), 1m)],
    };

    // --------------------------------------------------------------------------------------------------- intereses --

    private static DepositFactLineV1 Linea(CuentaRepresentativa c, params DepositConceptAmountV1[] valores) =>
        new() { Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Amounts = valores };

    public static InteresDeCaptacionCausadoV1 Causacion(CuentaRepresentativa c) => new() { Lines = [Linea(c, Valor(MemberAccountConcept.InterestPayable, 1m))] };

    public static InteresDeCaptacionLiquidadoV1 Liquidacion(CuentaRepresentativa c) => new()
    {
        Lines = [Linea(c, Valor(MemberAccountConcept.InterestPayable, -1m), Valor(MemberAccountConcept.Principal, 1m))],
    };

    public static RetencionPracticadaV1 Retencion(CuentaRepresentativa c) => new()
    {
        Lines = [Linea(c, Valor(MemberAccountConcept.Principal, -1m), Valor(MemberAccountConcept.Withholding, 1m))],
    };

    // ------------------------------------------------------------------------------------------------------ cargos --

    /// <summary>Un cargo cobrado del capital o, si la cuenta no alcanza, por cobrar; el ingreso por su clase.</summary>
    public static CargoDeCuentaCausadoV1 Cargo(CuentaRepresentativa c, ChargeKind clase, bool cobrado) => new()
    {
        Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Charge = new AccountChargeRefV1 { Kind = clase.ToString() },
        Amounts = [cobrado ? Valor(MemberAccountConcept.Principal, -1m, clase.ToString()) : Valor(MemberAccountConcept.ChargeReceivable, 1m, clase.ToString())],
    };

    // ----------------------------------------------------------------------------------------- retiro del asociado --

    /// <summary>El débito de una cuenta en el cruce al retiro, contra el puente <c>OwnLoanPayoff</c> (D45).</summary>
    public static CruceAlRetiroAplicadoV1 Cruce(CuentaRepresentativa c) => new()
    {
        Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Amounts = [Valor(ConceptoDeCapital(c.D), -1m)],
    };

    public static PerdidaAplicadaAlRetiroV1 Perdida(CuentaRepresentativa c) => new()
    {
        Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Amounts = [Valor(MemberAccountConcept.ContributionOrdinary, -1m)],
    };

    public static DevolucionOrdenadaV1 Devolucion(CuentaRepresentativa c, string tipoDeOrden) => new()
    {
        Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, OrderKind = tipoDeOrden, PayeePersonPublicId = c.Titular,
        Amounts = [Valor(ConceptoDeCapital(c.D), -1m, tipoDeOrden)],
    };

    // ------------------------------------------------------------------------------------------------------------ CDAT --

    /// <summary>
    /// US6 (T174): la constitución de un CDAT fondeada por un recaudador (con su clase) o desde otra cuenta del asociado (con sus dimensiones: la
    /// contrapartida es esa cuenta, research E107).
    /// </summary>
    public static CdatConstituidoV1 Constitucion(CuentaRepresentativa cdat, string? claseDeRecaudador, CuentaRepresentativa? origen = null) => new()
    {
        Account = cdat.Referencia, HolderPersonPublicId = cdat.Titular, Dimensions = cdat.D, Amounts = [Valor(MemberAccountConcept.Principal, 1m)],
        Funding = origen is null
            ? new TermDepositFundingV1 { Kind = "Collector", Collector = new CollectorRefV1 { CollectorClass = claseDeRecaudador ?? string.Empty, PayerPersonPublicId = cdat.Titular } }
            : new TermDepositFundingV1
            {
                Kind = "TransferFromAccount", SourceAccount = origen.Referencia, SourceHolderPersonPublicId = origen.Titular, SourceDimensions = origen.D,
                SourceAmounts = [Valor(MemberAccountConcept.Principal, -1m)],
            },
    };

    /// <summary>US6 (T174): el pago de un período del plan a otra cuenta del asociado o por giro (destino nulo).</summary>
    public static InteresesDeCdatPagadosV1 PagoDeIntereses(CuentaRepresentativa cdat, CuentaRepresentativa? destino) => new()
    {
        Account = cdat.Referencia, HolderPersonPublicId = cdat.Titular, Dimensions = cdat.D, Amounts = [Valor(MemberAccountConcept.InterestPayable, -1m)],
        Destination = Destino(destino),
    };

    /// <summary>US6 (T174): la retención de un pago de CDAT (sale del interés por pagar).</summary>
    public static RetencionPracticadaV1 RetencionDeCdat(CuentaRepresentativa c) => new()
    {
        Lines = [Linea(c, Valor(MemberAccountConcept.InterestPayable, -1m), Valor(MemberAccountConcept.Withholding, 1m))],
    };

    /// <summary>US6 (T174): la cancelación al vencimiento (capital e interés) a otra cuenta del asociado o por giro (destino nulo).</summary>
    public static CdatCanceladoV1 Cancelacion(CuentaRepresentativa cdat, CuentaRepresentativa? destino) => new()
    {
        Account = cdat.Referencia, HolderPersonPublicId = cdat.Titular, Dimensions = cdat.D, Cause = EmisionDeCartera.CancelacionPorInstruccion,
        Amounts = [Valor(MemberAccountConcept.Principal, -1m), Valor(MemberAccountConcept.InterestPayable, -1m)],
        Destination = Destino(destino, 2m),
    };

    /// <summary>US7 (T183): la bonificación del contractual que se abona al capital al cumplirse (contra el gasto por bonificación).</summary>
    public static BonificacionReconocidaV1 Bonificacion(CuentaRepresentativa c) => new()
    {
        Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Amounts = [Valor(MemberAccountConcept.Principal, 1m)],
    };

    /// <summary>US7 (T183): la provisión de la bonificación del contractual (al interés por pagar, contra el gasto por bonificación; P26).</summary>
    public static BonificacionReconocidaV1 ProvisionDeBonificacion(CuentaRepresentativa c) => new()
    {
        Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Amounts = [Valor(MemberAccountConcept.Bonus, 1m)],
    };

    /// <summary>US7 (T183): la penalidad del incumplimiento del contractual (del interés por pagar y del capital, contra el ingreso por penalidad).</summary>
    public static PenalidadAplicadaV1 PenalidadDeContractual(CuentaRepresentativa c) => new()
    {
        Account = c.Referencia, HolderPersonPublicId = c.Titular, Dimensions = c.D, Kind = EmisionDeCartera.PenalidadPorIncumplimiento,
        Amounts = [Valor(MemberAccountConcept.InterestPayable, -1m), Valor(MemberAccountConcept.Principal, -1m)],
    };

    /// <summary>US7 (T183): la retención de la bonificación abonada (sale del capital).</summary>
    public static RetencionPracticadaV1 RetencionDeBonificacion(CuentaRepresentativa c) => new()
    {
        Lines = [Linea(c, Valor(MemberAccountConcept.Principal, -1m), Valor(MemberAccountConcept.Withholding, 1m))],
    };

    private static TermDepositDestinationV1 Destino(CuentaRepresentativa? destino, decimal monto = 1m) => destino is null
        ? new TermDepositDestinationV1 { Kind = "DisbursementOrder", OrderPublicId = Guid.NewGuid() }
        : new TermDepositDestinationV1
        {
            Kind = "Account", TargetAccount = destino.Referencia, TargetHolderPersonPublicId = destino.Titular, TargetDimensions = destino.D,
            TargetAmounts = [Valor(MemberAccountConcept.Principal, monto)],
        };
}
