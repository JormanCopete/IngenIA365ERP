namespace IngenIA365ERP.Shared.Services.FacturacionElectronica;

/// <summary>
/// Las etiquetas en español y los nombres de los enums de facturación electrónica (feature 012, I4, T752; decisiones-transversales §2.5:
/// «valores en inglés; la pantalla traduce»). La API los entrega como número y los recibe por nombre o número: las pantallas los muestran
/// con la etiqueta y los mandan con el nombre. Los números y los nombres son los de <c>Domain.Enums.ElectronicInvoicing</c>,
/// <c>Domain.Enums.Dian.DianEnvironment</c>, <c>ActorKind</c> y <c>VeredictoFiscal</c>; lo fija <c>TextosDeFacturacionElectronicaTests</c>. (nuevo)
/// </summary>
public static class TextosDeFacturacionElectronica
{
    // ------------------------------------------------------------------------------------ constantes --

    public const int VeredictoElectronico = 1;
    public const int VeredictoNoElectronico = 2;
    public const int VeredictoBloqueado = 3;

    public const int EstadoPendiente = 0;
    public const int EstadoEnviado = 1;
    public const int EstadoValidado = 2;
    public const int EstadoValidadoConNotificaciones = 3;
    public const int EstadoRechazado = 4;
    public const int EstadoContingenciaDelFacturador = 5;
    public const int EstadoContingenciaDeLaDian = 6;
    public const int EstadoAnuladoSinReemplazo = 7;

    public const int ContingenciaDelFacturador = 3;
    public const int ContingenciaDeLaDian = 4;

    public const int ResolucionDeContingencia = 4;

    /// <summary>Los artefactos de una versión (texto, no enum guardado; api.md §24.4 <c>artifact</c>).</summary>
    public static IReadOnlyDictionary<string, string> Artefactos { get; } = new Dictionary<string, string>
    {
        ["Canonical"] = "Documento canónico (JSON)",
        ["SignedXml"] = "XML firmado",
        ["AttachedDocument"] = "AttachedDocument",
        ["ApplicationResponse"] = "Respuesta de la DIAN",
        ["GraphicRepresentation"] = "Representación gráfica (PDF)",
    };

    /// <summary>El estado calculado de una resolución (texto, api.md §24.2).</summary>
    public static IReadOnlyDictionary<string, string> EstadosDeResolucion { get; } = new Dictionary<string, string>
    {
        ["Active"] = "Vigente",
        ["NotYetValid"] = "Todavía no vigente",
        ["Expired"] = "Vencida",
        ["Exhausted"] = "Agotada",
    };

    // ----------------------------------------------------------------------------------- configuración --

    /// <summary><c>EmissionMode</c>.</summary>
    public static IReadOnlyDictionary<int, string> Modos { get; } = new Dictionary<int, string>
    {
        [1] = "Proveedor tecnológico",
        [2] = "Software propio",
    };

    public static IReadOnlyDictionary<int, string> NombresDeModo { get; } = new Dictionary<int, string>
    {
        [1] = "TechnologyProvider",
        [2] = "OwnSoftware",
    };

    /// <summary><c>DianEnvironment</c>.</summary>
    public static IReadOnlyDictionary<int, string> Ambientes { get; } = new Dictionary<int, string>
    {
        [1] = "Producción",
        [2] = "Habilitación (pruebas)",
    };

    public static IReadOnlyDictionary<int, string> NombresDeAmbiente { get; } = new Dictionary<int, string>
    {
        [1] = "Production",
        [2] = "Testing",
    };

    /// <summary><c>EmailDeliveryBy</c>.</summary>
    public static IReadOnlyDictionary<int, string> EntregasDeCorreo { get; } = new Dictionary<int, string>
    {
        [1] = "Lo envía el ERP",
        [2] = "Lo envía el canal",
    };

    public static IReadOnlyDictionary<int, string> NombresDeEntregaDeCorreo { get; } = new Dictionary<int, string>
    {
        [1] = "Erp",
        [2] = "Channel",
    };

