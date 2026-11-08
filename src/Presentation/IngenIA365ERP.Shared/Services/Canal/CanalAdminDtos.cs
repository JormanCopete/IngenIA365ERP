namespace IngenIA365ERP.Shared.Services.Canal;

// Feature 021 (US1; T050–T053; contracts/erp-administracion.md): lo que las pantallas de administración del canal digital leen y
// mandan. Los enums SALEN de la API como número (se leen int) y ENTRAN por nombre (las peticiones los mandan como texto: «Manual»,
// «Loan»…; «JSON de la API» en CLAUDE.md). Las equivalencias número ↔ nombre ↔ etiqueta viven en EnumsDelCanal. (nuevo)

/// <summary>La configuración vigente del canal (o los valores por defecto si la cooperativa nunca lo configuró: <c>Version = 0</c>).</summary>
public sealed record ConfiguracionDelCanalDto(
    int Version,
    DateTime? EffectiveFrom,
    bool IsEnabled,
    string? ChannelCode,
    string DisplayName,
    Guid? LogoAttachmentPublicId,
    string? PrimaryColor,
    string? SecondaryColor,
    string? SupportPhone,
    string? SupportEmail,
    string? SupportHours,
    int PaymentSessionMinutes,
    bool RequireOtpToPay,
    int InactivityMinutes,
    int EnablementPolicy,
    bool AllowNonAssociates,
    bool SmsEnabled,
    int DueReminderDays,
    int SurplusPolicy,
    bool SatelliteConfigured);

/// <summary>Cambiar la configuración crea una versión nueva (FR-014). Las políticas viajan por nombre.</summary>
public sealed record ConfiguracionDelCanalRequest(
    string DisplayName,
    Guid? LogoAttachmentPublicId,
    string? PrimaryColor,
    string? SecondaryColor,
    string? SupportPhone,
    string? SupportEmail,
    string? SupportHours,
    int PaymentSessionMinutes,
    bool RequireOtpToPay,
    int InactivityMinutes,
    string EnablementPolicy,
    bool AllowNonAssociates,
    bool SmsEnabled,
    int DueReminderDays,
    string SurplusPolicy);

/// <summary>Una versión publicada de un texto legal; nunca se edita.</summary>
public sealed record TextoLegalDto(Guid PublicId, int Kind, int Version, string Content, DateTime PublishedAt, string? PublishedBy);

public sealed record TextoLegalRequest(string Kind, string Content);

/// <summary>Un destino de pago: un sitio de pasarela amarrado a un convenio de recaudo de clase pasarela.</summary>
public sealed record DestinoDePagoDto(
    Guid PublicId,
    string Name,
    int Gateway,
    Guid SitePublicId,
    Guid CollectionAgreementPublicId,
    string CollectionAgreementName,
    bool OfferPse,
    bool OfferCard,
    TimeOnly ReversalCutoffLocalTime,
    int CredentialsStatus,
    DateTime? CredentialsCheckedAt,
    string? LoginLast4,
    bool IsActive);

public sealed record DestinoDePagoRequest(
    string Name,
    Guid CollectionAgreementPublicId,
    bool OfferPse,
    bool OfferCard,
    TimeOnly ReversalCutoffLocalTime,
    bool IsActive);

/// <summary>Las credenciales del sitio: van al canal y el ERP no guarda el secreto (research R10).</summary>
public sealed record CredencialesDePasarelaRequest(string BaseUrl, string Login, string SecretKey)
{
    /// <summary>El secreto nunca se imprime (registros, depuración).</summary>
    public override string ToString() => $"CredencialesDePasarelaRequest {{ BaseUrl = {BaseUrl}, Login = {Login} }}";
}

/// <summary>Un concepto publicado en el canal (FR-002 a FR-004).</summary>
public sealed record ConceptoDelCanalDto(
    Guid PublicId,
    int Kind,
    string? LineCode,
    string? LineName,
    Guid? DestinationPublicId,
    string? DestinationName,
    bool AllowDue,
    bool AllowPartial,
    decimal? PartialMinimum,
    bool AllowPrepayment,
    bool AllowPayoff,
    bool CanCombine,
    int? DefaultPrepaymentChoice,
    bool PublishStatement,
    bool PublishCertificate,
    bool PublishTaxCertificate);

public sealed record ConceptoDelCanalRequest(
    string Kind,
    string? LineCode,
    Guid? DestinationPublicId,
    bool AllowDue,
    bool AllowPartial,
    decimal? PartialMinimum,
    bool AllowPrepayment,
    bool AllowPayoff,
    bool CanCombine,
    string? DefaultPrepaymentChoice,
    bool PublishStatement,
    bool PublishCertificate,
    bool PublishTaxCertificate);

/// <summary>La habilitación de una persona para el canal.</summary>
public sealed record HabilitacionDelCanalDto(
    Guid PublicId,
    Guid PersonPublicId,
    string FullName,
    string? DocumentType,
    string Document,
    int Status,
    DateTime EnabledAt,
    int Source,
    string? DisabledReason,
    DateTime? DisabledAt);

