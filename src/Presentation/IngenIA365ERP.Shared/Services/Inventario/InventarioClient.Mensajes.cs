using System.Text.Json;
using IngenIA365ERP.Shared.Services.Contabilidad;
using IngenIA365ERP.Shared.Services.Http;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// La parte de mensajes de Inventario (feature 012, US7, T532; contracts/api.md §25): la bandeja con sus contadores y la vista
/// previa del envío posterior (<c>closure=true</c>), el detalle de un mensaje con contenido e intentos, el reproceso de rechazados,
/// el envío posterior de «no aplica» y la validación previa de un borrador (<c>documents/{id}/prevalidate</c>). La vista previa y
/// la activación de una bodega (<c>warehouses/{id}/activation</c>) viven en <c>.PuestaEnMarcha</c>, que nació en US4; US7 les
/// suma los conjuntos de cuentas. Reprocesar y enviar después llevan la <see cref="ClaveDeOperacion"/> de la operación y la
/// conservan en el reintento; las consultas no. Los filtros de enum viajan por nombre. (nuevo)
/// </summary>
public sealed partial class InventarioClient
{
    public const string RutaDeMensajes = Base + "/messages";

    /// <summary>Los dos códigos con que la confirmación devuelve lo que dijo Contabilidad (§25.4).</summary>
    public const string ValidacionPreviaNoContabilizable = "Inventory.Prevalidation.NotPostable";
    public const string ValidacionPreviaSinRespuesta = "Inventory.Prevalidation.NoResponse";

    public Task<ResultadoDeInventario<BandejaDeMensajesDto>> MensajesAsync(FiltroDeMensajes f, CancellationToken ct = default) =>
        EnviarAsync<BandejaDeMensajesDto>(HttpMethod.Get, ConQuery(RutaDeMensajes, Query(
            ("status", TextosDeInventario.Nombre(TextosDeInventario.NombresDeEstadoDeEntrega, f.Estado)),
            ("destination", f.Destino), ("type", f.Tipo), ("document", f.Documento?.ToString()), ("documentNumber", f.NumeroDeDocumento),
            ("documentType", f.TipoDeDocumento), ("batch", f.Lote?.ToString()),
            ("from", f.Desde?.ToString("yyyy-MM-dd")), ("to", f.Hasta?.ToString("yyyy-MM-dd")), ("closure", f.Clausura ? "true" : null),
            ("prevalidationOutcome", TextosDeInventario.Nombre(TextosDeInventario.NombresDeValidacionPrevia, f.ValidacionPrevia)),
            ("page", f.Pagina.ToString()), ("pageSize", f.TamanoDePagina.ToString()))), null, null, ct);

    /// <summary>El mensaje con su contenido (el JSON inmutable del contrato), sus intentos y sus dependencias.</summary>
    public Task<ResultadoDeInventario<DetalleDeMensajeDto>> MensajeAsync(Guid id, string? destino = null, CancellationToken ct = default) =>
        EnviarAsync<DetalleDeMensajeDto>(HttpMethod.Get, ConQuery($"{RutaDeMensajes}/{id}", Query(("destination", destino))), null, null, ct);

    /// <summary>Reprocesa rechazados (202): un lote <c>Reprocess</c> que arrastra a los dependientes que esperaban.</summary>
    public Task<ResultadoDeInventario<LoteDeMensajesDto>> ReprocesarMensajesAsync(IReadOnlyList<Guid> mensajes, string motivo, ClaveDeOperacion clave,
        string? destino = null, CancellationToken ct = default) =>
        EnviarAsync<LoteDeMensajesDto>(HttpMethod.Post, $"{RutaDeMensajes}/reprocess", new ReprocesoDeMensajesRequest(mensajes, motivo, destino), clave, ct);

