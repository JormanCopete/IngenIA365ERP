using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;

namespace IngenIA365ERP.Application.Lending.Origination;

/// <summary>
/// Los errores de la decisión individual, de los cupos, de los bloqueos, de las listas restrictivas y de las consultas a centrales (feature 013,
/// US2 bloque 6B; api.md §4.3–§4.5, §7.1, §7.2; data-model §10.1). Los <c>*.NotFound</c> son 404 —también sin permiso o fuera del alcance—;
/// los demás, 422 con <c>data</c> para corregir. Los mensajes van en español claro, dicen qué hacer y nunca muestran el código (FR-013;
/// <c>TextosDeCarteraTests</c>). (nuevo)
/// </summary>
public static class ErroresDeDecisionesYCupos
{
    public const string CodigoNivelRequerido = "Lending.Attribution.LevelRequired";
    public const string CodigoNoExceptuable = NoExceptuables.CodigoDeError;
    public const string CodigoContenidoCambiado = "Lending.Decision.ContentChanged";
    public const string CodigoNoPendiente = "Lending.Decision.NotPending";
    public const string CodigoValidacionBloqueante = "Lending.Decision.CheckBlocking";
    public const string CodigoCupoInexistente = "Lending.CreditLimit.NotFound";
    public const string CodigoCupoSeCruza = "Lending.CreditLimit.Overlaps";
    public const string CodigoCupoNoActivo = "Lending.CreditLimit.NotActive";
    public const string CodigoCupoNoRotativo = "Lending.CreditLimit.NotRevolving";
    public const string CodigoCupoSinCalculo = "Lending.CreditLimit.CalculationUnavailable";
    public const string CodigoNoAsociados = Configuration.ErroresDeLineasDeCredito.CodigoNoAsociadosNoPermitidos;
    public const string CodigoListasBloquean = "Lending.Screening.Blocked";
    public const string CodigoBloqueoInexistente = "Lending.Block.NotFound";
    public const string CodigoBloqueoDuplicado = "Lending.Block.Duplicate";
    public const string CodigoBloqueoAutomatico = "Lending.Block.Automatic";
    public const string CodigoBloqueoYaLevantado = DebtorBlock.CodigoYaLevantado;
    public const string CodigoResultadoDeListasInexistente = "Lending.ScreeningResult.NotFound";
    public const string CodigoResultadoNoPendiente = ScreeningResult.CodigoNoPendiente;
    public const string CodigoConsultaACentralInexistente = "Lending.BureauQuery.NotFound";
    public const string CodigoCanalNoConfigurado = "Core.ExternalChannel.NotConfigured";

    // ------------------------------------------------------------------------------------------------------------ decisión --

    /// <summary>El nivel efectivo es de otro (colegiado o mayor que la atribución de quien decide, api.md §4.3).</summary>
    public static Error NivelRequerido(string nivel, string nombreDelNivel, string? organo) => new ErrorConDatos(CodigoNivelRequerido,
        organo is null
            ? $"Este asunto corresponde al nivel {nombreDelNivel}: su atribución no alcanza. Lo decide alguien con ese nivel."
            : $"Este asunto lo decide {organo} en sesión. Llévelo a una sesión del órgano para votarlo.",
        new { level = nivel, levelName = nombreDelNivel, governingBody = organo });

    public static Error NoExceptuable(IReadOnlyList<MotivoNoExceptuable> motivos) => new ErrorConDatos(CodigoNoExceptuable,
        $"Esto no admite excepción: {string.Join(", ", motivos.Select(Motivo))}. Ningún nivel puede aprobarlo.",
        new { reasons = motivos.Select(m => m.ToString()).ToList() });

    public static Error ContenidoCambiado(string actual) => new ErrorConDatos(CodigoContenidoCambiado,
        "El asunto cambió desde que lo revisó (endeudamiento, validaciones o puntaje): se volvió a enrutar. Revíselo de nuevo antes de decidir.",
        new { currentSha256 = actual });

    public static Error NoPendiente(string estado) => new ErrorConDatos(CodigoNoPendiente,
        $"Este asunto no espera decisión (estado: {estado}).", new { status = estado });

