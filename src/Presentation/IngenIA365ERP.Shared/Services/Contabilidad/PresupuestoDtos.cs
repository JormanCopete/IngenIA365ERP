namespace IngenIA365ERP.Shared.Services.Contabilidad;

// Espejo de Application/Accounting/Budgets/BudgetDtos.cs (feature 009 E2, US9). Los nombres de
// propiedad son los de la API, letra por letra, para que System.Text.Json los case sin atributos;
// `Status` viaja como texto (None|Draft|Approved|Superseded) a propósito, para que la pantalla no
// dependa del convertidor global de enums.

/// <summary>Una versión del presupuesto de un año: quién la aprobó, cuándo y por qué cambió.</summary>
public sealed record VersionPresupuestoDto(int Version, string Status, DateTime? ApprovedAt, string? ApprovedBy, string? ChangeReason, DateTime CreatedAt, string? CreatedBy);

/// <summary>Una fila cuenta × (sucursal, centro) con sus doce cuotas (enero..diciembre) y el total.</summary>
public sealed record LineaPresupuestoDto(
    Guid AccountPublicId, string AccountCode, string AccountName,
    Guid? BranchPublicId, string? BranchName,
    Guid? CostCenterPublicId, string? CostCenterName,
    IReadOnlyList<decimal> Amounts, decimal Total);

/// <summary>El presupuesto vigente (o la versión pedida) de un año. <c>Status = "None"</c> y sin líneas cuando el año no tiene presupuesto.</summary>
public sealed record PresupuestoDto(
    int Year, int Version, string Status, DateTime? ApprovedAt, string? ApprovedBy, string? ChangeReason,
    IReadOnlyList<VersionPresupuestoDto> Versions, IReadOnlyList<LineaPresupuestoDto> Lines)
{
    public bool Existe => Status != "None";
    public bool EsBorrador => Status == "Draft";
    public bool EstaAprobado => Status == "Approved";
    public string EstadoTexto => Status switch
    {
        "None" => "Sin presupuesto",
        "Draft" => "Borrador",
        "Approved" => "Aprobado",
        "Superseded" => "Reemplazado",
        _ => Status,
    };
}

/// <summary>Lo que se envía por cada línea al crear o actualizar: la cuenta y las doce cuotas.</summary>
public sealed record LineaPresupuestoInput(Guid AccountPublicId, Guid? BranchPublicId, Guid? CostCenterPublicId, IReadOnlyList<decimal> Amounts);

/// <summary>Cuerpo de <c>POST /api/accounting/budgets</c>.</summary>
public sealed record CrearPresupuestoRequest(int Year, IReadOnlyList<LineaPresupuestoInput> Lines);

/// <summary>Cuerpo de <c>PUT /api/accounting/budgets/{year}</c>: sobre un aprobado, <c>Reason</c> es obligatorio y crea versión.</summary>
public sealed record ActualizarPresupuestoRequest(IReadOnlyList<LineaPresupuestoInput> Lines, string? Reason);

/// <summary>
/// Cuerpo de <c>POST /api/accounting/budgets/{year}/distribute</c>. <c>Mode</c>: <c>equal</c> (total/12,
/// resto en diciembre), <c>percent</c> (<c>Values</c> son porcentajes que suman 100) o <c>manual</c>
/// (<c>Values</c> son pesos que suman <c>Total</c>).
/// </summary>
public sealed record DistribucionInput(
    Guid AccountPublicId, Guid? BranchPublicId, Guid? CostCenterPublicId,
    decimal Total, string Mode, IReadOnlyList<decimal>? Values, string? Reason);