public sealed record PaginaDeHabilitacionesDto(IReadOnlyList<HabilitacionDelCanalDto> Items, int Total, int Page, int PageSize);

/// <summary>Una línea del catálogo común de Cartera (<c>GET /api/lending/lines</c>), lo que el selector de conceptos necesita.</summary>
public sealed record LineaParaElCanalDto(Guid PublicId, string Code, string Name, int Nature, int? DepositClass, int Status);

/// <summary>Un valor de enumeración del canal: el número con que sale de la API, el nombre con que entra y la etiqueta en pantalla.</summary>
public sealed record ValorDelCanal(int Numero, string Nombre, string Etiqueta);

/// <summary>Las enumeraciones del canal que las pantallas muestran o mandan (espejo de <c>Domain/Enums/Channel/ChannelEnums.cs</c>).</summary>
public static class EnumsDelCanal
{
    public static readonly IReadOnlyList<ValorDelCanal> PoliticasDeHabilitacion =
    [
        new(1, "AllActiveAssociates", "Todo asociado activo (se inhabilita al retirarse)"),
        new(2, "Manual", "Sólo las personas que habilite un funcionario"),
        new(3, "Both", "Las dos: todo asociado activo y las que habilite un funcionario"),
    ];

    public static readonly IReadOnlyList<ValorDelCanal> PoliticasDeExcedentes =
    [
        new(1, "AdvanceElseCreditBalance", "Anticipo a las próximas cuotas; si ya no hay deuda, saldo a favor"),
        new(2, "AlwaysCreditBalance", "Siempre saldo a favor del asociado"),
    ];

    public static readonly IReadOnlyList<ValorDelCanal> TiposDeTextoLegal =
    [
        new(1, "Terms", "Términos y condiciones"),
        new(2, "DataPolicy", "Política de tratamiento de datos"),
        new(3, "Faq", "Preguntas frecuentes"),
        new(4, "GatewayFaq", "Preguntas frecuentes de la pasarela"),
    ];

    /// <summary>Los tipos de concepto que se publican desde esta pantalla (los otros cobros llegan con su entrega).</summary>
    public static readonly IReadOnlyList<ValorDelCanal> TiposDeConcepto =
    [
        new(1, "Loan", "Crédito"),
        new(2, "Contribution", "Aportes"),
        new(3, "ContractualSaving", "Ahorro contractual"),
        new(4, "ProgrammedSaving", "Ahorro permanente o programado"),
    ];

    public static readonly IReadOnlyList<ValorDelCanal> OpcionesDePrepago =
    [
        new(1, "ReduceTerm", "Reducir el plazo"),
        new(2, "ReduceInstallment", "Reducir la cuota"),
    ];

    public static readonly IReadOnlyList<ValorDelCanal> EstadosDeHabilitacion =
    [
        new(1, "Enabled", "Habilitada"),
        new(2, "Disabled", "Inhabilitada"),
    ];

    public static readonly IReadOnlyList<ValorDelCanal> OrigenesDeHabilitacion =
    [
        new(1, "Policy", "Política del canal"),
        new(2, "Manual", "Un funcionario"),
        new(3, "Bulk", "Carga masiva"),
    ];

    public static readonly IReadOnlyList<ValorDelCanal> EstadosDeCredenciales =
    [
        new(0, "NotSet", "Sin credenciales"),
        new(1, "Active", "Verificadas y activas"),
        new(2, "Failed", "La verificación falló"),
        new(3, "PendingVerification", "Pendientes de verificar"),
    ];

    public const int EstadoHabilitada = 1;
    public const int ConceptoCredito = 1;
    public const int CredencialesActivas = 1;
    public const int CredencialesFallidas = 2;

    /// <summary>Clase «pasarela» de un convenio de recaudo (<c>CollectionAgreementKind.Gateway</c>).</summary>
    public const int ConvenioDePasarela = 3;

    /// <summary>La etiqueta en pantalla de un número; el número mismo si la API trae uno que esta versión no conoce.</summary>
    public static string Etiqueta(IReadOnlyList<ValorDelCanal> valores, int? numero) =>
        numero is null ? "—" : valores.FirstOrDefault(v => v.Numero == numero)?.Etiqueta ?? numero.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>El nombre con que la petición manda un número; el primero de la lista si no se conoce.</summary>
    public static string Nombre(IReadOnlyList<ValorDelCanal> valores, int numero) =>
        valores.FirstOrDefault(v => v.Numero == numero)?.Nombre ?? valores[0].Nombre;

    /// <summary>
    /// La naturaleza de línea (<c>LineNature</c>) y la clase de captación (<c>DepositClass</c>) que corresponden a cada tipo de concepto,
    /// por nombre para la consulta del catálogo de líneas.
    /// </summary>
    public static (string Naturaleza, string? ClaseDeCaptacion) LineasDe(int tipoDeConcepto) => tipoDeConcepto switch
    {
        1 => ("Placement", null),
        2 => ("Contribution", null),
        3 => ("Deposit", "Contractual"),
        _ => ("Deposit", "Permanent"),
    };
}