    /// <summary>Una validación bloqueante que ningún nivel exceptúa y no está en la lista de lo no exceptuable (bloqueos, descuento, canal caído).</summary>
    public static Error ValidacionBloqueante(ApplicationCheckKind clase, string? codigo) => new ErrorConDatos(codigo ?? CodigoValidacionBloqueante,
        $"La validación de {Clase(clase)} bloquea la aprobación y no se puede exceptuar. Corrija la causa o vuelva a correrla cuando se pueda.",
        new { kind = clase.ToString(), checkCode = codigo });

    public static Error CondicionesInvalidas(string campo, string mensaje) => new ErrorConDatos("Validation.Invalid", mensaje,
        new { errors = new[] { new ErrorDeCampo(campo, "Validation.Invalid", mensaje) } });

    // --------------------------------------------------------------------------------------------------------------- cupos --

    public static Error CupoInexistente() => new(CodigoCupoInexistente, "El cupo no existe.");

    public static Error CupoSeCruza(string motivo, DateOnly? desde) => new ErrorConDatos(CodigoCupoSeCruza,
        motivo == "Pending"
            ? "Este cupo ya tiene una versión esperando decisión. Espere a que se decida antes de registrar otra."
            : "El cupo ya tiene una versión que rige desde una fecha posterior: la nueva tiene que empezar después.",
        new { reason = motivo, validFrom = desde is { } d ? ErroresDeOriginacion.Fecha(d) : null });

    public static Error CupoNoActivo(string estado) => new ErrorConDatos(CodigoCupoNoActivo,
        $"El cupo no está activo (estado: {estado}).", new { status = estado });

    public static Error CupoNoRotativo(CreditLimitKind clase) => new ErrorConDatos(CodigoCupoNoRotativo,
        "La preferencia de abono a capital se registra en un cupo rotativo; la de un crédito se cambia en el crédito.", new { kind = clase.ToString() });

    public static Error CupoSinCalculo(string motivo, string mensaje) => new ErrorConDatos(CodigoCupoSinCalculo, mensaje, new { reason = motivo });

    public static Error NoAsociadosNoAdmitidos(Guid persona, string nombre) => new ErrorConDatos(CodigoNoAsociados,
        $"{nombre} no es asociado activo y ninguna línea aplicable admite no asociados (o el perfil regulatorio no lo permite). " +
        "A un no asociado sólo se le otorga cupo en una línea que lo admita.",
        new { personPublicId = persona });

    public static Error ListasBloquean(Guid resultado) => new ErrorConDatos(CodigoListasBloquean,
        "La persona coincide con una lista restrictiva: el cupo queda bloqueado hasta que el oficial de cumplimiento revise y libere la coincidencia.",
        new { screeningResultPublicId = resultado });

    // ----------------------------------------------------------------------------------------------------------- bloqueos --

    public static Error BloqueoInexistente() => new(CodigoBloqueoInexistente, "El bloqueo no existe.");

    public static Error BloqueoDuplicado(DebtorBlockReason clase, DateOnly desde, Guid bloqueo) => new ErrorConDatos(CodigoBloqueoDuplicado,
        $"La persona ya tiene un {Bloqueo(clase)} vigente desde el {ErroresDeOriginacion.Fecha(desde)}. Levántelo antes de registrar otro.",
        new { kind = clase.ToString(), since = ErroresDeOriginacion.Fecha(desde), blockPublicId = bloqueo });

    public static Error BloqueoAutomatico(DebtorBlockReason clase) => new ErrorConDatos(CodigoBloqueoAutomatico,
        $"Este bloqueo ({Bloqueo(clase)}) lo puso el sistema y se levanta solo: {ComoSeLevanta(clase)}",
        new { kind = clase.ToString(), howItLifts = ComoSeLevanta(clase) });

    // ------------------------------------------------------------------------------------------------- listas y centrales --

    public static Error ResultadoDeListasInexistente() => new(CodigoResultadoDeListasInexistente, "El resultado de la consulta de listas no existe.");

    public static Error ConsultaACentralInexistente() => new(CodigoConsultaACentralInexistente, "La consulta a centrales de riesgo no existe.");

    public static Error SinEvidencia(Guid persona, string nombre) => new ErrorConDatos(ErroresDeOriginacion.CodigoSinEvidencia,
        $"La autorización de {nombre} para la consulta a centrales de riesgo no tiene evidencia. Adjúntela para continuar.",
        new
        {
            personPublicId = persona,
            purpose = Domain.Enums.Compliance.DataAuthorizationPurpose.CreditBureauQuery.ToString(),
            howToFix = "Registre de nuevo la autorización con el documento firmado como evidencia.",
        });

