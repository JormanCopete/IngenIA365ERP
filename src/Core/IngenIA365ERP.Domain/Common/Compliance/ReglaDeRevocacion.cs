using System.Globalization;
using System.Text;
using IngenIA365ERP.Domain.Enums.Compliance;

namespace IngenIA365ERP.Domain.Common.Compliance;

/// <summary>
/// Si procede revocar (o suprimir) la autorización de una finalidad (feature 013, T69, S10; contracts/interfaces.md
/// §10.3; FR-006). Pura: recibe lo que respondió la consulta de obligaciones (<c>ObligacionesAsync</c> con codeudor y
/// cerradas) y los límites legales de permanencia del dato negativo; no los lee.
///
/// <list type="bullet">
/// <item><see cref="DataAuthorizationPurpose.ContactChannel"/> procede siempre.</item>
/// <item>Las demás finalidades no proceden mientras haya un deber legal de conservar: una obligación viva como deudor o
/// codeudor (<see cref="MotivoDeDeber.ObligacionViva"/>) o la persona «migrada, sin activar»
/// (<see cref="MotivoDeDeber.MigradaSinActivar"/>).</item>
/// <item>Para <see cref="DataAuthorizationPurpose.CreditBureauReport"/>, además, una obligación cerrada cuyo dato negativo
/// sigue dentro de su permanencia (<see cref="MotivoDeDeber.PermanenciaDelDatoNegativo"/>): desde el cierre, los días de
/// mora por <c>PERMANENCIA_NEGATIVO_VECES_MORA</c>, sin pasar de <c>PERMANENCIA_NEGATIVO_MESES_MAXIMOS</c>.</item>
/// </list>
/// La <see cref="DecisionDeRevocacion.Constancia"/> deja escrito el porqué; se sella en la solicitud.
/// </summary>
public static class ReglaDeRevocacion
{
    public static DecisionDeRevocacion Evaluar(
        DataAuthorizationPurpose finalidad,
        DateOnly hoy,
        bool migradaSinActivar,
        IEnumerable<ObligacionParaRevocacion> obligaciones,
        LimitesDePermanencia permanencia)
    {
        ArgumentNullException.ThrowIfNull(obligaciones);
        ArgumentNullException.ThrowIfNull(permanencia);

        if (finalidad == DataAuthorizationPurpose.ContactChannel)
            return Decidir(finalidad, []);

        var deberes = new List<DeberDeConservar>();
        var lista = obligaciones.ToList();

        foreach (var o in lista.Where(o => o.IsLive))
            deberes.Add(new DeberDeConservar(MotivoDeDeber.ObligacionViva, o.Numero, null,
                $"La obligación {o.Numero} está vigente: hay deber de conservar los datos mientras exista."));

        if (migradaSinActivar)
            deberes.Add(new DeberDeConservar(MotivoDeDeber.MigradaSinActivar, null, null,
                "La persona tiene obligaciones migradas que todavía no se activan."));

        if (finalidad == DataAuthorizationPurpose.CreditBureauReport)
        {
            foreach (var o in lista.Where(o => !o.IsLive && o.ClosedOn is not null && o.DiasDeMoraDelDatoNegativo > 0))
            {
                var hasta = permanencia.Hasta(o.ClosedOn!.Value, o.DiasDeMoraDelDatoNegativo);
                if (hoy <= hasta)
                    deberes.Add(new DeberDeConservar(MotivoDeDeber.PermanenciaDelDatoNegativo, o.Numero, hasta,
                        $"La obligación {o.Numero} se cerró el {o.ClosedOn!.Value:yyyy-MM-dd} con {o.DiasDeMoraDelDatoNegativo} días de mora: "
                        + $"el dato negativo permanece hasta el {hasta:yyyy-MM-dd}."));
            }
        }

        return Decidir(finalidad, deberes);
    }

    private static DecisionDeRevocacion Decidir(DataAuthorizationPurpose finalidad, IReadOnlyList<DeberDeConservar> deberes)
    {
        var sb = new StringBuilder();
        if (deberes.Count == 0)
        {
            sb.Append(CultureInfo.InvariantCulture, $"Procede la revocación de la finalidad {finalidad}: no hay deber legal de conservar ni reportar.");
        }
        else
        {
            sb.Append(CultureInfo.InvariantCulture, $"No procede la revocación de la finalidad {finalidad} mientras exista el deber legal:");
            foreach (var d in deberes) sb.Append(' ').Append(d.Explicacion);
        }
        return new DecisionDeRevocacion(deberes.Count == 0, deberes, sb.ToString());
    }
}

/// <summary>Lo que la regla necesita de cada obligación de la persona (deudor o codeudor, vivas y cerradas).</summary>
/// <param name="DiasDeMoraDelDatoNegativo">Días de mora que dejó el dato negativo al cerrar; cero si se pagó al día.</param>
public sealed record ObligacionParaRevocacion(string Numero, bool IsLive, DateOnly? ClosedOn, int DiasDeMoraDelDatoNegativo);

/// <summary>
/// Límites legales de permanencia del dato negativo (<c>PERMANENCIA_NEGATIVO_VECES_MORA</c>,
/// <c>PERMANENCIA_NEGATIVO_MESES_MAXIMOS</c>), leídos con vigencia por quien llama.
/// </summary>
public sealed record LimitesDePermanencia(decimal VecesMora, int MesesMaximos)
{
    /// <summary>Último día de permanencia: el cierre más los días de mora por las veces, sin pasar del tope de meses.</summary>
    public DateOnly Hasta(DateOnly cerradaEl, int diasDeMora)
    {
        if (VecesMora < 0m) throw new ArgumentOutOfRangeException(nameof(VecesMora), VecesMora, "Las veces de mora no pueden ser negativas.");
        ArgumentOutOfRangeException.ThrowIfNegative(MesesMaximos);
        var dias = (int)Math.Ceiling(diasDeMora * VecesMora);
        var porMora = cerradaEl.AddDays(dias);
        var tope = cerradaEl.AddMonths(MesesMaximos);
        return porMora < tope ? porMora : tope;
    }
}

/// <summary>Por qué hay deber de conservar o reportar.</summary>
public enum MotivoDeDeber
{
    ObligacionViva = 1,
    MigradaSinActivar = 2,
    PermanenciaDelDatoNegativo = 3,
}

/// <summary>Un deber que impide revocar: su motivo, la obligación (si aplica), hasta cuándo (permanencia) y el texto.</summary>
public sealed record DeberDeConservar(MotivoDeDeber Motivo, string? NumeroDeObligacion, DateOnly? Hasta, string Explicacion);

/// <summary>Si procede, los deberes que lo impiden y la constancia que se sella en la solicitud del titular.</summary>
public sealed record DecisionDeRevocacion(bool Procede, IReadOnlyList<DeberDeConservar> Deberes, string Constancia);
