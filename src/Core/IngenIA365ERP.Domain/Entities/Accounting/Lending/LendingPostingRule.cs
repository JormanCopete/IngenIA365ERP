using System.Globalization;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting.Lending;

/// <summary>
/// Una regla de la matriz contable de Cartera (<c>ACC_LendingPostingRules</c>; feature 013, T0634, T52; data-model parte 3 §5.1;
/// contracts/contabilidad.md §3.1). Dice a qué cuenta va un <c>Role</c> de un hecho (<c>Operation</c>, o <c>*</c>) para una
/// combinación de dimensiones de Cartera, con vigencia. Molde: <c>InventoryPostingRule</c> de la 012.
///
/// <para>
/// Las dimensiones de Cartera van <b>por código</b> (línea, modalidad, sub-segmento, categoría, concepto, clase de recaudador,
/// motivo) y los booleanos como <c>bool?</c> (nulo = cualquiera): Contabilidad no acopla su esquema a <c>LND_</c> (T51). Sólo
/// sucursal, centro de costo y cuenta, que son de Core y de Contabilidad, llevan llave. El centro de costo <b>se asigna</b> a la
/// línea del comprobante y no se compara: no entra en <see cref="DimensionKey"/> ni pesa (C77).
/// </para>
///
/// <para>
/// <see cref="DimensionKey"/> y <see cref="SpecificityWeight"/> se calculan en el constructor y nunca se asignan a mano. La clave es
/// posicional (contracts/contabilidad.md §3.1; tasks.md, contradicción 4), con <c>*</c> donde la regla no fija valor, los booleanos
/// como <c>1</c>/<c>0</c> y la sucursal por su <c>Id</c>:
/// <c>{Operation}|{Role}|{CreditLineCode}|{ModalityCode}|{SubsegmentCode}|{RiskCategory}|{ConceptCode}|{IsEmployee}|{InGracePeriod}|{OnBehalfOfAssociate}|{Restructured}|{CollectorClass}|{ReasonCode}|{BranchId}</c>.
/// El peso suma las opcionales fijadas: línea 512, empleado 256, gracia 128, sub-segmento 64, reestructurado 32, por cuenta del
/// asociado 16, sucursal 4 y hecho 2 (el hecho sólo puede fijarse en los roles de resultado; en los demás va en <c>*</c>, lo
/// valida <c>ReglasDeLaMatrizDeCartera</c>). Son potencias de dos: dos candidatas con el mismo peso fijan las mismas dimensiones.
/// </para>
///
/// <para>
/// Normalización: línea y modalidad en mayúsculas (son códigos de catálogo); la categoría a su letra (<c>E1</c> y <c>E2</c> → <c>E</c>,
/// §3.4); sub-segmento, concepto, clase de recaudador y motivo conservan su forma porque nombran valores de catálogos y enumeraciones
/// (<c>ConLibranza</c>, <c>CurrentInterest</c>, <c>Payroll</c>). Nada se borra: una versión nueva es otra fila con la misma clave y la
/// anterior se cierra la víspera (<see cref="CerrarVigencia"/>); desactivar también cierra. La regla vieja explica lo contabilizado.
/// </para>
/// </summary>
public class LendingPostingRule : AuditableEntity
{
    /// <summary>Pesos de especificidad (T52, C77).</summary>
    public const int PesoLinea = 512;
    public const int PesoEmpleado = 256;
    public const int PesoGracia = 128;
    public const int PesoSubsegmento = 64;
    public const int PesoReestructurado = 32;
    public const int PesoPorCuentaDelAsociado = 16;
    public const int PesoSucursal = 4;
    public const int PesoHecho = 2;

    /// <summary>Lo que va en la clave donde la regla no fija valor (y en <see cref="Operation"/> cuando vale para todo hecho).</summary>
    public const string Cualquiera = "*";

    /// <summary>Para EF.</summary>
    private LendingPostingRule() { }

