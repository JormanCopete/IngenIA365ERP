namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>
/// Los textos de pantalla y los nombres que viajan de los enums de la plataforma de Cartera (feature 013, fase 2, T0094, T0095). La API
/// devuelve los enums como número y los recibe por nombre: aquí está la traducción en los dos sentidos, para que ninguna pantalla muestre un
/// número ni un código técnico. (nuevo)
/// </summary>
public static class TextosDePlataforma
{
    // ------------------------------------------------------------------------------------------- valores nacionales --

    public static string EstadoDeRevision(int estado) => estado switch
    {
        1 => "Validado",
        2 => "Objetado",
        _ => "Pendiente de validar",
    };

    public static string DecisionDeRevision(int decision) => decision switch
    {
        1 => "Conforme",
        2 => "Objetado",
        _ => "—",
    };

    /// <summary>Los nombres de <c>NationalValueReviewDecision</c>.</summary>
    public const string Conforme = "Confirmed";
    public const string Objetado = "Objected";

    public static string OrigenDeReplica(int origen) => origen switch
    {
        1 => "Tarea programada",
        2 => "Alta de la cooperativa",
        3 => "Ciclo diario",
        4 => "Cierre mensual",
        _ => "—",
    };

    /// <summary>Lo que significa cada código de valor nacional (DT §2.21), para no mostrar el código solo.</summary>
    public static string NombreDeValor(string codigo) => codigo switch
    {
        "SMMLV" => "Salario mínimo mensual",
        "UVT" => "Unidad de valor tributario",
        "USURA" => "Tasa de usura",
        "IBR" => "Indicador bancario de referencia",
        "IPC" => "Índice de precios al consumidor",
        "FESTIVO" => "Festivo nacional",
        "LIMITE" => "Límite legal",
        "UVR" => "Unidad de valor real",
        "TOPE_TASA_VIVIENDA" => "Tope de tasa de vivienda",
        // Feature 014 (T016).
        "COMPONENTE_INFLACIONARIO" => "Componente inflacionario de los rendimientos",
        "LIMITE_INEMBARGABLE" => "Límite inembargable de los depósitos",
        "TOPE_ENTREGA_SIN_SUCESION" => "Tope de entrega sin juicio de sucesión",
        "TOPE_SEGURO_DEPOSITOS" => "Tope del seguro de depósitos",
        _ => codigo,
    };

    public static string NombreDeDocumento(string clase) => clase switch
    {
        "CatalogoDeModalidades" => "Catálogo de modalidades de crédito",
        "TiposDeOrganizacion" => "Tipos de organización solidaria",
        "SarcAnexo1" => "Anexo 1 del SARC",
        "SarcAnexo2" => "Anexo 2 del SARC",
        "ReglaDePerdidaEsperada" => "Regla de pérdida esperada",
        _ => clase,
    };

    public static string Unidad(string? unidad) => unidad switch
    {
        null or "" => "",
        "EA" => "efectivo anual",
        "NominalMV" => "nominal mes vencido",
        "Pesos" => "pesos",
        "Fraccion" => "fracción",
        "Dias" => "días",
        "Meses" => "meses",
        "Horas" => "horas",
        "Veces" => "veces",
        _ => unidad,
    };

    /// <summary>
    /// El valor como se lee: las tasas (EA, nominal) se guardan como fracción y se muestran en porcentaje; los pesos sin decimales;
    /// lo demás con los decimales que traiga.
    /// </summary>
    public static string Valor(decimal? valor, string? unidad) => valor switch
    {
        null => "—",
        { } v when unidad is "EA" or "NominalMV" => (v * 100m).ToString("N4") + " %",
        { } v when unidad is "Pesos" => v.ToString("N0"),
        { } v => v.ToString("0.##########"),
    };

    public static readonly IReadOnlyList<string> Unidades = ["EA", "NominalMV", "Pesos", "Fraccion", "Dias", "Meses", "Horas", "Veces"];

    public static readonly IReadOnlyList<string> CodigosDeValor = ["SMMLV", "UVT", "USURA", "IBR", "IPC", "FESTIVO", "LIMITE"];

    // ------------------------------------------------------------------------------------------- canales externos --

    public static readonly IReadOnlyList<(int Valor, string Nombre, string Texto)> ClasesDeCanal =
    [
        (1, "Sms", "Mensajes de texto (SMS)"),
        (2, "WhatsApp", "WhatsApp"),
        (3, "SecuritiesDepository", "Depósito de valores"),
        (4, "CreditBureau", "Central de riesgo"),
        (5, "RestrictiveLists", "Listas restrictivas"),
    ];

    public static string ClaseDeCanal(int clase) => ClasesDeCanal.FirstOrDefault(c => c.Valor == clase).Texto ?? "—";

    public static string NombreDeClaseDeCanal(int clase) => ClasesDeCanal.First(c => c.Valor == clase).Nombre;

    public static string ResultadoExterno(int? resultado) => resultado switch
    {
        1 => "Respondió bien",
        2 => "Rechazado",
        3 => "En proceso",
        4 => "No encontrado",
        5 => "Canal no disponible",
        6 => "Datos inválidos",
        _ => "Sin verificar",
    };

    public static string Circuito(int circuito) => circuito == 2 ? "Suspendido por fallas" : "Disponible";

    /// <summary>El canal que se ofrece sin credencial (no tiene validez legal) y el de la carga propia de listas.</summary>
    public const string CanalSimulado = "SIMULADO";
    public const string CanalDeCargaPropia = "CARGA-PROPIA";

    // ------------------------------------------------------------------------------------ registro de participantes --