    public static Error CanalNoDisponible(string canal, string? detalle = null) => new ErrorConDatos(ErroresDeOriginacion.CodigoCanalNoDisponible,
        $"La central de riesgo ({canal}) no responde. Intente más tarde.", new { channelCode = canal, retryAfter = (string?)null, detail = detalle });

    public static Error CanalNoConfigurado(string central) => new ErrorConDatos(CodigoCanalNoConfigurado,
        $"No hay un canal configurado para la central {central}. Configúrelo en Administración › Canales externos.",
        new { bureauCode = central });

    // ----------------------------------------------------------------------------------------------------------- textos --

    /// <summary>Cómo se levanta cada bloqueo automático (api.md §7.2, <c>data.howItLifts</c>).</summary>
    public static string ComoSeLevanta(DebtorBlockReason clase) => clase switch
    {
        DebtorBlockReason.Arrears => "el proceso diario lo levanta cuando el deudor se pone al día.",
        DebtorBlockReason.ValidationFailed => "se levanta al confirmar o anular la venta con la validación fallida.",
        DebtorBlockReason.WrittenOff => "se levanta cuando el crédito castigado se paga o se recupera.",
        DebtorBlockReason.RestrictiveListMatch => "se levanta cuando el oficial de cumplimiento libera la coincidencia en listas.",
        DebtorBlockReason.AssociateWithdrawal => "se levanta cuando la persona se reafilia en el maestro.",
        DebtorBlockReason.Deceased => "no se levanta: la persona falleció.",
        DebtorBlockReason.Insolvency => "se levanta cuando termina el proceso de insolvencia.",
        DebtorBlockReason.LimitRevalidation => "se levanta al revalidar el cupo cuando ya cumple (o con la decisión del consejo).",
        _ => "lo levanta a mano quien tenga el permiso, con motivo.",
    };

    public static string Bloqueo(DebtorBlockReason clase) => clase switch
    {
        DebtorBlockReason.Manual => "bloqueo manual",
        DebtorBlockReason.Garnishment => "embargo",
        DebtorBlockReason.Arrears => "bloqueo por mora",
        DebtorBlockReason.ValidationFailed => "bloqueo por validación fallida",
        DebtorBlockReason.WrittenOff => "bloqueo por castigo",
        DebtorBlockReason.RestrictiveListMatch => "bloqueo por coincidencia en listas",
        DebtorBlockReason.AssociateWithdrawal => "bloqueo por retiro del asociado",
        DebtorBlockReason.Deceased => "bloqueo por fallecimiento",
        DebtorBlockReason.Insolvency => "bloqueo por insolvencia",
        DebtorBlockReason.LimitRevalidation => "bloqueo de cupo por revalidar",
        _ => "bloqueo",
    };

    public static string Motivo(MotivoNoExceptuable m) => m switch
    {
        MotivoNoExceptuable.PaymentCapacity => "falta de capacidad de pago",
        MotivoNoExceptuable.HolderAuthorization => "falta de autorización del titular",
        MotivoNoExceptuable.RestrictiveListMatch => "coincidencia en listas restrictivas",
        MotivoNoExceptuable.Usury => "tasa sobre la usura",
        _ => m.ToString(),
    };

    public static string Clase(ApplicationCheckKind clase) => clase switch
    {
        ApplicationCheckKind.RestrictiveLists => "listas restrictivas",
        ApplicationCheckKind.CreditBureau => "centrales de riesgo",
        ApplicationCheckKind.PaymentCapacity => "capacidad de pago",
        ApplicationCheckKind.DeductionCapacity => "capacidad de descuento",
        ApplicationCheckKind.ConsolidatedExposure => "endeudamiento consolidado",
        ApplicationCheckKind.CreditLimit => "cupo",
        ApplicationCheckKind.Concentration => "concentración",
        ApplicationCheckKind.Usury => "usura",
        ApplicationCheckKind.LineRequirements => "requisitos de la línea",
        ApplicationCheckKind.DataAuthorization => "autorización de datos",
        ApplicationCheckKind.Scoring => "puntaje",
        ApplicationCheckKind.RelatedParty => "vinculados",
        ApplicationCheckKind.Documents => "documentos",
        _ => clase.ToString(),
    };
}