    public LendingPostingRule(
        string operation,
        string role,
        int accountId,
        DateOnly validFrom,
        string? creditLineCode = null,
        string? modalityCode = null,
        string? subsegmentCode = null,
        string? riskCategory = null,
        string? conceptCode = null,
        bool? isEmployee = null,
        bool? inGracePeriod = null,
        bool? onBehalfOfAssociate = null,
        bool? restructured = null,
        string? collectorClass = null,
        string? reasonCode = null,
        int? branchId = null,
        int? costCenterId = null,
        DateOnly? validTo = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("El rol es obligatorio.", nameof(role));
        if (accountId <= 0) throw new ArgumentOutOfRangeException(nameof(accountId), "La regla lleva una cuenta.");

        Operation = Texto(operation) ?? Cualquiera;
        Role = role.Trim();
        AccountId = accountId;
        ValidFrom = validFrom;
        CreditLineCode = Mayusculas(creditLineCode);
        ModalityCode = Mayusculas(modalityCode);
        SubsegmentCode = Texto(subsegmentCode);
        RiskCategory = Categoria(riskCategory);
        ConceptCode = Texto(conceptCode);
        IsEmployee = isEmployee;
        InGracePeriod = inGracePeriod;
        OnBehalfOfAssociate = onBehalfOfAssociate;
        Restructured = restructured;
        CollectorClass = Texto(collectorClass);
        ReasonCode = Texto(reasonCode);
        BranchId = branchId;
        CostCenterId = costCenterId;
        Notes = notes?.Trim() ?? string.Empty;

        DimensionKey = ClaveDe(Operation, Role, CreditLineCode, ModalityCode, SubsegmentCode, RiskCategory, ConceptCode, IsEmployee,
            InGracePeriod, OnBehalfOfAssociate, Restructured, CollectorClass, ReasonCode, BranchId);
        SpecificityWeight = PesoDe(Operation, CreditLineCode, SubsegmentCode, IsEmployee, InGracePeriod, OnBehalfOfAssociate, Restructured, BranchId);

        if (validTo is { } hasta) CerrarVigencia(hasta);
    }

    /// <summary>Tipo del hecho de <c>OperacionesDeCartera</c> (el <c>Type</c> del mensaje), o <c>*</c>.</summary>
    public string Operation { get; private set; } = Cualquiera;

    /// <summary>Código de <c>RolesDeCuentaDeCartera</c>.</summary>
    public string Role { get; private set; } = string.Empty;

    public string? CreditLineCode { get; private set; }

    /// <summary>Modalidad del <c>CatalogoDeModalidades</c> vigente (<c>CONSUMO</c>, <c>COMERCIAL</c>…).</summary>
    public string? ModalityCode { get; private set; }

    /// <summary><c>ConLibranza</c>, <c>SinLibranza</c>, <c>Empleados</c>…; tiene que ser de la modalidad.</summary>
    public string? SubsegmentCode { get; private set; }

    /// <summary><c>A</c>..<c>E</c>: la categoría contable vigente (§3.4).</summary>
    public string? RiskCategory { get; private set; }

    /// <summary>Valor de <c>LoanConcept</c> como texto.</summary>
    public string? ConceptCode { get; private set; }

    /// <summary>Deudor empleado según el estado actual de la persona maestra (C8); nulo = cualquiera.</summary>
    public bool? IsEmployee { get; private set; }

    public bool? InGracePeriod { get; private set; }

    public bool? OnBehalfOfAssociate { get; private set; }

    /// <summary>Dimensión «reestructurado» (C4).</summary>
    public bool? Restructured { get; private set; }

    /// <summary>Valor de <c>ClaseDeRecaudador</c> como texto (<c>Receipts</c>, <c>Payroll</c>, <c>Contributions</c>, <c>OwnLoanPayoff</c>, <c>Test</c>).</summary>
    public string? CollectorClass { get; private set; }

    /// <summary>Motivo del rol (forma de desembolso, nivel del deterioro, castigo o condonación, tipo de contingencia).</summary>
    public string? ReasonCode { get; private set; }

    /// <summary>Sucursal (<c>COR_Branches</c>); se compara con la sucursal del hecho.</summary>
    public int? BranchId { get; private set; }

    /// <summary>Centro de costo (<c>COR_CostCenters</c>) que se <b>asigna</b> a la línea si la cuenta lo maneja; no se compara (C77).</summary>
    public int? CostCenterId { get; private set; }