    public static readonly IReadOnlyList<(int Valor, string Nombre, string Texto)> ClasesDeParticipante =
    [
        (1, "Receipts", "Recaudo"),
        (2, "Payroll", "Nómina"),
        (3, "Contributions", "Aportes"),
        (4, "Treasury", "Tesorería"),
        (5, "Test", "Doble de prueba"),
        (6, "Accounting", "Contabilidad"),
        (7, "Lending", "Cartera"),
        (8, "Inventory", "Comercial"),
    ];

    public static string ClaseDeParticipante(int clase) => ClasesDeParticipante.FirstOrDefault(c => c.Valor == clase).Texto ?? "—";

    public static string NombreDeClaseDeParticipante(int clase) => ClasesDeParticipante.First(c => c.Valor == clase).Nombre;

    public static readonly IReadOnlyList<(int Valor, string Nombre, string Texto)> RolesDeParticipante =
    [
        (1, "ObligationsCollector", "Recaudador de obligaciones"),
        (2, "MessageDestination", "Destino de mensajes"),
    ];

    public static string RolDeParticipante(int rol) => RolesDeParticipante.FirstOrDefault(r => r.Valor == rol).Texto ?? "—";

    public static string NombreDeRolDeParticipante(int rol) => RolesDeParticipante.First(r => r.Valor == rol).Nombre;

    public static readonly IReadOnlyList<(string Valor, string Texto)> Destinos =
    [
        ("Accounting", "Contabilidad"),
        ("Lending", "Cartera"),
        ("Inventory", "Comercial"),
        ("Treasury", "Tesorería"),
        ("Receipts", "Recaudo"),
    ];

    public static string Destino(string? destino) =>
        string.IsNullOrEmpty(destino) ? "—" : Destinos.FirstOrDefault(d => d.Valor == destino).Texto ?? destino;

    // ------------------------------------------------------------------------------------------------- cumplimiento --

    public static readonly IReadOnlyList<(int Valor, string Nombre, string Texto)> ClasesDeSolicitud =
    [
        (1, "Query", "Consulta"),
        (2, "Update", "Actualización"),
        (3, "Rectification", "Rectificación"),
        (4, "Suppression", "Supresión"),
        (5, "Revocation", "Revocación de la autorización"),
    ];

    public static string ClaseDeSolicitud(int clase) => ClasesDeSolicitud.FirstOrDefault(c => c.Valor == clase).Texto ?? "—";

    public static string NombreDeClaseDeSolicitud(int clase) => ClasesDeSolicitud.First(c => c.Valor == clase).Nombre;

    public static string EstadoDeSolicitud(int estado) => estado switch
    {
        1 => "Recibida",
        2 => "Prorrogada",
        3 => "Respondida",
        4 => "Retirada",
        _ => "—",
    };

    public static readonly IReadOnlyList<(int Valor, string Nombre, string Texto)> DesenlacesDeSolicitud =
    [
        (1, "Granted", "Procede"),
        (2, "PartiallyGranted", "Procede en parte"),
        (3, "Denied", "No procede"),
    ];

    public static string DesenlaceDeSolicitud(int? desenlace) =>
        desenlace is { } d ? DesenlacesDeSolicitud.FirstOrDefault(x => x.Valor == d).Texto ?? "—" : "—";

    public static string NombreDeDesenlace(int desenlace) => DesenlacesDeSolicitud.First(d => d.Valor == desenlace).Nombre;

    public static readonly IReadOnlyList<(int Valor, string Nombre, string Texto)> Finalidades =
    [
        (1, "GeneralTreatment", "Tratamiento general"),
        (2, "CreditProcessing", "Trámite de crédito"),
        (3, "CreditBureauQuery", "Consulta a centrales de riesgo"),
        (4, "CreditBureauReport", "Reporte a centrales de riesgo"),
        (5, "ContactChannel", "Canal de contacto"),
    ];

    public static string Finalidad(int? finalidad) => finalidad is { } f ? Finalidades.FirstOrDefault(x => x.Valor == f).Texto ?? "—" : "—";

    public static string NombreDeFinalidad(int finalidad) => Finalidades.First(f => f.Valor == finalidad).Nombre;

    public static readonly IReadOnlyList<(int Valor, string Nombre, string Texto)> CanalesDeContacto =
    [
        (1, "Sms", "SMS"),
        (2, "WhatsApp", "WhatsApp"),
        (3, "Email", "Correo"),
        (4, "Call", "Llamada"),
        (5, "Visit", "Visita"),
    ];

    public static string CanalDeContacto(int? canal) => canal is { } c ? CanalesDeContacto.FirstOrDefault(x => x.Valor == c).Texto ?? "—" : "—";

    public static string NombreDeCanalDeContacto(int canal) => CanalesDeContacto.First(c => c.Valor == canal).Nombre;

    /// <summary>Por dónde llegó la solicitud (los valores que admite la API, que se guardan tal cual).</summary>
    public static readonly IReadOnlyList<(string Valor, string Texto)> MediosDeRecibo =
    [
        ("Presencial", "Presencial"),
        ("Correo", "Correo"),
        ("Escrito", "Escrito"),
        ("Telefono", "Teléfono"),
    ];

    public static string EstadoDeCarga(string estado) => estado switch
    {
        "Active" => "Vigente",
        "Replaced" => "Reemplazada",
        "Scheduled" => "Programada",
        _ => estado,
    };

    public static string ClaseDeEntrada(int clase) => clase switch
    {
        1 => "Persona",
        2 => "Organización",
        _ => "Otra",
    };
}
