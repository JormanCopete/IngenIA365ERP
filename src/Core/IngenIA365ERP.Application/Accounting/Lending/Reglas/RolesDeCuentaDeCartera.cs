using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Accounting.Lending.Reglas;

/// <summary>
/// Una dimensión de la matriz de Cartera (feature 013, T0640, T52; contracts/contabilidad.md §2.2 y §3.1). El nombre que viaja
/// (<see cref="RolesDeCuentaDeCartera.NombreDe"/>) es el del cuerpo de la API (<c>creditLineCode</c>…). El centro de costo no está:
/// se asigna a la línea y no se compara (C77). (nuevo)
/// </summary>
public enum DimensionDeCartera
{
    CreditLine = 1,
    Modality = 2,
    Subsegment = 3,
    RiskCategory = 4,
    Concept = 5,
    IsEmployee = 6,
    InGracePeriod = 7,
    OnBehalfOfAssociate = 8,
    Restructured = 9,
    CollectorClass = 10,
    Reason = 11,
    Branch = 12,

    /// <summary>El hecho (<c>Operation</c>): opcional con peso 2 sólo en los roles de resultado (C77).</summary>
    Operation = 13,

    // Feature 014 (T044; contracts/contabilidad.md §1.1): las dimensiones de captaciones, al final de la clave. No pesan: se exigen por
    // igualdad cuando el rol las declara.

    /// <summary>Naturaleza de la línea (<c>LineNature</c>: <c>Deposit</c>, <c>Contribution</c>).</summary>
    LineNature = 14,

    /// <summary>Clase de captación (<c>DepositClass</c>).</summary>
    DepositClass = 15,

    /// <summary>Plazo de emisión (<c>IssueTermBand</c>), exigido sólo en el capital de CDAT y contractual (§1.2).</summary>
    IssueTermBand = 16,

    /// <summary>Clase de aporte (<c>ContributionClass</c>).</summary>
    ContributionClass = 17,

    /// <summary>Marca de la cuenta (<c>AccountFlag</c>: <c>Standard</c>, <c>GmfExempt</c>, <c>Garnished</c>; research C20).</summary>
    AccountFlag = 18,
}

/// <summary>La familia de un rol (§2.2): decide si admite el hecho fijado y agrupa los conjuntos de cuentas de la conciliación (§14). (nuevo)</summary>
public enum FamiliaDeRol
{
    Saldo = 1,
    Orden = 2,
    PasivoConElDeudor = 3,
    Pasivo = 4,
    Puente = 5,
    Resultado = 6,
    Diferido = 7,
    Contingente = 8,

    /// <summary>Los saldos de las cuentas de captación (feature 014, T045).</summary>
    Captacion = 9,

    /// <summary>Los aportes sociales y los fondos patrimoniales (feature 014, T045).</summary>
    Patrimonio = 10,
}

/// <summary>
/// Quién es el tercero natural de un rol (§2.2, §7.3). Se pone sólo en la línea cuya cuenta exige tercero; un rol sin tercero natural
/// toma la persona del sobre si la cuenta lo exige. (nuevo)
/// </summary>
public enum TerceroNaturalDeCartera
{
    Ninguno = 0,
    Deudor = 1,

    /// <summary>La aseguradora del seguro causado (<c>counterpartyPersonPublicId</c>).</summary>
    Aseguradora = 2,

    /// <summary>El tercero del cargo por cuenta del asociado (gasto de cobranza a favor de un tercero).</summary>
    TerceroDelCargo = 3,

    /// <summary>El pagador del recaudo (<c>collector.payerPersonPublicId</c>).</summary>
    Pagador = 4,

    /// <summary>El beneficiario sellado en la orden de giro (<c>payeePersonPublicId</c>).</summary>
    Beneficiario = 5,

    /// <summary>El titular de la cuenta de captación o de aportes (<c>holderPersonPublicId</c>; feature 014, T045).</summary>
    Titular = 6,
}

/// <summary>
/// El documento cruce natural de un rol (§5): <see cref="Credito"/> = el tipo del mapeo (<c>PG</c>) con el número del crédito;
/// <see cref="Factura"/> = <c>FV</c> con la factura de la venta (los dos roles de Comercial, C79). Sólo se pone si la cuenta lo exige;
/// los roles sin cruce natural que caen en una cuenta que lo exige llevan el de por defecto (§5, regla 1). (nuevo)
/// </summary>
public enum CruceDeRol
{
    Ninguno = 0,
    Credito = 1,
    Factura = 2,