    public int AccountId { get; private set; }

    /// <summary>La clave posicional de §3.1, con <c>*</c> donde la regla no fija valor.</summary>
    public string DimensionKey { get; private set; } = string.Empty;

    /// <summary>Suma de los pesos de las opcionales que fija.</summary>
    public int SpecificityWeight { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    /// <summary>Nula = abierta.</summary>
    public DateOnly? ValidTo { get; private set; }

    /// <summary>Quién decidió y por qué (la fuente de la contadora).</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>¿Rige en esa fecha contable?</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || fecha <= ValidTo.Value);

    /// <summary>
    /// Cierra la vigencia el día <paramref name="hasta"/> (inclusive). Una versión nueva la cierra la víspera de su <c>ValidFrom</c>;
    /// desactivar, en la fecha elegida. La víspera de <see cref="ValidFrom"/> la deja sin ningún día vigente; más atrás no tiene sentido.
    /// </summary>
    public void CerrarVigencia(DateOnly hasta)
    {
        if (hasta < ValidFrom.AddDays(-1))
            throw new ArgumentOutOfRangeException(nameof(hasta), hasta, "La vigencia no puede cerrarse antes de la víspera de su inicio.");
        ValidTo = hasta;
    }

    /// <summary>La clave normalizada de §3.1; la usan la entidad y quien busca una regla por sus dimensiones.</summary>
    public static string ClaveDe(
        string? operation, string role, string? creditLineCode, string? modalityCode, string? subsegmentCode, string? riskCategory,
        string? conceptCode, bool? isEmployee, bool? inGracePeriod, bool? onBehalfOfAssociate, bool? restructured, string? collectorClass,
        string? reasonCode, int? branchId) =>
        string.Join('|',
            Texto(operation) ?? Cualquiera,
            role.Trim(),
            Mayusculas(creditLineCode) ?? Cualquiera,
            Mayusculas(modalityCode) ?? Cualquiera,
            Texto(subsegmentCode) ?? Cualquiera,
            Categoria(riskCategory) ?? Cualquiera,
            Texto(conceptCode) ?? Cualquiera,
            Booleano(isEmployee),
            Booleano(inGracePeriod),
            Booleano(onBehalfOfAssociate),
            Booleano(restructured),
            Texto(collectorClass) ?? Cualquiera,
            Texto(reasonCode) ?? Cualquiera,
            branchId?.ToString(CultureInfo.InvariantCulture) ?? Cualquiera);

    /// <summary>Línea 512, empleado 256, gracia 128, sub-segmento 64, reestructurado 32, por cuenta del asociado 16, sucursal 4, hecho 2.</summary>
    public static int PesoDe(
        string? operation, string? creditLineCode, string? subsegmentCode, bool? isEmployee, bool? inGracePeriod, bool? onBehalfOfAssociate,
        bool? restructured, int? branchId)
    {
        var peso = 0;
        if (Mayusculas(creditLineCode) is not null) peso += PesoLinea;
        if (isEmployee is not null) peso += PesoEmpleado;
        if (inGracePeriod is not null) peso += PesoGracia;
        if (Texto(subsegmentCode) is not null) peso += PesoSubsegmento;
        if (restructured is not null) peso += PesoReestructurado;
        if (onBehalfOfAssociate is not null) peso += PesoPorCuentaDelAsociado;
        if (branchId is not null) peso += PesoSucursal;
        if (Texto(operation) is not null) peso += PesoHecho;
        return peso;
    }

    /// <summary>La letra de una categoría o subcategoría (<c>E1</c> → <c>E</c>); nula si viene vacía o es <c>*</c>.</summary>
    public static string? Categoria(string? valor) =>
        Texto(valor) is { } texto ? texto[..1].ToUpperInvariant() : null;

    private static string Booleano(bool? valor) => valor is null ? Cualquiera : valor.Value ? "1" : "0";

    private static string? Texto(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || valor.Trim() == Cualquiera ? null : valor.Trim();

    private static string? Mayusculas(string? valor) => Texto(valor)?.ToUpperInvariant();
}
