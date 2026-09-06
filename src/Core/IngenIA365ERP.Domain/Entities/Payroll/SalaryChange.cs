using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_SalaryChanges] (nom_novsalario).</summary>
public class SalaryChange : AuditableEntityLong
{
    public int PayrollCompanyId { get; set; }
    public int EmployeeId { get; set; }
    public DateTime EffectiveDate { get; set; }
    public decimal NewSalary { get; set; }

    [MaxLength(20)]
    public string? UserName { get; set; }

    public DateTime? EntryDate { get; set; }

    // Navigation
    public Employee? Employee { get; set; }

    /// <summary>Largo de la columna legada <c>UserName</c> (SOLIDO): varchar(20).</summary>
    public const int UserNameMaxLength = 20;

    /// <summary>
    /// Recorta el usuario a lo que cabe en la columna legada. Un correo como nombre de
    /// usuario no cabe y PostgreSQL rechaza el INSERT entero (22001). Quién lo hizo de
    /// verdad va en <c>CreatedBy</c>, sin recorte.
    /// </summary>
    public static string? RecortarUsuario(string? userName) =>
        userName is null ? null : userName.Length <= UserNameMaxLength ? userName : userName[..UserNameMaxLength];
}