    /// <summary><c>VeredictoFiscal</c> de la preparación.</summary>
    public static IReadOnlyDictionary<int, string> Veredictos { get; } = new Dictionary<int, string>
    {
        [1] = "Lista para emitir electrónicamente",
        [2] = "No obligada: comprobante no electrónico",
        [3] = "Bloqueada: falta completar",
    };

    // ------------------------------------------------------------------------------------- documentos --

    /// <summary><c>ElectronicDocumentKind</c>.</summary>
    public static IReadOnlyDictionary<int, string> TiposDeDocumento { get; } = new Dictionary<int, string>
    {
        [1] = "Factura electrónica",
        [2] = "Nota crédito",
        [3] = "Nota débito",
        [4] = "Documento equivalente POS",
        [5] = "Nota de ajuste del documento equivalente",
        [6] = "Documento soporte",
        [7] = "Nota de ajuste del documento soporte",
        [8] = "Evento RADIAN 030",
        [9] = "Evento RADIAN 032",
    };

    public static IReadOnlyDictionary<int, string> NombresDeTipoDeDocumento { get; } = new Dictionary<int, string>
    {
        [1] = "Invoice",
        [2] = "CreditNote",
        [3] = "DebitNote",
        [4] = "PosEquivalent",
        [5] = "PosAdjustmentNote",
        [6] = "SupportDocument",
        [7] = "SupportDocumentAdjustmentNote",
        [8] = "RadianEvent030",
        [9] = "RadianEvent032",
    };

    /// <summary><c>ElectronicDocumentStatus</c>.</summary>
    public static IReadOnlyDictionary<int, string> Estados { get; } = new Dictionary<int, string>
    {
        [0] = "Pendiente de transmitir",
        [1] = "Enviado, sin respuesta",
        [2] = "Validado",
        [3] = "Validado con notificaciones",
        [4] = "Rechazado",
        [5] = "En contingencia del facturador (03)",
        [6] = "En contingencia de la DIAN (04)",
        [7] = "Anulado sin reemplazo",
    };

    public static IReadOnlyDictionary<int, string> NombresDeEstado { get; } = new Dictionary<int, string>
    {
        [0] = "Pending",
        [1] = "Sent",
        [2] = "Validated",
        [3] = "ValidatedWithNotices",
        [4] = "Rejected",
        [5] = "IssuerContingency",
        [6] = "DianContingency",
        [7] = "CancelledWithoutReplacement",
    };

    /// <summary><c>ContingencyType</c>.</summary>
    public static IReadOnlyDictionary<int, string> TiposDeContingencia { get; } = new Dictionary<int, string>
    {
        [3] = "03 · falla del facturador",
        [4] = "04 · falla de la DIAN",
    };

    public static IReadOnlyDictionary<int, string> NombresDeTipoDeContingencia { get; } = new Dictionary<int, string>
    {
        [3] = "Issuer03",
        [4] = "Dian04",
    };

    /// <summary><c>ResolutionKind</c>.</summary>
    public static IReadOnlyDictionary<int, string> TiposDeResolucion { get; } = new Dictionary<int, string>
    {
        [1] = "Factura electrónica",
        [2] = "Documento equivalente POS",
        [3] = "Documento soporte",
        [4] = "Contingencia",
    };

    public static IReadOnlyDictionary<int, string> NombresDeTipoDeResolucion { get; } = new Dictionary<int, string>
    {
        [1] = "Invoice",
        [2] = "PosEquivalent",
        [3] = "SupportDocument",
        [4] = "Contingency",
    };

    /// <summary><c>ChannelOutcome</c>.</summary>
    public static IReadOnlyDictionary<int, string> Resultados { get; } = new Dictionary<int, string>
    {
        [1] = "Validado",
        [2] = "Validado con notificaciones",
        [3] = "Rechazado",
        [4] = "En proceso",
        [5] = "No encontrado",
        [6] = "DIAN no disponible",
        [7] = "Canal no disponible",
        [8] = "Datos inválidos",
    };

    /// <summary><c>TransmissionOperation</c>.</summary>
    public static IReadOnlyDictionary<int, string> Operaciones { get; } = new Dictionary<int, string>
    {
        [1] = "Emisión",
        [2] = "Transmisión desde contingencia",
        [3] = "Consulta de estado",
        [4] = "Evento",
        [5] = "Descarga",
    };