    /// <summary>
    /// <c>CTA</c> «Cuenta de captación o aportes» con el número de la cuenta (<c>MemberAccountRefV1.accountNumber</c>; feature 014, T045,
    /// contabilidad.md §5).
    /// </summary>
    Cuenta = 3,
}

/// <summary>
/// Un rol de cuenta de Cartera (§2.2): su naturaleza, su familia, sus dimensiones exigidas (igualdad exacta, nunca <c>*</c>) y opcionales
/// (igualdad o <c>*</c>, con peso), los conceptos y motivos que admite, su tercero y su cruce natural. (nuevo)
/// </summary>
public sealed record RolDeCuentaDeCartera(
    string Codigo,
    AccountNature Naturaleza,
    FamiliaDeRol Familia,
    IReadOnlyList<DimensionDeCartera> Exigidas,
    IReadOnlyList<DimensionDeCartera> Opcionales,
    IReadOnlyList<string>? Conceptos,
    IReadOnlyList<string>? Motivos,
    TerceroNaturalDeCartera Tercero,
    CruceDeRol Cruce)
{
    /// <summary>Sólo los roles de resultado admiten fijar el hecho (peso 2); en los demás va en <c>*</c> (C77).</summary>
    public bool AdmiteHecho => Familia == FamiliaDeRol.Resultado;

    /// <summary>Exigidas u opcionales (sin mirar el motivo: ver <see cref="RolesDeCuentaDeCartera.DimensionesDe"/>).</summary>
    public bool Admite(DimensionDeCartera dimension) => Exigidas.Contains(dimension) || Opcionales.Contains(dimension);
}

/// <summary>
/// Los cuarenta y tres roles de cuenta de la matriz de Cartera: los veintiséis de crédito (feature 013, T0640, T52, C91; contracts/contabilidad.md
/// §2.2; data-model parte 3 §5.5) y, al final, los diecisiete de captaciones y aportes (feature 014, T045; research C10; contabilidad.md §2 de la
/// 014). Catálogo fijo en código: son datos, no parámetros. Donde <c>contabilidad.md</c> y <c>data-model.md</c> difieren en las
/// exigidas, rige la tabla de <c>contabilidad.md</c> (tasks.md, contradicción 3). La cuenta de cada rol la elige la contadora en la
/// matriz: ninguna cuenta vive en código (<c>LaContabilidadNoTieneCuentasEnCodigo</c>).
///
/// <para>
/// <c>GastoDeterioro</c> y <c>RecuperacionDeterioro</c> (y, en captaciones, el capital con el plazo de emisión) son los únicos cuyas exigidas
/// dependen del valor de otra: con el motivo
/// <c>Individual</c> exigen también modalidad y concepto (<see cref="DimensionesDe"/>). Decisiones del dueño que rigen aquí: la dimensión
/// «reestructurado» (C4), los roles <c>IngresoDiferido</c> y <c>GastoDeCastigoOCondonacion</c> sin cuenta sembrada (C7) y «empleado» del
/// estado actual de la persona (C8). Los contingentes siguen el CUIF (C91): <c>ContingenteAcreedora</c> de naturaleza crédito contra
/// <c>ContraContingenteAcreedora</c> de naturaleza débito.
/// </para>
/// </summary>
public static class RolesDeCuentaDeCartera
{
    public const string Capital = "Capital";
    public const string InteresPorCobrar = "InteresPorCobrar";
    public const string PagoPorCuentaDelAsociado = "PagoPorCuentaDelAsociado";
    public const string CargoPorCobrar = "CargoPorCobrar";
    public const string DeterioroIndividual = "DeterioroIndividual";
    public const string DeterioroGeneral = "DeterioroGeneral";
    public const string SuspensoEnOrden = "SuspensoEnOrden";
    public const string ContraOrdenContingente = "ContraOrdenContingente";
    public const string CastigadaEnOrden = "CastigadaEnOrden";
    public const string ContraOrdenControl = "ContraOrdenControl";
    public const string Anticipo = "Anticipo";
    public const string SaldoAFavor = "SaldoAFavor";
    public const string SeguroPorPagar = "SeguroPorPagar";
    public const string CuentaPorPagarATercero = "CuentaPorPagarATercero";
    public const string RecaudoPorAplicar = "RecaudoPorAplicar";
    public const string DesembolsoPorGirar = "DesembolsoPorGirar";
    public const string PuenteComercial = "PuenteComercial";
    public const string CuentaPorCobrarComercial = "CuentaPorCobrarComercial";
    public const string Ingreso = "Ingreso";
    public const string GastoDeterioro = "GastoDeterioro";
    public const string RecuperacionDeterioro = "RecuperacionDeterioro";
    public const string GastoDeCastigoOCondonacion = "GastoDeCastigoOCondonacion";
    public const string IngresoPorRecuperacion = "IngresoPorRecuperacion";
    public const string IngresoDiferido = "IngresoDiferido";
    public const string ContingenteAcreedora = "ContingenteAcreedora";
    public const string ContraContingenteAcreedora = "ContraContingenteAcreedora";

