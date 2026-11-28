using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>
/// 2026-11-27 (feature 028, E2, T071; FR-023, D4, H1): <b>la cuota enviada en la caja</b>. Cuando la cuota del período de un crédito ya va en el archivo de la
/// pagaduría o en la nómina propia, «Todo para cancelar (F7)» y la cancelación del crédito lo avisan siempre, y según <c>Pagos.CuotaEnviadaAlCancelar</c> de la
/// línea —que Cartera dice en el listado, porque la caja no lee sus parámetros—:
/// <list type="bullet">
/// <item><c>Incluir</c> (de fábrica): la cancelación la cobra, como siempre, con el aviso;</item>
/// <item><c>Excluir</c>: F7 propone «todo menos la cuota enviada» (modalidad 8): la cuota queda en el crédito tal como se envió y la paga su canal;</item>
/// <item><c>Preguntar</c>: F7 propone la cancelación completa y el cajero puede dejar la cuota.</item>
/// </list>
/// Dejarla sólo se ofrece si Cartera dio lo que cuesta (la cuota sigue en su canal sin respuesta y es la causada del período) y la admite. Puro: lo usa
/// <c>Pages/Recaudo/Caja.razor</c>. (nuevo)
/// 2026-11-27 (feature 028, D13 ajustada por el dueño): dentro del tope legal del prepago Cartera la deja con el interés proporcional
/// (<c>SentInstallmentLeftRule = Proporcional</c>): el aviso lo dice, con lo que sobraría si su canal la paga completa.
/// </summary>
public static class CuotaEnviadaEnLaCaja
{
    /// <summary>La cancelación total (modalidad 1).</summary>
    public const int Cancelacion = 1;

    /// <summary>Todo menos la cuota enviada (modalidad 8, <c>PayoffExceptCurrentInstallment</c>).</summary>
    public const int SalvoLaCuota = 8;

    public const string Incluir = "Incluir";
    public const string Excluir = "Excluir";
    public const string Preguntar = "Preguntar";

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>¿Se puede cancelar el crédito dejando la cuota enviada? La línea lo deja («Excluir» o «Preguntar»), Cartera dio el valor y admite la modalidad.</summary>
    public static bool OfreceDejarla(ObligacionPagableDto o)
    {
        ArgumentNullException.ThrowIfNull(o);
        return o.SentInstallment is not null && o.SentInstallmentAtPayoff is Excluir or Preguntar && o.PayoffExceptSentInstallmentAmount is > 0m
               && (o.AdmittedModalities is not { Count: > 0 } m || m.Contains(SalvoLaCuota));
    }

    /// <summary>¿La caja propone dejarla sin preguntar? Con «Excluir».</summary>
    public static bool LaPropone(ObligacionPagableDto o) => OfreceDejarla(o) && o.SentInstallmentAtPayoff == Excluir;

    /// <summary>¿El cajero elige si la deja? Con «Preguntar».</summary>
    public static bool ElCajeroElige(ObligacionPagableDto o) => OfreceDejarla(o) && o.SentInstallmentAtPayoff == Preguntar;

    /// <summary>La modalidad y el valor con que «Todo para cancelar (F7)» agrega el crédito: todo menos la cuota con «Excluir»; si no, la cancelación total.</summary>
    public static (int Modalidad, decimal Valor) ParaCancelar(ObligacionPagableDto o) =>
        LaPropone(o) ? (SalvoLaCuota, o.PayoffExceptSentInstallmentAmount!.Value) : (Cancelacion, o.PayoffAmount);

    /// <summary>La modalidad y el valor al marcar o desmarcar «dejar la cuota» (con «Preguntar», o la modalidad elegida a mano).</summary>
    public static (int Modalidad, decimal Valor) AlDejarla(ObligacionPagableDto o, bool dejarla) =>
        dejarla && OfreceDejarla(o) ? (SalvoLaCuota, o.PayoffExceptSentInstallmentAmount!.Value) : (Cancelacion, o.PayoffAmount);

    /// <summary>
    /// El aviso de la cuota enviada cuando el crédito se cancela (modalidad 1) o se cancela dejándola (8): qué pasa con ella. Nulo sin cuota enviada o con otra
    /// modalidad.
    /// </summary>
    public static string? Aviso(ObligacionPagableDto o, int? modalidad)
    {
        ArgumentNullException.ThrowIfNull(o);
        if (o.SentInstallment is not { } e || modalidad is not (Cancelacion or SalvoLaCuota)) return null;
        var cuota = e.InstallmentNumber is { } n ? $"la cuota {n}" : "la cuota del período";
        var canal = e.EnNominaPropia ? "la nómina" : "la pagaduría";
        if (modalidad == SalvoLaCuota)
            return o.SentInstallmentLeftRule == Proporcional
                ? $"{e.Explanation} Se cancela todo menos {cuota}: queda en el crédito"
                  + (o.SentInstallmentLeftAmount is { } dejada ? $" por {dejada.ToString("C0", Colombia)}" : string.Empty)
                  + $", con el interés proporcional ({RazonDelProporcional(o.SentInstallmentLeftRuleReason)}), para que la pague {canal}."
                  + DeMas(o, e)
                : $"{e.Explanation} Se cancela todo menos {cuota}: queda en el crédito"
                  + (o.SentInstallmentLeftAmount is { } queda ? $" por {queda.ToString("C0", Colombia)}" : string.Empty)
                  + $", tal como se envió, para que la pague {canal}.";
        var despues = e.EnNominaPropia
            ? "Si el crédito queda cancelado antes de aprobar la nómina, su descuento baja a cero con aviso."
            : "Lo que la pagaduría gire después por ella quedará como saldo de la pagaduría.";
        var opcion = OfreceDejarla(o) ? $" Puede dejarla: «{CajaClient.Modalidad(SalvoLaCuota)}»." : string.Empty;
        return $"{e.Explanation} La cancelación la cobra. {despues}{opcion}";
    }

    /// <summary>2026-11-27 (feature 028, D13 ajustada por el dueño): la regla con que Cartera deja la cuota dentro del tope legal del prepago.</summary>
    public const string Proporcional = "Proporcional";

    /// <summary>Por qué el proporcional, en palabras.</summary>
    private static string RazonDelProporcional(string? motivo) => motivo switch
    {
        "LegalPrepaymentCapUnknown" => "faltan los valores nacionales para saber si el crédito está fuera del tope legal del prepago",
        _ => "dentro del tope legal del prepago sin penalidad, Ley 1555 de 2012",
    };

    /// <summary>Con el proporcional: lo que sobra si su canal paga la cuota completa, tal como se envió.</summary>
    private static string DeMas(ObligacionPagableDto o, CuotaEnviadaDeLaCajaDto e)
    {
        if (o.SentInstallmentAsSentAmount is not { } completa || o.SentInstallmentLeftAmount is not { } dejada || completa <= dejada) return string.Empty;
        var deMas = (completa - dejada).ToString("C0", Colombia);
        return e.EnNominaPropia
            ? $" Si la nómina descuenta la cuota completa ({completa.ToString("C0", Colombia)}), los {deMas} de más vuelven al neto del empleado."
            : $" Si la pagaduría gira la cuota completa ({completa.ToString("C0", Colombia)}), los {deMas} de más no entran al crédito: quedan como saldo de la pagaduría.";
    }
}
