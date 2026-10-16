using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español de la cobranza de K4 (feature 013, US12, T0958): canales y resultados de una gestión, estado de promesas, acuerdos, procesos
/// jurídicos, campañas y avisos previos, las acciones sugeridas de una etapa y la regla de gasto. Los enums llegan como número y salen por nombre en las
/// peticiones; ninguno muestra un código técnico (FR-013). Los de la ventana de contacto están en <see cref="TextosDeCartera"/>. (nuevo)
/// </summary>
public static class TextosDeCobranza
{
    private static string Numero(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static string De(IReadOnlyList<(string Nombre, int Valor, string Texto)> lista, int valor) =>
        lista.FirstOrDefault(p => p.Valor == valor).Texto ?? Numero(valor);

    // ---------------------------------------------------------------------------------------------- gestiones --

    /// <summary>Los canales de una gestión (<c>CollectionActionChannel</c>): nombre que se manda, número que vuelve y texto.</summary>
    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> CanalesDeGestion { get; } =
    [
        ("Phone", 1, "Llamada"), ("InPerson", 2, "En persona"), ("Email", 3, "Correo"), ("Sms", 4, "SMS"), ("WhatsApp", 5, "WhatsApp"), ("Letter", 6, "Carta"),
        ("Visit", 7, "Visita"),
    ];

    public static string CanalDeGestion(int v) => De(CanalesDeGestion, v);

    /// <summary>Los resultados de una gestión (<c>CollectionActionResult</c>).</summary>
    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> ResultadosDeGestion { get; } =
    [
        ("NoAnswer", 1, "No contestó"), ("WrongNumber", 2, "Número equivocado"), ("ContactedNoCommitment", 3, "Contactado, sin compromiso"),
        ("PromiseToPay", 4, "Promesa de pago"), ("Refused", 5, "Se negó a pagar"), ("RequestsAgreement", 6, "Pide un acuerdo de pago"),
        ("ReportsDeceased", 7, "Informa que el deudor falleció"), ("Other", 8, "Otro"),
    ];

    public static string ResultadoDeGestion(int v) => De(ResultadosDeGestion, v);

    public static string RolDeContacto(int v) => v switch { 1 => "Deudor", 2 => "Codeudor", 3 => "Pagaduría", _ => Numero(v) };

    public static string EstadoDePromesa(int? v) => v switch { 1 => "Pendiente", 2 => "Cumplida", 3 => "Incumplida", _ => "Anulada" };

    public static IReadOnlyList<(int Valor, string Texto)> EstadosDePromesa { get; } = [(1, "Pendiente"), (2, "Cumplida"), (3, "Incumplida")];

    // ----------------------------------------------------------------------------------------------- agenda --

    public static string ClaseDeAgenda(string clase) => clase switch
    {
        "Promise" => "Promesa de pago",
        "FollowUp" => "Seguimiento",
        "AgreementInstallment" => "Cuota de acuerdo",
        "Stage" => "Cambio de etapa",
        _ => clase,
    };

    // ----------------------------------------------------------------------------------------------- reparto --

    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> BasesDeReparto { get; } = [("ByCount", 1, "Por cantidad de créditos"), ("ByBalance", 2, "Por saldo")];

    public static string BaseDeReparto(int v) => De(BasesDeReparto, v);

    // ------------------------------------------------------------------------------------ etapas y gasto --

    public static IReadOnlyList<(string Codigo, string Texto)> AccionesSugeridas { get; } =
    [
        ("Call", "Llamar"), ("Message", "Enviar mensaje"), ("Visit", "Visitar"), ("PriorNotice", "Enviar el aviso previo al reporte"),
        ("PaymentAgreement", "Ofrecer un acuerdo de pago"), ("LegalHandover", "Entregar al abogado"),
    ];

    public static string AccionSugerida(string codigo) => AccionesSugeridas.FirstOrDefault(a => a.Codigo == codigo).Texto ?? codigo;

    public static IReadOnlyList<(string Codigo, string Texto)> BasesDeGasto { get; } = [("PastDueAmount", "El valor vencido"), ("PastDuePrincipal", "El capital vencido")];

    public static string BaseDeGasto(string codigo) => BasesDeGasto.FirstOrDefault(b => b.Codigo == codigo).Texto ?? codigo;

    public static string MomentoDeGasto(string codigo) => codigo == "OnStageEntry" ? "Al entrar a la etapa" : codigo;

    /// <summary>El estado del aviso de un gasto (último evento del mensaje <c>DebtorMessageEventKind</c>).</summary>
    public static string EstadoDelAvisoDeGasto(int v) => v switch
    {
        1 => "En cola",
        2 => "Diferido",
        3 => "Excluido",
        4 => "Enviado",
        5 => "Entregado",
        6 => "Falló",
        7 => "Simulado",
        8 => "Cancelado",
        _ => Numero(v),
    };

    public static string OrigenDelGasto(string origen) => origen switch
    {
        "Stage" => "Etapa de cobranza",
        "LegalAction" => "Actuación del cobro jurídico",
        _ => origen,
    };

    // ------------------------------------------------------------------------------------------- acuerdos --

    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> EstadosDeAcuerdo { get; } =
        [("Proposed", 1, "Propuesto"), ("Approved", 2, "Aprobado"), ("Rejected", 3, "Rechazado"), ("Cancelled", 4, "Cancelado")];

    public static string EstadoDeAcuerdo(int v) => De(EstadosDeAcuerdo, v);

    public static string DesempenoDeAcuerdo(int? v) => v switch { 1 => "Al día", 2 => "Cumplido", 3 => "Incumplido", _ => "—" };

    // ------------------------------------------------------------------------------------------- jurídico --

    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> EstadosJuridicos { get; } =
    [
        ("Assigned", 1, "Asignado"), ("Filed", 2, "Radicado"), ("InProgress", 3, "En curso"), ("Suspended", 4, "Suspendido"), ("Concluded", 5, "Concluido"),
        ("Withdrawn", 6, "Retirado"),
    ];

    public static string EstadoJuridico(int v) => De(EstadosJuridicos, v);

    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> ActuacionesJuridicas { get; } =
    [
        ("Filing", 1, "Radicación"), ("Order", 2, "Auto"), ("Seizure", 3, "Embargo o secuestro"), ("Hearing", 4, "Audiencia"), ("Judgment", 5, "Sentencia"),
        ("Fee", 6, "Honorarios"), ("Expense", 7, "Gasto"), ("StatusChange", 8, "Cambio de estado"), ("Other", 9, "Otra"),
    ];

    public static string ActuacionJuridica(int v) => De(ActuacionesJuridicas, v);

    // -------------------------------------------------------------------------------------------- campañas --

    public static string EstadoDeCampana(int v) => v switch
    {
        1 => "Borrador",
        2 => "Programada",
        3 => "En curso",
        4 => "Terminada",
        5 => "Cancelada",
        _ => Numero(v),
    };

    public static string MotivoDeExclusion(string motivo) => motivo switch
    {
        "ChannelNotAuthorized" => "No autorizó ese canal",
        "Deceased" => "El deudor falleció",
        "Insolvency" => "Está en insolvencia",
        "CollectionSuspended" => "La cobranza está suspendida",
        "WeeklyChannelUsed" => "Ya se le contactó por otro canal esta semana",
        _ => TextosDeCartera.RazonDeContacto(motivo),
    };

    // ---------------------------------------------------------------------------------------------- avisos --

    public static string EstadoDeAviso(int v) => v switch
    {
        1 => "Pendiente",
        2 => "En curso",
        3 => "Válido para reportar",
        4 => "Cerrado",
        _ => Numero(v),
    };

    public static IReadOnlyList<(int Valor, string Texto)> EstadosDeAviso { get; } = [(1, "Pendiente"), (2, "En curso"), (3, "Válido para reportar"), (4, "Cerrado")];

    public static string OrigenDelAviso(string origen) => origen switch { "Migration" => "Migrado", "Process" => "Del proceso", _ => origen };

    /// <summary>Los canales del aviso previo (<c>MessageChannel</c>); la carta se registra con su guía, no se envía.</summary>
    public static IReadOnlyList<(string Nombre, string Texto)> CanalesDeAviso { get; } = [("Email", "Correo"), ("Sms", "SMS"), ("WhatsApp", "WhatsApp")];

    /// <summary>Todos los canales de mensajes (<c>MessageChannel</c>).</summary>
    public static IReadOnlyList<(string Nombre, int Valor, string Texto)> CanalesDeMensaje { get; } =
        [("Email", 1, "Correo"), ("Sms", 2, "SMS"), ("WhatsApp", 3, "WhatsApp"), ("Letter", 4, "Carta")];

    public static string CanalDeMensaje(int v) => De(CanalesDeMensaje, v);

    public static string PropositoDeMensaje(int v) => v switch
    {
        1 => "Cobranza",
        2 => "Aviso previo",
        3 => "Informativo",
        4 => "Entrega de documento",
        5 => "Aviso de gasto de cobranza",
        _ => Numero(v),
    };
}