    // Feature 014 (T045; research C10; contracts/contabilidad.md §2): los 17 roles de captaciones y aportes, en español (C10), al final.
    public const string CapitalDeCaptacion = "CapitalDeCaptacion";
    public const string CapitalDeCaptacionInactiva = "CapitalDeCaptacionInactiva";
    public const string InteresPorPagar = "InteresPorPagar";
    public const string GastoPorIntereses = "GastoPorIntereses";
    public const string RetencionPorPagar = "RetencionPorPagar";
    public const string GmfPorPagar = "GmfPorPagar";
    public const string GastoPorBonificacion = "GastoPorBonificacion";
    public const string IngresoPorCargos = "IngresoPorCargos";
    public const string CargoDeCuentaPorCobrar = "CargoDeCuentaPorCobrar";
    public const string AportesSociales = "AportesSociales";
    public const string FondoDeRevalorizacion = "FondoDeRevalorizacion";
    public const string FondoDeExcedentes = "FondoDeExcedentes";
    public const string DevolucionPorPagar = "DevolucionPorPagar";
    public const string NoReclamadosPorPagar = "NoReclamadosPorPagar";
    public const string EmbargosPorConsignar = "EmbargosPorConsignar";
    public const string IngresoPorPenalidad = "IngresoPorPenalidad";
    public const string PerdidaAbsorbida = "PerdidaAbsorbida";

    /// <summary>Respuesta 89 B del dueño (E260): la retención en la fuente practicada sobre la revalorización y los retornos que reparte la asamblea.</summary>
    public const string RetencionDeAsambleaPorPagar = "RetencionDeAsambleaPorPagar";

    // ------------------------------------------------------------------------------------------------ valores fijos --

    /// <summary>Las categorías contables (§3.2): las subcategorías van a su letra.</summary>
    public static IReadOnlyList<string> Categorias { get; } = ["A", "B", "C", "D", "E"];

    /// <summary>Los conceptos de saldo de un crédito (valores de <c>LoanConcept</c> como texto).</summary>
    public const string Principal = "Principal";
    public const string CurrentInterest = "CurrentInterest";
    public const string DefaultInterest = "DefaultInterest";
    public const string CurrentInterestInSuspense = "CurrentInterestInSuspense";
    public const string DefaultInterestInSuspense = "DefaultInterestInSuspense";
    public const string Insurance = "Insurance";
    public const string CollectionFee = "CollectionFee";
    public const string OtherCharge = "OtherCharge";
    public const string Advance = "Advance";
    public const string CreditBalance = "CreditBalance";

    /// <summary>Los diez conceptos de <c>LoanConcept</c>, en su orden.</summary>
    public static IReadOnlyList<string> Conceptos { get; } = Enum.GetNames<IngenIA365ERP.Domain.Enums.Lending.LoanConcept>();

    /// <summary>Las clases de recaudador (valores de <c>ClaseDeRecaudador</c> como texto).</summary>
    public static IReadOnlyList<string> ClasesDeRecaudador { get; } = Enum.GetNames<IngenIA365ERP.Application.Common.Integration.Lending.ClaseDeRecaudador>();

    /// <summary>Los conceptos del activo (los que se deterioran, se condonan y se castigan).</summary>
    public static IReadOnlyList<string> ConceptosDelActivo { get; } = [Principal, CurrentInterest, DefaultInterest, Insurance, CollectionFee, OtherCharge];

    /// <summary>Los motivos del nivel del deterioro en la regla (C93): <c>Individual</c> en las líneas por crédito, <c>General</c> o <c>Additional</c> en el general.</summary>
    public const string Individual = "Individual";
    public const string General = "General";
    public const string Additional = "Additional";

