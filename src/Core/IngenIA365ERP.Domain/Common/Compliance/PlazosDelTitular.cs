using IngenIA365ERP.Domain.Common.Calendar;
using IngenIA365ERP.Domain.Enums.Compliance;

namespace IngenIA365ERP.Domain.Common.Compliance;

/// <summary>
/// Plazos de las solicitudes del titular de los datos en días hábiles nacionales (feature 013, T69, S10; data-model
/// parte 3 §4.9, §12.5). Puro: los días llegan como límites legales con vigencia (<see cref="LimitesDelTitular"/>, de
/// los <c>LIMITE</c> <c>TITULAR_*</c>) y el calendario con los festivos nacionales; ningún plazo está en el código.
///
/// <list type="bullet">
/// <item>La consulta (<see cref="DataSubjectRequestKind.Query"/>) vence a los días de consulta; las demás clases
/// (actualizar, rectificar, suprimir, revocar), a los de reclamo.</item>
/// <item>Se cuentan desde el día siguiente al recibo (<see cref="CalendarioHabil.SumarHabiles"/>).</item>
/// <item>La prórroga suma sus días hábiles al vencimiento y se concede <b>una sola vez</b>.</item>
/// </list>
/// </summary>
public static class PlazosDelTitular
{
    public static DateOnly Vencimiento(DataSubjectRequestKind clase, DateOnly recibidaEl, LimitesDelTitular limites, CalendarioHabil calendario)
    {
        ArgumentNullException.ThrowIfNull(limites);
        ArgumentNullException.ThrowIfNull(calendario);
        var dias = clase == DataSubjectRequestKind.Query ? limites.ConsultaDias : limites.ReclamoDias;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dias, nameof(limites));
        return calendario.SumarHabiles(recibidaEl, dias);
    }

    /// <summary>El nuevo vencimiento al prorrogar. Lanza si la solicitud ya se prorrogó (la prórroga es una sola).</summary>
    public static DateOnly Prorroga(DataSubjectRequestKind clase, DateOnly vencimiento, bool yaProrrogada, LimitesDelTitular limites, CalendarioHabil calendario)
    {
        ArgumentNullException.ThrowIfNull(limites);
        ArgumentNullException.ThrowIfNull(calendario);
        if (yaProrrogada)
            throw new InvalidOperationException("La solicitud ya se prorrogó: la prórroga se concede una sola vez.");
        var dias = clase == DataSubjectRequestKind.Query ? limites.ConsultaProrrogaDias : limites.ReclamoProrrogaDias;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dias, nameof(limites));
        return calendario.SumarHabiles(vencimiento, dias);
    }
}

/// <summary>
/// Los cuatro límites del titular vigentes a la fecha del recibo: <c>TITULAR_CONSULTA_DIAS</c>,
/// <c>TITULAR_CONSULTA_PRORROGA_DIAS</c>, <c>TITULAR_RECLAMO_DIAS</c>, <c>TITULAR_RECLAMO_PRORROGA_DIAS</c>.
/// </summary>
public sealed record LimitesDelTitular(int ConsultaDias, int ConsultaProrrogaDias, int ReclamoDias, int ReclamoProrrogaDias);