    /// <summary>Envía después lo que no aplicaba (202), exactamente hasta el corte de la vista previa (FR-078).</summary>
    public Task<ResultadoDeInventario<LoteDeMensajesDto>> EnviarNoAplicaAsync(EnvioPosteriorRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<LoteDeMensajesDto>(HttpMethod.Post, $"{RutaDeMensajes}/send-not-applicable", request, clave, ct);

    /// <summary>«¿Contabilidad podría contabilizar este borrador tal como está?» Una consulta: no guarda nada ni lleva clave.</summary>
    public Task<ResultadoDeInventario<ValidacionPreviaDto>> ValidarConContabilidadAsync(Guid documento, CancellationToken ct = default) =>
        EnviarAsync<ValidacionPreviaDto>(HttpMethod.Post, $"{RutasDeGrupo.Documentos}/{documento}/prevalidate", new { }, null, ct);
}

// --------------------------------------------------------------------------------------------------- DTO --

/// <summary>Los filtros de la bandeja (§25.2); <c>Estado</c> y <c>ValidacionPrevia</c> son los números de sus enums.</summary>
public sealed record FiltroDeMensajes(
    int? Estado = null,
    string? Destino = null,
    string? Tipo = null,
    Guid? Documento = null,
    string? NumeroDeDocumento = null,
    string? TipoDeDocumento = null,
    Guid? Lote = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    bool Clausura = false,
    int? ValidacionPrevia = null,
    int Pagina = 1,
    int TamanoDePagina = 50);

public sealed record ConteoPorEstadoDto(int Pending, int InBatch, int Processed, int Rejected, int NotApplicable, int ValidationFailed)
{
    /// <summary>El conteo de un <c>DeliveryStatus</c>.</summary>
    public int De(int estado) => estado switch
    {
        0 => Pending, 1 => InBatch, 2 => Processed, 3 => Rejected, 4 => NotApplicable, 5 => ValidationFailed, _ => 0,
    };
}

/// <summary>La bandeja: la página, los contadores por estado (sin el filtro de estado) y, con <c>closure=true</c>, el corte.</summary>
public sealed record BandejaDeMensajesDto(PaginaDeInventarioDto<MensajeDeIntegracionDto> Messages, ConteoPorEstadoDto CountsByStatus, Guid? CutoffMessagePublicId);

public sealed record LoteDelMensajeDto(Guid BatchPublicId, long Number);

public sealed record ErrorDelMensajeDto(string? Code, string? Message, string? DataJson);

/// <summary><c>WithoutVoucher</c>: 1 informativo, 2 valor cero. <c>Route</c> lleva al comprobante o al lote resumido.</summary>
public sealed record ResultadoDelMensajeDto(string? VoucherTypeCode, string? Number, Guid? AccountingDocumentPublicId, string? Route, int? WithoutVoucher);

/// <summary><c>Kind</c>: 1 documento, 2 operación (cierre, reapertura, reclasificación).</summary>
public sealed record OrigenDelMensajeDto(int Kind, string? DocumentClass, string? DocumentTypeCode, string? Number, Guid PublicId, DateOnly OperationDate);

public sealed record RelacionadoDelMensajeDto(Guid PublicId, string? DocumentClass, string? Number);

public sealed record BloqueoDelMensajeDto(Guid MessagePublicId, string Type, int DeliveryStatus);

/// <summary><c>IntegrationMessageDto</c> (§25.2). <c>DeliveryStatus</c>, <c>Mode</c>, <c>Kind</c> y <c>PrevalidationOutcome</c> llegan como número.</summary>
public sealed record MensajeDeIntegracionDto(
    Guid MessagePublicId,
    DateTime EmittedAt,
    string Type,
    int Version,
    int Kind,
    string Destination,
    int DeliveryStatus,
    int Mode,
    string? ScheduleKey,
    LoteDelMensajeDto? Batch,
    int Attempts,
    DateTime? NextAttemptAt,
    ErrorDelMensajeDto? LastError,
    DateTime? ProcessedAt,
    ResultadoDelMensajeDto? Result,
    OrigenDelMensajeDto Origin,
    RelacionadoDelMensajeDto? Related,
    string OriginUserName,
    int? PrevalidationOutcome,
    IReadOnlyList<BloqueoDelMensajeDto> BlockedBy,
    bool DestinationAvailable,
    bool? PendingValidation);

/// <summary><c>Kind</c> del actor: 1 persona, 2 proceso. <c>Outcome</c>: <c>DeliveryAttemptOutcome</c>.</summary>
public sealed record ActorDelIntentoDto(int Kind, string Name);

public sealed record IntentoDelMensajeDto(DateTime StartedAt, DateTime FinishedAt, int Outcome, string? Code, string? Message, ActorDelIntentoDto Actor,
    long? BatchNumber, string Instance);

public sealed record VecinoDelMensajeDto(Guid MessagePublicId, string Type, int? Status);

public sealed record DetalleDeMensajeDto(
    MensajeDeIntegracionDto Message,
    string Payload,
    string PayloadSha256,
    IReadOnlyList<IntentoDelMensajeDto> Attempts,
    IReadOnlyList<VecinoDelMensajeDto> DependsOn,
    IReadOnlyList<VecinoDelMensajeDto> Dependents);

public sealed record ReprocesoDeMensajesRequest(IReadOnlyList<Guid> MessagePublicIds, string Reason, string? Destination);

public sealed record EnvioPosteriorRequest(DateOnly From, DateOnly To, IReadOnlyList<Guid>? DocumentTypePublicIds, Guid CutoffMessagePublicId, string Reason);

/// <summary>El 202 del reproceso (<c>Dragged</c>) y del envío posterior (<c>Documents</c>): el lote que corre en segundo plano.</summary>
public sealed record LoteDeMensajesDto(Guid BatchPublicId, long Number, int Trigger, int Messages, int? Dragged, int? Documents);

// -------------------------------------------------------------------------------------- validación previa --

public sealed record CuentaDeValidacionPreviaDto(string Code, string? Name);

public sealed record ErrorDeValidacionPreviaDto(string MessageType, int? LineNumber, CuentaDeValidacionPreviaDto? Account, string Rule, string Message,
    QuienCorrigeDto WhoFixes);

public sealed record AvisoDeValidacionPreviaDto(string MessageType, string Code, string Message);

/// <summary><c>PrevalidationResultDto</c> (§25.1). <c>PostingMode</c>: 1 en línea, 2 por lotes, 3 no pasa.</summary>
public sealed record ValidacionPreviaDto(
    bool Applies,
    int? PostingMode,
    bool Responded,
    bool? IsPostable,
    IReadOnlyList<ErrorDeValidacionPreviaDto> Errors,
    IReadOnlyList<AvisoDeValidacionPreviaDto> Warnings,
    long ElapsedMs)
{
    /// <summary>Los errores en la forma común con el 422 de la confirmación.</summary>
    public IReadOnlyList<HallazgoDeValidacionPreviaDto> Hallazgos() =>
        Errors.Select(e => new HallazgoDeValidacionPreviaDto(e.LineNumber, e.Account?.Code, e.Rule, e.Message, e.WhoFixes, e.MessageType)).ToList();
}

/// <summary>
/// Un hallazgo de la validación previa, venga de <c>prevalidate</c> o del 422 <c>Inventory.Prevalidation.NotPostable</c> de una
/// confirmación: la línea del documento, la cuenta, la regla y quién corrige. (nuevo)
/// </summary>
public sealed record HallazgoDeValidacionPreviaDto(int? LineNumber, string? Account, string Rule, string Message, QuienCorrigeDto? WhoFixes,
    string? MessageType = null);

/// <summary>Lee los <c>errors[]</c> del 422 de una confirmación (§25.1: «los mismos errores que la validación previa»). (nuevo)</summary>
public static class HallazgosDeValidacionPrevia
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Los hallazgos si <paramref name="codigo"/> es uno de los dos de la validación previa (la lista puede venir vacía en
    /// <c>NoResponse</c>); nulo si el error es otro.
    /// </summary>
    public static IReadOnlyList<HallazgoDeValidacionPreviaDto>? DelRechazo(string? codigo, JsonElement? data)
    {
        if (codigo is not (InventarioClient.ValidacionPreviaNoContabilizable or InventarioClient.ValidacionPreviaSinRespuesta)) return null;
        if (data is not { ValueKind: JsonValueKind.Object } d || !d.TryGetProperty("errors", out var errores) || errores.ValueKind != JsonValueKind.Array)
            return [];
        try { return errores.Deserialize<List<HallazgoDeValidacionPreviaDto>>(Opciones) ?? []; }
        catch (JsonException) { return []; }
    }
}
