using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Cuenta del plan de la empresa (feature 009). Los niveles 1 a 4 vienen del catálogo y no se
/// editan; las auxiliares (5 y 6) las crea la empresa. Sólo las del nivel de movimiento
/// configurado (<see cref="IsMovement"/>) reciben movimientos y pueden parametrizarse en otros
/// módulos (FR-009). Las <b>reglas</b> (módulos habilitados, exige tercero, documento cruce,
/// centro de costo, sucursal, base gravable) gobiernan toda línea que llegue a la cuenta, venga
/// de la digitación o de un módulo (FR-014), y quedan bloqueadas en cuanto la cuenta tiene
/// movimientos en el ejercicio (FR-012, <see cref="FirstMovementAt"/>).
///
/// <para>
/// Reescrita en la 009: la versión heredada de SOLIDO arrastraba más de cincuenta atributos
/// que ninguna pantalla usaba y unas banderas que nadie hacía cumplir.
/// </para>
/// </summary>
public class ChartOfAccount : AuditableEntity
{
    /// <summary>Nomenclatura de la cooperativa (hasta 12 dígitos); prefijo del código del padre (FR-008).</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>1 clase · 2 grupo · 3 cuenta · 4 subcuenta · 5 auxiliar · 6 sub-auxiliar.</summary>
    public byte Level { get; set; }

    public AccountNature Nature { get; set; }

    public int? ParentId { get; set; }
    public ChartOfAccount? Parent { get; set; }
    public ICollection<ChartOfAccount> Children { get; set; } = [];

    public string NiifItemCode { get; set; } = string.Empty;
    public AccountOrigin Origin { get; set; }

    /// <summary><c>Level == AccountingSetup.MovementLevel</c>; lo escribe el handler para no recalcularlo en cada consulta.</summary>
    public bool IsMovement { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Fecha del primer movimiento contabilizado; la fija el contrato de contabilización y bloquea las reglas.</summary>
    public DateOnly? FirstMovementAt { get; set; }

    // ---- reglas de movimiento (sólo tienen sentido en cuentas de movimiento) ----
    public AccountingModules EnabledModules { get; set; }
    public bool RequiresThirdParty { get; set; }
    public bool RequiresCrossDocument { get; set; }
    public bool RequiresCostCenter { get; set; }

    /// <summary>La sucursal se elige explícitamente; sin esta regla se toma la propuesta (principal o la del usuario).</summary>
    public bool RequiresBranch { get; set; }

    // ---- cuenta bancaria (conciliación) ----
    public int? BankId { get; set; }
    public Bank? Bank { get; set; }
    public string? BankAccountNumber { get; set; }

    // ---- cuenta de impuesto ----
    public TaxKind TaxKind { get; set; } = TaxKind.None;
    public string? TaxConceptCode { get; set; }
    public bool RequiresTaxBase { get; set; }
    public ICollection<AccountTaxRate> TaxRates { get; set; } = [];

    public bool EsBancaria => BankId.HasValue;
    public bool EsDeImpuesto => TaxKind != TaxKind.None;
}
