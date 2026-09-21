using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_Holidays]. Calendario de festivos (feature 010, R6). La lista de la Ley
/// 51 de 1983 es un valor legal y por eso <b>no</b> vive en <c>Domain/Payroll</c>: la produce el
/// generador de la semilla (<c>FestivosLey51</c>, en Persistence) y el cálculo de días hábiles
/// sólo lee esta tabla, que la cooperativa edita (un puente decretado se registra aquí y queda
/// quién lo hizo). La semilla inserta lo que falta por <see cref="Date"/> y nunca toca ni borra
/// una fila <c>Decreed</c> o <c>Manual</c>.
/// </summary>
public class Holiday : AuditableEntity
{
    public DateOnly Date { get; set; }

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    public HolidayOrigin Origin { get; set; }

    /// <summary>Desnormalizado de <see cref="Date"/> para el índice por año.</summary>
    public short Year { get; set; }

    /// <summary>Un festivo sembrado (Ley 51) no se elimina desde la pantalla; los decretados y manuales sí.</summary>
    public bool EsSembrado => Origin is HolidayOrigin.Ley51Fixed or HolidayOrigin.Ley51MovedToMonday or HolidayOrigin.Ley51Easter;
}
