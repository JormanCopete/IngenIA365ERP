using System.Globalization;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting.Inventory;

/// <summary>
/// Una regla de la matriz contable de Inventario (<c>ACC_InventoryPostingRules</c>; feature 012, T27, T478; data-model §20;
/// contracts/contabilidad.md §2). Dice a qué cuenta va un <c>Role</c> de una <c>Operation</c> para una combinación de
/// dimensiones, con vigencia.
///
/// <para>
/// Las dimensiones del módulo y de los catálogos de Core van <b>por código</b> (grupo contable, bodega, punto de venta,
/// medio de pago, tarifa, motivo): Contabilidad no acopla su esquema a <c>INV_</c>. Sólo sucursal y centro de costo, que
/// son de Core, llevan llave. Por eso esos códigos no cambian una vez creados.
/// </para>
///
/// <para>
/// <see cref="DimensionKey"/> y <see cref="SpecificityWeight"/> se calculan en el constructor y nunca se asignan a mano:
/// la clave es la del único filtrado <c>(DimensionKey, ValidFrom)</c> —con <c>*</c> donde la regla no fija valor, porque un
/// único con columnas nulas se comporta distinto en SQL Server y PostgreSQL— y el peso ordena la resolución (bodega o punto
/// 16, centro 8, sucursal 4, grupo 2). Los códigos se guardan en mayúsculas y sin espacios; el motivo conserva su forma
/// porque nombra valores de enumeraciones (<c>ShortageToCashier</c>, <c>NegativeRegularization</c>).
/// </para>
///
/// <para>
/// Nada se borra: una versión nueva es otra fila con la misma clave y la anterior se cierra la víspera
/// (<see cref="CerrarVigencia"/>); desactivar también cierra. La regla vieja hace falta para el espejo de una anulación.
/// </para>
/// </summary>
public class InventoryPostingRule : AuditableEntity
{
    /// <summary>Pesos de especificidad (T27). Potencias de dos: dos candidatas con el mismo peso fijan las mismas dimensiones.</summary>
    public const short PesoBodegaOPunto = 16;
    public const short PesoCentro = 8;
    public const short PesoSucursal = 4;
    public const short PesoGrupo = 2;

    /// <summary>Lo que va en la clave donde la regla no fija valor.</summary>
    public const string Cualquiera = "*";

    /// <summary>Para EF.</summary>
    private InventoryPostingRule() { }

    public InventoryPostingRule(
        string operation,
        string role,
        int accountId,
        DateOnly validFrom,
        string? accountingGroupCode = null,
        string? warehouseCode = null,
        string? pointOfSaleCode = null,
        string? paymentMeansCode = null,
        string? taxRateCode = null,
        decimal? taxRate = null,
        string? reasonCode = null,
        int? branchId = null,
        int? costCenterId = null,
        DateOnly? validTo = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("La operación es obligatoria.", nameof(operation));
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("El rol es obligatorio.", nameof(role));
        if (accountId <= 0) throw new ArgumentOutOfRangeException(nameof(accountId), "La regla lleva una cuenta.");

        Operation = operation.Trim();
        Role = role.Trim();
        AccountId = accountId;
        ValidFrom = validFrom;
        AccountingGroupCode = Codigo(accountingGroupCode);
        WarehouseCode = Codigo(warehouseCode);
        PointOfSaleCode = Codigo(pointOfSaleCode);
        PaymentMeansCode = Codigo(paymentMeansCode);
        TaxRateCode = Codigo(taxRateCode);
        TaxRate = taxRate;
        ReasonCode = string.IsNullOrWhiteSpace(reasonCode) ? null : reasonCode.Trim();
        BranchId = branchId;
        CostCenterId = costCenterId;
        Notes = notes?.Trim() ?? string.Empty;

        DimensionKey = ClaveDe(Operation, Role, AccountingGroupCode, WarehouseCode, PointOfSaleCode, PaymentMeansCode,
            TaxRateCode, TaxRate, ReasonCode, BranchId, CostCenterId);
        SpecificityWeight = PesoDe(AccountingGroupCode, WarehouseCode, PointOfSaleCode, BranchId, CostCenterId);

        if (validTo is { } hasta) CerrarVigencia(hasta);
    }

    /// <summary>Código de <c>OperacionesDeInventario</c> (decisiones-transversales §2.7).</summary>
    public string Operation { get; private set; } = string.Empty;

