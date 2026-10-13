namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos de lo que llega a Cartera y de las reservas de número (feature 013, US6, T0602, T0603): estados del recibo, tipos de mensaje, el estado de
/// la validación de una venta y el de una reserva. Ningún texto muestra códigos técnicos ni el nombre de otro sistema. (nuevo)
/// </summary>
public static class TextosDeIntegracion
{
    public static IReadOnlyList<(string Valor, string Texto)> EstadosDeRecibo { get; } =
    [
        ("Processed", "Procesado"),
        ("ValidationFailed", "Validación fallida"),
        ("Rejected", "Rechazado"),
        ("NotIncorporated", "No incorporado"),
        ("ConfirmedByException", "Confirmado por excepción"),
    ];

    public static string EstadoDeRecibo(string? v) => EstadosDeRecibo.FirstOrDefault(e => e.Valor == v).Texto ?? v ?? "—";

    public static IReadOnlyList<(string Valor, string Texto)> TiposDeMensaje { get; } =
    [
        ("VentaACreditoRegistrada", "Venta a crédito"),
        ("AjusteDeVentaACredito", "Ajuste de venta a crédito"),
        ("ResultadoDelGiro", "Resultado del giro"),
    ];

    public static string TipoDeMensaje(string? v) => v switch
    {
        "ObligacionCreada" => "Obligación creada",
        "ObligacionRechazada" => "Obligación rechazada",
        _ => TiposDeMensaje.FirstOrDefault(e => e.Valor == v).Texto ?? v ?? "—",
    };

    public static string Modulo(string? v) => v switch
    {
        "INV" => "Comercial",
        "TES" => "Tesorería",
        "REC" => "Recaudo",
        "CAR" => "Cartera",
        _ => v ?? "—",
    };

    public static string ResultadoDeEvaluacion(string? v) => v switch
    {
        "Accepted" => "Aceptado",
        "ValidationFailed" => "Validación fallida",
        "NotConvertible" => "No convertible",
        "NotIncorporated" => "No incorporado",
        _ => v ?? "—",
    };

    public static IReadOnlyList<(string Valor, string Texto)> EstadosDeReserva { get; } =
    [
        ("Reserved", "Reservado"),
        ("Used", "Usado"),
        ("Released", "Liberado"),
        ("Expired", "Vencido"),
    ];

    public static string EstadoDeReserva(string? v) => EstadosDeReserva.FirstOrDefault(e => e.Valor == v).Texto ?? v ?? "—";
}
