using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español de la originación (feature 013, US2, T0410–T0413): estados de la solicitud, clases y resultados de las validaciones,
/// decisiones, cupos, bloqueos, periodicidades y sistemas de amortización, con la numeración de <c>Domain/Enums/Lending</c>. Ninguno muestra un
/// código técnico (FR-013). (nuevo)
/// </summary>
public static class TextosDeOriginacion
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    private static string De(IReadOnlyList<(int Valor, string Texto)> lista, int valor) =>
        lista.FirstOrDefault(p => p.Valor == valor).Texto ?? valor.ToString(CultureInfo.InvariantCulture);

    public static IReadOnlyList<(int Valor, string Texto)> EstadosDeSolicitud { get; } =
    [
        (1, "Borrador"), (2, "Radicada"), (3, "Pendiente de decisión"), (4, "Devuelta"), (5, "Aprobada"), (6, "Negada"), (7, "Desembolsada"),
        (8, "Cupo otorgado"), (9, "Anulada"),
    ];

    public static string EstadoDeSolicitud(int v) => De(EstadosDeSolicitud, v);

    public static IReadOnlyList<(int Valor, string Texto)> Periodicidades { get; } =
        [(30, "Mensual"), (15, "Quincenal"), (10, "Decadal"), (7, "Semanal"), (60, "Bimestral"), (90, "Trimestral"), (180, "Semestral"), (360, "Anual"), (0, "Pago único")];

    public static string Periodicidad(int? v) => v is { } p ? De(Periodicidades, p) : "—";

    public static IReadOnlyList<(int Valor, string Texto)> SistemasDeAmortizacion { get; } =
        [(1, "Cuota fija"), (2, "Abono constante a capital"), (3, "Al vencimiento")];

    public static IReadOnlyList<(int Valor, string Texto)> PreferenciasDeAbono { get; } = [(1, "Reducir el plazo"), (2, "Reducir la cuota")];

    public static string PreferenciaDeAbono(int? v) => v is { } p ? De(PreferenciasDeAbono, p) : "—";

    public static IReadOnlyList<(int Valor, string Texto)> FormasDePago { get; } = [(1, "Caja"), (2, "Nómina propia")];

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeValidacion { get; } =
    [
        (1, "Listas restrictivas"), (2, "Centrales de riesgo"), (3, "Capacidad de pago"), (4, "Capacidad de descuento"), (5, "Endeudamiento consolidado"),
        (6, "Cupo"), (7, "Concentración"), (8, "Usura"), (9, "Requisitos de la línea"), (10, "Autorización de datos"), (11, "Puntaje"),
        (12, "Persona vinculada"), (13, "Documentos"),
    ];

    public static string ClaseDeValidacion(int v) => De(ClasesDeValidacion, v);

    public static string ResultadoDeValidacion(int v) => v switch
    {
        1 => "Cumple",
        2 => "Advertencia",
        3 => "Bloquea",
        4 => "No aplica",
        5 => "No disponible",
        _ => v.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>La clase de aviso del sistema de diseño para un resultado.</summary>
    public static string ClaseDeAviso(int resultado) => resultado switch
    {
        1 => "aviso aviso-ok",
        2 => "aviso aviso-alerta",
        3 or 5 => "aviso aviso-error",
        _ => "aviso aviso-info",
    };

    public static IReadOnlyList<(int Valor, string Texto)> ResultadosDeDecision { get; } =
        [(1, "Aprobar"), (2, "Aprobar con condiciones"), (3, "Negar"), (4, "Devolver para corregir")];

    public static string ResultadoDeDecision(int v) => De(ResultadosDeDecision, v);

    public static string Ruta(string ruta) => ruta switch
    {
        "Individual" => "Decisión individual",
        "Session" => "En sesión del órgano",
        _ => ruta,
    };

    public static string Exclusion(string? motivo) => motivo switch
    {
        null => "",
        "Filer" => "Usted radicó o pidió este asunto: no puede decidirlo.",
        "UserWithoutPerson" => "Su usuario no tiene persona asociada: pida al administrador asociarla.",
        "Applicant" => "Usted es el solicitante.",
        "Interested" => "Usted tiene interés en el asunto.",
        _ => "No puede decidir este asunto.",
    };

    public static string Sujeto(string sujeto) => sujeto switch
    {
        "CreditApplication" => "Solicitud",
        "CreditLimit" => "Cupo",
        _ => TextosDeCartera.Sujeto(sujeto),
    };

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeCupo { get; } = [(1, "Consolidado"), (2, "Por línea"), (3, "Rotativo")];

    public static string ClaseDeCupo(int v) => De(ClasesDeCupo, v);

    public static string OrigenDeCupo(int v) => v switch { 1 => "Calculado por aportes", 2 => "Asignado", 3 => "Migrado", _ => v.ToString(CultureInfo.InvariantCulture) };

    public static string EstadoDeCupo(string estado) => estado switch
    {
        "PendingDecision" => "Espera decisión",
        "Active" => "Vigente",
        "Blocked" => "Bloqueado",
        "Ended" => "Terminado",
        _ => estado,
    };

    public static IReadOnlyList<(int Valor, string Texto)> ClasesDeBloqueo { get; } =
    [
        (1, "Manual"), (2, "Mora"), (3, "Validación fallida"), (4, "Castigo"), (5, "Coincidencia en listas"), (6, "Retiro del asociado"),
        (7, "Fallecimiento"), (8, "Insolvencia"), (9, "Embargo"), (10, "Cupo por revalidar"),
    ];

    public static string ClaseDeBloqueo(int v) => De(ClasesDeBloqueo, v);

    public static string EstadoDeListas(string estado) => estado switch
    {
        "Blocked" => "Pendiente de liberar",
        "Released" => "Liberada",
        "NoMatch" => "Sin coincidencia",
        _ => estado,
    };

    public static string ResultadoDeListas(int v) => v switch
    {
        1 => "Sin coincidencia", 2 => "Posible coincidencia", 3 => "Coincidencia", 4 => "No disponible", _ => v.ToString(CultureInfo.InvariantCulture),
    };

    public static IReadOnlyList<(int Valor, string Texto)> FundamentosDeGrupo { get; } =
        [(1, "Vinculación económica"), (2, "Vinculación jurídica"), (3, "Codeudor común"), (4, "Persona vinculada")];

    public static string FundamentoDeGrupo(int v) => De(FundamentosDeGrupo, v);

    public static string EstadoDeConsulta(string estado) => estado switch
    {
        "Ok" => "Respondió",
        "NoHistory" => "Sin historia",
        "ChannelUnavailable" => "Central no disponible",
        "InvalidData" => "Datos inválidos",
        _ => estado,
    };

    public static string Pesos(decimal valor) => valor.ToString("C0", Colombia);

    public static string Porcentaje(decimal fraccion) => (fraccion * 100m).ToString("0.####", Colombia) + " %";
}
