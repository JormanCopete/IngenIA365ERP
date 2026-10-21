using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Accounting.Lending;

/// <summary>
/// Qué tipo de comprobante y qué documento cruce por defecto usa un hecho de Cartera (<c>ACC_LendingVoucherMappings</c>; feature 013,
/// T0634, T54; data-model parte 3 §5.2; contracts/contabilidad.md §4.2 y §5). Una fila por hecho y, como excepción, por hecho y clase
/// de recaudador (<c>PagoAplicado|Payroll</c> → <c>DN</c>, C79). El tipo debe ser <c>Usage = Module</c>, <c>ModuleCode = CAR</c> y
/// activo; lo valida quien lo guarda (<c>SetLendingVoucherMappingCommand</c>) y quien lo usa. El tipo de una unidad lo da su hecho
/// principal (C01): el mapeo de un hecho que viaja en la unidad de otro no se usa.
///
/// <para>
/// Sin vigencia, como en la 012: un cambio vale para lo que se contabilice después y queda en el diff de auditoría. Por eso tipo y
/// cruce se cambian en su sitio; el hecho y la clase, que forman <see cref="MappingKey"/>, no. Lo sembrado lleva <see cref="IsSeeded"/>
/// y queda «pendiente de validar» (<see cref="ValidatedAt"/> nulo) hasta que la contadora lo confirma (C6).
/// </para>
/// </summary>
public class LendingVoucherMapping : AuditableEntity
{
    /// <summary>Lo que va en la clave cuando la fila vale para toda clase de recaudador.</summary>
    public const string Cualquiera = "*";

    /// <summary>Para EF.</summary>
    private LendingVoucherMapping() { }

    public LendingVoucherMapping(string operation, string? collectorClass, int voucherTypeId, int? crossDocumentTypeId = null, bool isSeeded = false)
    {
        if (string.IsNullOrWhiteSpace(operation)) throw new ArgumentException("El hecho es obligatorio.", nameof(operation));
        Operation = operation.Trim();
        CollectorClass = Clase(collectorClass);
        VoucherTypeId = voucherTypeId;
        CrossDocumentTypeId = crossDocumentTypeId;
        IsSeeded = isSeeded;
        MappingKey = ClaveDe(Operation, CollectorClass);
    }

    /// <summary>Tipo del hecho (<c>OperacionesDeCartera</c>).</summary>
    public string Operation { get; private set; } = string.Empty;

    /// <summary>Excepción por clase de recaudador (valor de <c>ClaseDeRecaudador</c>); nulo = toda la operación.</summary>
    public string? CollectorClass { get; private set; }

    public int VoucherTypeId { get; set; }

    public VoucherType? VoucherType { get; set; }

    /// <summary>Cruce de las líneas cuya cuenta lo exige (<c>PG</c> con el número del crédito en la semilla).</summary>
    public int? CrossDocumentTypeId { get; set; }

    public CrossDocumentType? CrossDocumentType { get; set; }

    /// <summary><c>{Operation}|{CollectorClass|*}</c>; único entre las vivas.</summary>
    public string MappingKey { get; private set; } = string.Empty;

    /// <summary>Lo dejó la semilla (<c>LendingVoucherMappingsSeeder</c>).</summary>
    public bool IsSeeded { get; private set; }

    /// <summary>Conformidad de la contadora; nula = «pendiente de validar».</summary>
    public DateTime? ValidatedAt { get; private set; }

    public string? ValidatedBy { get; private set; }

    /// <summary>La contadora confirma el mapeo (o lo cambia: cambiarlo también es confirmarlo).</summary>
    public void Validar(DateTime cuando, string quien)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(quien);
        ValidatedAt = cuando;
        ValidatedBy = quien.Trim();
    }

    public static string ClaveDe(string operation, string? collectorClass) =>
        operation.Trim() + "|" + (Clase(collectorClass) ?? Cualquiera);

    private static string? Clase(string? valor) =>
        string.IsNullOrWhiteSpace(valor) || valor.Trim() == Cualquiera ? null : valor.Trim();
}
