using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Cómo interpretar los archivos de extracto de una cuenta bancaria (feature 009, FR-056): qué
/// columna es fecha, referencia, descripción y valor (uno con signo o débito/crédito separados),
/// el formato de fecha y las filas de encabezado. Lo define el tesorero una vez con vista previa
/// y se reutiliza cada mes. Las columnas son índices basados en 1 (o 0 = no aplica).
/// </summary>
public class BankStatementColumnMap : AuditableEntity
{
    public int AccountId { get; set; }
    public ChartOfAccount? Account { get; set; }

    public int DateColumn { get; set; }
    public string DateFormat { get; set; } = "yyyy-MM-dd";
    public int ReferenceColumn { get; set; }
    public int DescriptionColumn { get; set; }
    public int AmountColumn { get; set; }
    public int DebitColumn { get; set; }
    public int CreditColumn { get; set; }
    public SignConvention SignConvention { get; set; }
    public int HeaderRows { get; set; } = 1;
    public string? Delimiter { get; set; }
}