    private const DimensionDeCartera Lin = DimensionDeCartera.CreditLine;
    private const DimensionDeCartera Mod = DimensionDeCartera.Modality;
    private const DimensionDeCartera Sub = DimensionDeCartera.Subsegment;
    private const DimensionDeCartera Cat = DimensionDeCartera.RiskCategory;
    private const DimensionDeCartera Con = DimensionDeCartera.Concept;
    private const DimensionDeCartera Emp = DimensionDeCartera.IsEmployee;
    private const DimensionDeCartera Gra = DimensionDeCartera.InGracePeriod;
    private const DimensionDeCartera Pca = DimensionDeCartera.OnBehalfOfAssociate;
    private const DimensionDeCartera Ree = DimensionDeCartera.Restructured;
    private const DimensionDeCartera Cla = DimensionDeCartera.CollectorClass;
    private const DimensionDeCartera Mot = DimensionDeCartera.Reason;
    private const DimensionDeCartera Suc = DimensionDeCartera.Branch;
    private const DimensionDeCartera Op = DimensionDeCartera.Operation;
    private const DimensionDeCartera Nat = DimensionDeCartera.LineNature;
    private const DimensionDeCartera Cls = DimensionDeCartera.DepositClass;
    private const DimensionDeCartera Plz = DimensionDeCartera.IssueTermBand;
    private const DimensionDeCartera CAp = DimensionDeCartera.ContributionClass;
    private const DimensionDeCartera Mrc = DimensionDeCartera.AccountFlag;

    private const AccountNature D = AccountNature.Debit;
    private const AccountNature C = AccountNature.Credit;

    private static readonly string[] MotivosDelDeterioroGeneral = [General, Additional];
    private static readonly string[] MotivosDelGastoDeDeterioro = [Individual, General, Additional];
    private static readonly string[] MotivosDelGiro = ["Transfer", "Check", "ThirdPartyPayment"];
    private static readonly string[] MotivosDelCastigo = ["WriteOff", "Waiver"];
    private static readonly string[] MotivosContingentes = ["ApprovedNotDisbursed", "RevolvingLimit", "CollateralReceived"];

    // ------------------------------------------------------------------------------------- captaciones (feature 014) --

    /// <summary>Las naturalezas de línea que admiten los roles de captaciones (<c>Placement</c> no: <c>LineNatureMismatch</c>).</summary>
    public static IReadOnlyList<string> NaturalezasDeCaptacion { get; } = [nameof(LineNature.Deposit), nameof(LineNature.Contribution)];

    /// <summary>Las naturalezas de línea (<c>LineNature</c> como texto).</summary>
    public static IReadOnlyList<string> Naturalezas { get; } = Enum.GetNames<LineNature>();

    /// <summary>Las clases de captación (<c>DepositClass</c> como texto).</summary>
    public static IReadOnlyList<string> ClasesDeCaptacion { get; } = Enum.GetNames<DepositClass>();

    /// <summary>Los plazos de emisión (<c>IssueTermBand</c> como texto).</summary>
    public static IReadOnlyList<string> PlazosDeEmision { get; } = Enum.GetNames<IssueTermBand>();

    /// <summary>Las clases de aporte (<c>ContributionClass</c> como texto).</summary>
    public static IReadOnlyList<string> ClasesDeAporte { get; } = Enum.GetNames<ContributionClass>();

    /// <summary>Las marcas de la cuenta (<c>AccountFlag</c> como texto; research C20).</summary>
    public static IReadOnlyList<string> MarcasDeCuenta { get; } = Enum.GetNames<AccountFlag>();

    /// <summary>Los conceptos del libro de cuentas (<c>MemberAccountConcept</c> como texto; contabilidad.md §1.2).</summary>
    public static IReadOnlyList<string> ConceptosDeCaptacion { get; } = Enum.GetNames<MemberAccountConcept>();

    /// <summary>
    /// Las clases de captación cuyo capital exige además el plazo de emisión (§1.2): el CUIF separa 2110 por plazo y 2125 por corto y largo
    /// plazo, no 2105 ni 2130.
    /// </summary>
    public static IReadOnlyList<string> ClasesConPlazoDeEmision { get; } = [nameof(DepositClass.TermDeposit), nameof(DepositClass.Contractual)];

    /// <summary>Los motivos del puente de devoluciones: el <c>OrderKind</c> de la orden (mensajes.md §5.3), salvo el desembolso de un crédito.</summary>
    public static IReadOnlyList<string> MotivosDeLaDevolucion { get; } =
    [
        "DepositWithdrawal", "MembershipRefund", "ContributionRefund", "DeceasedDelivery", "TermDepositInterest", "TermDepositRedemption",
        "ContractualMaturity", "UnclaimedPayment", "GarnishmentConsignment", "SurplusPayment",
    ];

    /// <summary>Los motivos del ingreso por cargos: el <c>ChargeKind</c> del cargo.</summary>
    public static IReadOnlyList<string> MotivosDelCargo { get; } = Enum.GetNames<ChargeKind>();