    /// <summary>Código de <c>RolesDeCuenta</c> (decisiones-transversales §2.7).</summary>
    public string Role { get; private set; } = string.Empty;

    public string? AccountingGroupCode { get; private set; }

    public string? WarehouseCode { get; private set; }

    public string? PointOfSaleCode { get; private set; }

    public string? PaymentMeansCode { get; private set; }

    public string? TaxRateCode { get; private set; }

    /// <summary>La tarifa (fracción, seis decimales) que la regla espera; va con <see cref="TaxRateCode"/> (C8).</summary>
    public decimal? TaxRate { get; private set; }

    /// <summary>Tratamiento, destino de caja, causa de ajuste o razón del kardex, según el rol (§2.7).</summary>
    public string? ReasonCode { get; private set; }

    public int? BranchId { get; private set; }

    public int? CostCenterId { get; private set; }

    public int AccountId { get; private set; }

    /// <summary><c>{Operation}|{Role}|G:…|W:…|P:…|M:…|T:código@tarifa|R:…|B:Id|C:Id</c>, con <c>*</c> donde no fija valor.</summary>
    public string DimensionKey { get; private set; } = string.Empty;

    /// <summary>Suma de los pesos de las dimensiones opcionales que fija (16/8/4/2).</summary>
    public short SpecificityWeight { get; private set; }

    public DateOnly ValidFrom { get; private set; }

    /// <summary>Nula = abierta.</summary>
    public DateOnly? ValidTo { get; private set; }

    /// <summary>Quién decidió y por qué.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>¿Rige en esa fecha de operación?</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || fecha <= ValidTo.Value);

    /// <summary>
    /// Cierra la vigencia el día <paramref name="hasta"/> (inclusive). Una versión nueva la cierra la víspera de su
    /// <c>ValidFrom</c>; desactivar, en la fecha elegida. La víspera de <see cref="ValidFrom"/> la deja sin ningún día
    /// vigente; más atrás no tiene sentido.
    /// </summary>
    public void CerrarVigencia(DateOnly hasta)
    {
        if (hasta < ValidFrom.AddDays(-1))
            throw new ArgumentOutOfRangeException(nameof(hasta), hasta, "La vigencia no puede cerrarse antes de la víspera de su inicio.");
        ValidTo = hasta;
    }

    /// <summary>La clave normalizada; la usan el constructor y quien busca una regla por sus dimensiones.</summary>
    public static string ClaveDe(
        string operation, string role, string? accountingGroupCode, string? warehouseCode, string? pointOfSaleCode,
        string? paymentMeansCode, string? taxRateCode, decimal? taxRate, string? reasonCode, int? branchId, int? costCenterId)
    {
        var tarifa = Codigo(taxRateCode) is { } codigo
            ? codigo + "@" + (taxRate is { } t ? t.ToString("0.############", CultureInfo.InvariantCulture) : Cualquiera)
            : Cualquiera;
        return string.Join('|',
            operation.Trim(),
            role.Trim(),
            "G:" + (Codigo(accountingGroupCode) ?? Cualquiera),
            "W:" + (Codigo(warehouseCode) ?? Cualquiera),
            "P:" + (Codigo(pointOfSaleCode) ?? Cualquiera),
            "M:" + (Codigo(paymentMeansCode) ?? Cualquiera),
            "T:" + tarifa,
            "R:" + (string.IsNullOrWhiteSpace(reasonCode) ? Cualquiera : reasonCode.Trim()),
            "B:" + (branchId?.ToString(CultureInfo.InvariantCulture) ?? Cualquiera),
            "C:" + (costCenterId?.ToString(CultureInfo.InvariantCulture) ?? Cualquiera));
    }

    /// <summary>Bodega o punto 16, centro 8, sucursal 4, grupo 2.</summary>
    public static short PesoDe(string? accountingGroupCode, string? warehouseCode, string? pointOfSaleCode, int? branchId, int? costCenterId)
    {
        short peso = 0;
        if (Codigo(warehouseCode) is not null || Codigo(pointOfSaleCode) is not null) peso += PesoBodegaOPunto;
        if (costCenterId is not null) peso += PesoCentro;
        if (branchId is not null) peso += PesoSucursal;
        if (Codigo(accountingGroupCode) is not null) peso += PesoGrupo;
        return peso;
    }

    private static string? Codigo(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();
}
