namespace IngenIA365ERP.Application.Common.Integration.Lending;

/// <summary>
/// Lo que dice en español cada aviso que trae una simulación o una aplicación de pago del contrato de obligaciones (<c>Notices</c>, códigos como
/// <c>Lending.Payment.AdvanceDoesNotReduceInterest</c>). El código no se le muestra a nadie (FR-013): lo traducen la simulación de Cartera y la Caja de
/// Recaudo, que no conoce Cartera más que por este contrato. 2026-11-24 (pedido del dueño): la Caja mostraba los códigos tal cual. (nuevo)
/// </summary>
public static class AvisosDelContratoDeObligaciones
{
    public const string AnticipoNoReduceIntereses = "Lending.Payment.AdvanceDoesNotReduceInterest";
    public const string PreferenciaAsumida = "Lending.Payment.PreferenceAssumed";
    public const string TratadoComoCancelacion = "Lending.Payment.TreatedAsPayoff";
    public const string InteresDeLaCuotaCompleta = "Lending.Payment.FullInstallmentInterestCharged";

    /// <summary>El texto del aviso; un código que no conoce vuelve tal cual (mejor verlo que perderlo).</summary>
    public static string Texto(string codigo) => codigo switch
    {
        AnticipoNoReduceIntereses =>
            "El anticipo queda guardado y paga cada cuota cuando vence: no reduce intereses. Si el asociado quiere pagar menos intereses, use «Abono extraordinario a capital».",
        PreferenciaAsumida =>
            "El abono quedó como saldo a favor: no se eligió si reduce el plazo o la cuota y el deudor no tiene preferencia registrada. Elija «Reducir el plazo» o «Reducir la cuota».",
        TratadoComoCancelacion =>
            "El abono alcanza todo el capital por vencer: se aplica como cancelación del crédito y lo que sobra se devuelve.",
        InteresDeLaCuotaCompleta =>
            "Al cancelar antes del vencimiento se cobra el interés completo de la cuota en curso, no sólo el causado hasta la fecha de pago.",
        "Lending.Prevalidation.NotPostable" => "Contabilidad no tiene regla para este hecho. Pida a la contadora completar la matriz.",
        "Lending.Prevalidation.NoResponse" => "Contabilidad no respondió a tiempo: la operación sigue y queda marcada para revisión.",
        "Lending.Usury.ProjectedWithLastKnown" => "La usura de algún día todavía no está certificada: se proyectó con la última conocida.",
        _ => codigo,
    };

    /// <summary>Los textos de una lista de avisos, en el mismo orden.</summary>
    public static IReadOnlyList<string> Textos(IEnumerable<string> codigos) => codigos.Select(Texto).ToList();
}