    public static IReadOnlyList<RolDeCuentaDeCartera> Todos { get; } =
    [
        new(Capital, D, FamiliaDeRol.Saldo, [Mod, Cat], [Lin, Emp, Sub, Ree, Suc], null, null, TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(InteresPorCobrar, D, FamiliaDeRol.Saldo, [Mod, Cat, Con], [Lin, Emp, Gra, Sub, Ree, Suc], [CurrentInterest, DefaultInterest], null,
            TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(PagoPorCuentaDelAsociado, D, FamiliaDeRol.Saldo, [Mod, Cat, Con], [Lin, Emp, Gra, Sub, Ree, Suc], [Insurance, CollectionFee, OtherCharge], null,
            TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(CargoPorCobrar, D, FamiliaDeRol.Saldo, [Con], [Lin, Sub, Suc], [CollectionFee, OtherCharge], null, TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(DeterioroIndividual, C, FamiliaDeRol.Saldo, [Mod, Cat, Con], [Lin, Emp, Gra, Sub, Ree, Suc], ConceptosDelActivo, null,
            TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(DeterioroGeneral, C, FamiliaDeRol.Saldo, [Mot], [Suc], null, MotivosDelDeterioroGeneral, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        new(SuspensoEnOrden, D, FamiliaDeRol.Orden, [Mod, Cat, Con], [Lin, Emp, Sub, Ree, Suc], [CurrentInterestInSuspense, DefaultInterestInSuspense], null,
            TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(ContraOrdenContingente, C, FamiliaDeRol.Orden, [], [Suc], null, null, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        new(CastigadaEnOrden, D, FamiliaDeRol.Orden, [Con], [Lin, Sub, Suc],
            [Principal, CurrentInterest, DefaultInterest, CurrentInterestInSuspense, DefaultInterestInSuspense, Insurance, CollectionFee, OtherCharge], null,
            TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(ContraOrdenControl, C, FamiliaDeRol.Orden, [], [Suc], null, null, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        new(Anticipo, C, FamiliaDeRol.PasivoConElDeudor, [], [Lin, Suc], null, null, TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(SaldoAFavor, C, FamiliaDeRol.PasivoConElDeudor, [], [Lin, Suc], null, null, TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(SeguroPorPagar, C, FamiliaDeRol.Pasivo, [], [Lin, Suc], null, null, TerceroNaturalDeCartera.Aseguradora, CruceDeRol.Ninguno),
        new(CuentaPorPagarATercero, C, FamiliaDeRol.Pasivo, [], [Suc], null, null, TerceroNaturalDeCartera.TerceroDelCargo, CruceDeRol.Ninguno),
        new(RecaudoPorAplicar, D, FamiliaDeRol.Puente, [Cla], [Suc], null, null, TerceroNaturalDeCartera.Pagador, CruceDeRol.Credito),
        new(DesembolsoPorGirar, C, FamiliaDeRol.Puente, [Mot], [Lin, Suc], null, MotivosDelGiro, TerceroNaturalDeCartera.Beneficiario, CruceDeRol.Credito),
        new(PuenteComercial, C, FamiliaDeRol.Puente, [], [Lin, Suc], null, null, TerceroNaturalDeCartera.Deudor, CruceDeRol.Factura),
        new(CuentaPorCobrarComercial, C, FamiliaDeRol.Puente, [], [Lin, Suc], null, null, TerceroNaturalDeCartera.Deudor, CruceDeRol.Factura),
        // K6b (T1056): el Ingreso admite también el capital, el anticipo y el saldo a favor, sólo para la corrección monetaria UVR (con su hecho
        // fijado en la regla, la contadora elige la cuenta de la corrección monetaria).
        new(Ingreso, C, FamiliaDeRol.Resultado, [Mod, Con], [Op, Lin, Emp, Gra, Sub, Ree, Pca, Suc],
            [CurrentInterest, DefaultInterest, CurrentInterestInSuspense, DefaultInterestInSuspense, Insurance, CollectionFee, OtherCharge,
                Principal, Advance, CreditBalance], null,
            TerceroNaturalDeCartera.Deudor, CruceDeRol.Ninguno),
        new(GastoDeterioro, D, FamiliaDeRol.Resultado, [Mot], [Op, Lin, Emp, Gra, Sub, Suc], ConceptosDelActivo, MotivosDelGastoDeDeterioro,
            TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        new(RecuperacionDeterioro, C, FamiliaDeRol.Resultado, [Mot], [Op, Lin, Emp, Gra, Sub, Suc], ConceptosDelActivo, MotivosDelGastoDeDeterioro,
            TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        new(GastoDeCastigoOCondonacion, D, FamiliaDeRol.Resultado, [Mot, Con], [Op, Lin, Sub, Pca, Suc], ConceptosDelActivo, MotivosDelCastigo,
            TerceroNaturalDeCartera.Deudor, CruceDeRol.Ninguno),
        new(IngresoPorRecuperacion, C, FamiliaDeRol.Resultado, [], [Op, Lin, Sub, Suc], null, null, TerceroNaturalDeCartera.Deudor, CruceDeRol.Ninguno),
        new(IngresoDiferido, C, FamiliaDeRol.Diferido, [Mod], [Lin, Suc], null, null, TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(ContingenteAcreedora, C, FamiliaDeRol.Contingente, [Mot], [Lin, Suc], null, MotivosContingentes, TerceroNaturalDeCartera.Deudor, CruceDeRol.Credito),
        new(ContraContingenteAcreedora, D, FamiliaDeRol.Contingente, [Mot], [Suc], null, MotivosContingentes, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        // Feature 014 (T045; contracts/contabilidad.md §2, research C10 y C20): captaciones y aportes. El plazo de emisión del capital se exige
        // sólo en CDAT y contractual (DimensionesDe); la inactividad cambia de rol, no de marca.
        new(CapitalDeCaptacion, C, FamiliaDeRol.Captacion, [Cls, Mrc], [Lin, Suc], null, null, TerceroNaturalDeCartera.Titular, CruceDeRol.Cuenta),
        new(CapitalDeCaptacionInactiva, C, FamiliaDeRol.Captacion, [Cls], [Lin, Suc], null, null, TerceroNaturalDeCartera.Titular, CruceDeRol.Cuenta),
        new(InteresPorPagar, C, FamiliaDeRol.Captacion, [Cls], [Lin, Suc], null, null, TerceroNaturalDeCartera.Titular, CruceDeRol.Cuenta),
        new(GastoPorIntereses, D, FamiliaDeRol.Resultado, [Cls], [Op, Lin, Suc], null, null, TerceroNaturalDeCartera.Titular, CruceDeRol.Ninguno),
        new(RetencionPorPagar, C, FamiliaDeRol.Pasivo, [Cls], [Suc], null, null, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        new(GmfPorPagar, C, FamiliaDeRol.Pasivo, [Cls], [Suc], null, null, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        new(GastoPorBonificacion, D, FamiliaDeRol.Resultado, [Cls], [Op, Lin, Suc], null, null, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        new(IngresoPorCargos, C, FamiliaDeRol.Resultado, [Mot], [Op, Lin, Suc], null, MotivosDelCargo, TerceroNaturalDeCartera.Titular, CruceDeRol.Ninguno),
        new(CargoDeCuentaPorCobrar, D, FamiliaDeRol.Saldo, [], [Lin, Suc], null, null, TerceroNaturalDeCartera.Titular, CruceDeRol.Cuenta),
        new(AportesSociales, C, FamiliaDeRol.Patrimonio, [CAp], [Lin, Suc], null, null, TerceroNaturalDeCartera.Titular, CruceDeRol.Cuenta),
        new(FondoDeRevalorizacion, D, FamiliaDeRol.Patrimonio, [], [Suc], null, null, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        new(FondoDeExcedentes, D, FamiliaDeRol.Patrimonio, [], [Suc], null, null, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        new(DevolucionPorPagar, C, FamiliaDeRol.Puente, [Nat, Mot], [Lin, Suc], null, MotivosDeLaDevolucion, TerceroNaturalDeCartera.Beneficiario,
            CruceDeRol.Ninguno),
        new(NoReclamadosPorPagar, C, FamiliaDeRol.Pasivo, [Nat], [Suc], null, null, TerceroNaturalDeCartera.Titular, CruceDeRol.Cuenta),
        new(EmbargosPorConsignar, C, FamiliaDeRol.Puente, [], [Suc], null, null, TerceroNaturalDeCartera.Beneficiario, CruceDeRol.Ninguno),
        new(IngresoPorPenalidad, C, FamiliaDeRol.Resultado, [Cls], [Op, Lin, Suc], null, null, TerceroNaturalDeCartera.Titular, CruceDeRol.Ninguno),
        new(PerdidaAbsorbida, C, FamiliaDeRol.Patrimonio, [], [Suc], null, null, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
        // E260 (respuesta 89 B): no lleva clase de captación, como la retención sobre rendimientos: lo retenido a un asociado sobre lo que reparte la asamblea.
        new(RetencionDeAsambleaPorPagar, C, FamiliaDeRol.Pasivo, [], [Suc], null, null, TerceroNaturalDeCartera.Ninguno, CruceDeRol.Ninguno),
    ];

    /// <summary>Cuántos roles de crédito hay antes de los de captaciones (la 013).</summary>
    public const int RolesDeCredito = 26;

    /// <summary>Los 17 roles de captaciones y aportes (feature 014): los que se agregaron al final de <see cref="Todos"/>.</summary>
    public static IReadOnlyList<RolDeCuentaDeCartera> DeCaptaciones { get; } = Todos.Skip(RolesDeCredito).ToList();

    /// <summary>¿Es un rol de captaciones o aportes (feature 014)? Sus reglas no admiten <c>lineNature = Placement</c>.</summary>
    public static bool EsDeCaptaciones(string? rol) => rol is not null && DeCaptaciones.Any(r => r.Codigo == rol.Trim());

    private static readonly Dictionary<string, RolDeCuentaDeCartera> PorCodigo = Todos.ToDictionary(r => r.Codigo, StringComparer.Ordinal);

    /// <summary>El rol por su código exacto (sensible a mayúsculas, como en la clave de la regla).</summary>
    public static RolDeCuentaDeCartera? Buscar(string? codigo) =>
        codigo is null ? null : PorCodigo.GetValueOrDefault(codigo.Trim());

    /// <summary>
    /// Las exigidas y opcionales del rol según el motivo de la regla o del hecho: <c>GastoDeterioro</c> y <c>RecuperacionDeterioro</c>
    /// con <c>Individual</c> exigen además modalidad y concepto (§2.2); con <c>General</c> o <c>Additional</c>, no los admiten. Desde la
    /// feature 014 (contabilidad.md §1.2), el capital de captación —activo o inactivo— exige además el plazo de emisión cuando la clase es
    /// CDAT o contractual; en las demás clases no lo admite.
    /// </summary>
    public static (IReadOnlyList<DimensionDeCartera> Exigidas, IReadOnlyList<DimensionDeCartera> Opcionales) DimensionesDe(
        RolDeCuentaDeCartera rol, string? motivo, string? claseDeCaptacion = null)
    {
        if (rol.Codigo is GastoDeterioro or RecuperacionDeterioro && string.Equals(motivo?.Trim(), Individual, StringComparison.Ordinal))
            return ([.. rol.Exigidas, Mod, Con], rol.Opcionales);
        if (rol.Codigo is CapitalDeCaptacion or CapitalDeCaptacionInactiva
            && ClasesConPlazoDeEmision.Contains(claseDeCaptacion?.Trim() ?? string.Empty, StringComparer.Ordinal))
            return ([.. rol.Exigidas, Plz], rol.Opcionales);
        return (rol.Exigidas, rol.Opcionales);
    }

    /// <summary>¿El rol admite el concepto? Sin lista propia, cualquiera de <c>LoanConcept</c>.</summary>
    public static bool AdmiteConcepto(RolDeCuentaDeCartera rol, string concepto) =>
        (rol.Conceptos ?? Conceptos).Contains(concepto, StringComparer.Ordinal);

    /// <summary>¿El rol admite el motivo? Sólo los roles que exigen motivo tienen lista.</summary>
    public static bool AdmiteMotivo(RolDeCuentaDeCartera rol, string motivo) =>
        rol.Motivos is { } motivos && motivos.Contains(motivo, StringComparer.Ordinal);

    /// <summary>
    /// De concepto a rol de saldo (§2.2): el constructor elige el rol de cada importe por su <c>LoanConcept</c>. El suspenso de un crédito
    /// castigado va a <c>CastigadaEnOrden</c>; un cargo «por cuenta del asociado», a <c>PagoPorCuentaDelAsociado</c>. Nulo si el concepto
    /// no es de Cartera.
    /// </summary>
    public static string? RolDelConcepto(string concepto, bool porCuentaDelAsociado = false, bool castigado = false) => concepto switch
    {
        Principal => Capital,
        CurrentInterest or DefaultInterest => InteresPorCobrar,
        CurrentInterestInSuspense or DefaultInterestInSuspense => castigado ? CastigadaEnOrden : SuspensoEnOrden,
        Insurance => PagoPorCuentaDelAsociado,
        CollectionFee or OtherCharge => porCuentaDelAsociado ? PagoPorCuentaDelAsociado : CargoPorCobrar,
        Advance => Anticipo,
        CreditBalance => SaldoAFavor,
        _ => null,
    };

    /// <summary>
    /// De concepto del libro de cuentas a rol (contabilidad.md §2 de la 014, «de concepto a rol»): el capital va a
    /// <see cref="CapitalDeCaptacion"/> o, si la cuenta está inactiva, a <see cref="CapitalDeCaptacionInactiva"/>; los aportes de toda clase a
    /// <see cref="AportesSociales"/> (la clase viaja como dimensión, <see cref="ClaseDeAporteDelConcepto"/>); la bonificación se reconoce como
    /// interés por pagar. Nulo si el concepto no es del libro de cuentas.
    /// </summary>
    public static string? RolDelConceptoDeCuenta(string concepto, bool inactiva = false) => concepto switch
    {
        nameof(MemberAccountConcept.Principal) => inactiva ? CapitalDeCaptacionInactiva : CapitalDeCaptacion,
        nameof(MemberAccountConcept.InterestPayable) or nameof(MemberAccountConcept.Bonus) => InteresPorPagar,
        nameof(MemberAccountConcept.Withholding) => RetencionPorPagar,
        nameof(MemberAccountConcept.Gmf) => GmfPorPagar,
        nameof(MemberAccountConcept.ContributionMinimum) or nameof(MemberAccountConcept.ContributionOrdinary)
            or nameof(MemberAccountConcept.ContributionExtraordinary) or nameof(MemberAccountConcept.ContributionRevaluation) => AportesSociales,
        nameof(MemberAccountConcept.ChargeReceivable) => CargoDeCuentaPorCobrar,
        // Feature 014, US14 (T209): lo trasladado a no reclamados.
        nameof(MemberAccountConcept.Unclaimed) => NoReclamadosPorPagar,
        _ => null,
    };

    /// <summary>La clase de aporte de un concepto de aportes (<c>ContributionOrdinary</c> → <c>Ordinary</c>…); nula en los demás.</summary>
    public static string? ClaseDeAporteDelConcepto(string concepto) => concepto switch
    {
        nameof(MemberAccountConcept.ContributionMinimum) => nameof(ContributionClass.Minimum),
        nameof(MemberAccountConcept.ContributionOrdinary) => nameof(ContributionClass.Ordinary),
        nameof(MemberAccountConcept.ContributionExtraordinary) => nameof(ContributionClass.Extraordinary),
        nameof(MemberAccountConcept.ContributionRevaluation) => nameof(ContributionClass.Revalued),
        _ => null,
    };

    /// <summary>La contrapartida de orden de un rol de orden deudora (§2.2): el suspenso contra la contingente; la castigada contra el control.</summary>
    public static string? ContraDeOrden(string rol) => rol switch
    {
        SuspensoEnOrden => ContraOrdenContingente,
        CastigadaEnOrden => ContraOrdenControl,
        ContingenteAcreedora => ContraContingenteAcreedora,
        _ => null,
    };

    /// <summary>El nombre de la dimensión en la API, en los errores y en la plantilla.</summary>
    public static string NombreDe(DimensionDeCartera dimension) => dimension switch
    {
        DimensionDeCartera.CreditLine => "creditLineCode",
        DimensionDeCartera.Modality => "modalityCode",
        DimensionDeCartera.Subsegment => "subsegmentCode",
        DimensionDeCartera.RiskCategory => "riskCategory",
        DimensionDeCartera.Concept => "conceptCode",
        DimensionDeCartera.IsEmployee => "isEmployee",
        DimensionDeCartera.InGracePeriod => "inGracePeriod",
        DimensionDeCartera.OnBehalfOfAssociate => "onBehalfOfAssociate",
        DimensionDeCartera.Restructured => "restructured",
        DimensionDeCartera.CollectorClass => "collectorClass",
        DimensionDeCartera.Reason => "reasonCode",
        DimensionDeCartera.Branch => "branch",
        DimensionDeCartera.Operation => "operation",
        DimensionDeCartera.LineNature => "lineNature",
        DimensionDeCartera.DepositClass => "depositClass",
        DimensionDeCartera.IssueTermBand => "issueTermBand",
        DimensionDeCartera.ContributionClass => "contributionClass",
        DimensionDeCartera.AccountFlag => "accountFlag",
        _ => dimension.ToString(),
    };

    /// <summary>El peso de una dimensión opcional (§3.3); las exigidas no pesan.</summary>
    public static int PesoDe(DimensionDeCartera dimension) => dimension switch
    {
        DimensionDeCartera.CreditLine => LendingPostingRule.PesoLinea,
        DimensionDeCartera.IsEmployee => LendingPostingRule.PesoEmpleado,
        DimensionDeCartera.InGracePeriod => LendingPostingRule.PesoGracia,
        DimensionDeCartera.Subsegment => LendingPostingRule.PesoSubsegmento,
        DimensionDeCartera.Restructured => LendingPostingRule.PesoReestructurado,
        DimensionDeCartera.OnBehalfOfAssociate => LendingPostingRule.PesoPorCuentaDelAsociado,
        DimensionDeCartera.Branch => LendingPostingRule.PesoSucursal,
        DimensionDeCartera.Operation => LendingPostingRule.PesoHecho,
        _ => 0,
    };
}
