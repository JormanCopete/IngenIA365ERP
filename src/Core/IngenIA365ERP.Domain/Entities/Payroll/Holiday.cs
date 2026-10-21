using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_Holidays]. Calendario de festivos (feature 010, R6). La lista de la Ley
/// 51 de 1983 es un valor legal y por eso <b>no</b> vive en <c>Domain/Payroll</c>: la produce el
/// generador de la semilla (<c>FestivosLey51</c>, en <c>Domain/Common/Calendar</c> desde la feature 013) y el cálculo de días hábiles
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

    /// <summary>
    /// Feature 013 (T93, C68): el <c>PublicId</c> central (<c>SourcePublicId</c>) del <c>FESTIVO</c> nacional del que esta
    /// fila es proyección; nulo en los festivos locales (<c>Decreed</c>, <c>Manual</c>) y en los Ley 51 que la réplica no ha
    /// adoptado todavía. Lo escribe sólo <c>EfectoDeReplicacionEnNomina</c>.
    /// </summary>
    public Guid? NationalValuePublicId { get; set; }

    /// <summary>
    /// Un festivo sembrado (Ley 51) o nacional (proyección de la réplica, feature 013) no se elimina desde la pantalla; los
    /// decretados y manuales sí.
    /// </summary>
    public bool EsSembrado => Origin is HolidayOrigin.Ley51Fixed or HolidayOrigin.Ley51MovedToMonday or HolidayOrigin.Ley51Easter
        or HolidayOrigin.National;
}