    /// <summary><c>DocumentVersionReason</c>.</summary>
    public static IReadOnlyDictionary<int, string> MotivosDeVersion { get; } = new Dictionary<int, string>
    {
        [1] = "Inicial",
        [2] = "Corrección sin cambio económico (caso a)",
        [3] = "Reemplazo con el mismo número (caso b)",
        [4] = "Referencia completada",
    };

    /// <summary><c>RejectedBy</c>.</summary>
    public static IReadOnlyDictionary<int, string> Rechazadores { get; } = new Dictionary<int, string>
    {
        [1] = "la DIAN",
        [2] = "el canal",
    };

    /// <summary><c>UniqueCodeKind</c>.</summary>
    public static IReadOnlyDictionary<int, string> CodigosUnicos { get; } = new Dictionary<int, string>
    {
        [1] = "CUFE",
        [2] = "CUDE",
        [3] = "CUDS",
    };

    /// <summary><c>ActorKind</c>: quién pidió o detectó.</summary>
    public static IReadOnlyDictionary<int, string> Actores { get; } = new Dictionary<int, string>
    {
        [1] = "Persona",
        [2] = "Proceso",
    };

    // ------------------------------------------------------------------------------------- lectores --

    public static string Modo(int valor) => Texto(Modos, valor);
    public static string Ambiente(int valor) => Texto(Ambientes, valor);
    public static string EntregaDeCorreo(int valor) => Texto(EntregasDeCorreo, valor);
    public static string Veredicto(int valor) => Texto(Veredictos, valor);
    public static string TipoDeDocumento(int valor) => Texto(TiposDeDocumento, valor);
    public static string Estado(int valor) => Texto(Estados, valor);
    public static string TipoDeContingencia(int? valor) => valor is { } v ? Texto(TiposDeContingencia, v) : "";
    public static string TipoDeResolucion(int? valor) => valor is { } v ? Texto(TiposDeResolucion, v) : "";
    public static string Resultado(int? valor) => valor is { } v ? Texto(Resultados, v) : "";
    public static string Operacion(int valor) => Texto(Operaciones, valor);
    public static string MotivoDeVersion(int valor) => Texto(MotivosDeVersion, valor);
    public static string Rechazador(int? valor) => valor is { } v ? Texto(Rechazadores, v) : "";
    public static string CodigoUnico(int? valor) => valor is { } v ? Texto(CodigosUnicos, v) : "Código único";
    public static string Actor(int valor) => Texto(Actores, valor);

    /// <summary>El número de un estado que llega por nombre (<c>electronicStatus</c> de la lista de ventas), o nulo.</summary>
    public static int? EstadoPorNombre(string? nombre) =>
        NombresDeEstado.FirstOrDefault(p => string.Equals(p.Value, nombre, StringComparison.OrdinalIgnoreCase)) is { Value: not null } par ? par.Key : null;

    public static string Artefacto(string artefacto) => Artefactos.TryGetValue(artefacto, out var t) ? t : artefacto;

    public static string EstadoDeResolucion(string? estado) => estado is not null && EstadosDeResolucion.TryGetValue(estado, out var t) ? t : estado ?? "";

    /// <summary>El nombre del enum para mandar (nulo si el número no es uno conocido).</summary>
    public static string? Nombre(IReadOnlyDictionary<int, string> nombres, int? valor) =>
        valor is { } v && nombres.TryGetValue(v, out var n) ? n : null;

    /// <summary>La clase de estilo de la pastilla del estado.</summary>
    public static string ClaseDeEstado(int estado) => estado switch
    {
        EstadoValidado or EstadoValidadoConNotificaciones => "aviso-ok",
        EstadoRechazado => "aviso-error",
        EstadoContingenciaDelFacturador or EstadoContingenciaDeLaDian or EstadoEnviado => "aviso-alerta",
        _ => "aviso-info",
    };

    private static string Texto(IReadOnlyDictionary<int, string> textos, int valor) => textos.TryGetValue(valor, out var t) ? t : $"({valor})";
}
