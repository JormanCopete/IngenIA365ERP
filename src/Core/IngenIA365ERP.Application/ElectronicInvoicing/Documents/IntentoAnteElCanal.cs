using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing.Canonical;
using IngenIA365ERP.Application.ElectronicInvoicing.Channels;
using IngenIA365ERP.Application.ElectronicInvoicing.Contingencies;
using IngenIA365ERP.Application.ElectronicInvoicing.Numeracion;
using IngenIA365ERP.Application.ElectronicInvoicing.Settings;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.ElectronicInvoicing.Transactions;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>El resultado de un intento contra el canal (api.md §24.4: <c>{ status, outcome, messages[] }</c>). (nuevo)</summary>
/// <param name="Attempted">Falso si no se llamó al canal: todavía no le tocaba, o espera a otro documento.</param>
/// <param name="Detail">Por qué no se intentó, para la pantalla.</param>
public sealed record ElectronicTransmissionResultDto(
    Guid ElectronicDocumentPublicId,
    ElectronicDocumentStatus Status,
    ChannelOutcome? Outcome,
    IReadOnlyList<MensajeDelCanalDto> Messages,
    bool Attempted,
    string? Detail = null);

/// <summary>Qué pide quien llama. (nuevo)</summary>
public enum PedidoDeIntento
{
    /// <summary>El procesador o el intento en línea del POS: respeta la espera y decide la operación por el estado.</summary>
    Automatico = 1,

    /// <summary>«Reintentar ahora» de una persona: adelanta la espera; en <c>Sent</c> responde <c>AwaitingResponse</c>.</summary>
    ReintentarAhora = 2,

    /// <summary>«Consultar a la DIAN»: siempre <c>QueryStatus</c>, también para confirmar un rechazo.</summary>
    Consultar = 3,
}

/// <summary>
/// Un intento contra el canal <b>sellado</b> del documento (feature 012, I4, T712, T713; contracts/dian.md §5, §6 y §7.1), fuera de la
/// transacción de confirmación. Lo comparten <see cref="EmitElectronicDocumentCommand"/> y <see cref="QueryElectronicDocumentStatusCommand"/>:
/// <list type="number">
/// <item>toma el arrendamiento de la fila (<see cref="IArrendamientoDeDocumentoElectronico"/>); otro lo tiene →
/// <c>ElectronicInvoicing.Document.InProgress</c> con <c>data.leaseUntil</c>; todavía no le toca → no intenta;</item>
/// <item>decide la operación: <c>Emit</c> en <c>Pending</c>, <c>TransmitContingency</c> en contingencia (la 03 sólo con su evento cerrado) y
/// <c>QueryStatus</c> en <c>Sent</c> o si el intento anterior cayó entre la llamada y su registro (un arrendamiento vencido sin soltar): ante
/// lo ambiguo <b>siempre se consulta primero</b>;</item>
/// <item>no transmite mientras <c>WaitsForDocumentId</c> no esté validado;</item>
/// <item>el canónico se reconstruye y se compara con la versión, y se sube si falta (<see cref="GuardadoDeArtefactos"/>);</item>
/// <item>llama al canal con <see cref="ContextoDeCanal"/> (credenciales por <see cref="ICredencialesDeCanal"/>, la clave
/// <c>{tenant:N}:{Environment}:{Number}:v{n}</c>) y, en <b>otra</b> transacción, agrega exactamente una transmisión inmutable, los artefactos,
/// el estado por <see cref="TransicionesDelDocumentoElectronico"/>, <c>AttemptCount</c>, <c>NextAttemptAt</c> (<see cref="EsperasDeReintento"/>),
/// <c>LastMessagesJson</c>, y suelta el arrendamiento;</item>
/// <item>después: el evento 04 (se une o se abre, y se cierra con una respuesta definitiva posterior), la falla o la respuesta al circuito,
/// <c>Dian.DocumentoRechazado</c>, y —validado o en contingencia 04— la representación gráfica y la entrega al comprador.</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class IntentoAnteElCanal(
    IApplicationDbContext db,
    ICanalesDeEmision canales,
    ICredencialesDeCanal credenciales,
    ICurrentTenantService tenant,
    IDateTimeService reloj,
    IActorActual actorActual,
    IArrendamientoDeDocumentoElectronico arrendamiento,
    EsperasDeReintento esperas,
    GuardadoDeArtefactos artefactos,
    ContingenciaDeLaDian contingencias,
    IAlertas alertas,
    ILogger<IntentoAnteElCanal> logger,
    IRegistroDeFallasDelCanal? fallas = null,
    GeneracionDeRepresentacion? representacion = null,
    EntregaAlComprador? entrega = null)
{
    private static readonly JsonSerializerOptions Json = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Quién toma la fila: la réplica y un número del intento.</summary>
    private readonly string _dueno = Recortar($"{System.Environment.MachineName}:{Guid.NewGuid():N}", 100);

    public async Task<Result<ElectronicTransmissionResultDto>> IntentarAsync(Guid documentoPublicId, PedidoDeIntento pedido, CancellationToken ct)
    {
        var previo = await db.ElectronicDocuments.AsNoTracking()
            .Where(d => d.PublicId == documentoPublicId)
            .Select(d => new { d.Id, d.Status, d.LeaseUntil, d.LeaseOwner })
            .FirstOrDefaultAsync(ct);
        if (previo is null) return Falla(ErroresDeDocumentosElectronicos.NotFound());

        var estadoPrevio = previo.Status;
        if (TransicionesDelDocumentoElectronico.EsFinal(estadoPrevio))
            return Falla(new Error(TransicionesDelDocumentoElectronico.CodigoFinal, $"El documento está {estadoPrevio}: no cambia más."));
        if (pedido == PedidoDeIntento.ReintentarAhora && estadoPrevio == ElectronicDocumentStatus.Sent)
            return Falla(new Error(TransicionesDelDocumentoElectronico.CodigoEsperaRespuesta,
                "El documento se envió y no tiene respuesta: se consulta a la DIAN, no se reenvía."));
        if (pedido != PedidoDeIntento.Consultar && estadoPrevio == ElectronicDocumentStatus.Rejected)
            return Falla(ErroresDeDocumentosElectronicos.NoAdmite(estadoPrevio, "reintentos: se corrige, se reemplaza o se cancela"));
        if (pedido == PedidoDeIntento.Consultar && estadoPrevio is ElectronicDocumentStatus.IssuerContingency or ElectronicDocumentStatus.DianContingency)
            return Falla(ErroresDeDocumentosElectronicos.NoAdmite(estadoPrevio, "consultas: se transmite al cerrarse la contingencia"));

        var ahora = reloj.UtcNow;
        // El intento anterior tomó la fila y no la soltó: pudo caer entre la llamada y su registro. Se consulta antes de reenviar (§6.3).
        var ambiguo = previo.LeaseOwner is not null && previo.LeaseUntil is { } vencio && vencio < ahora;

        var tomado = await arrendamiento.TomarAsync(previo.Id, ahora, ahora + esperas.Arrendamiento, _dueno,
            respetarEspera: pedido == PedidoDeIntento.Automatico, ct);
        if (!tomado)
        {
            var actual = await db.ElectronicDocuments.AsNoTracking().Where(d => d.Id == previo.Id)
                .Select(d => new { d.Status, d.LeaseUntil }).FirstAsync(ct);
            return actual.LeaseUntil is { } hasta && hasta >= ahora
                ? Falla(ErroresDeDocumentosElectronicos.InProgress(hasta))
                : Result.Success(new ElectronicTransmissionResultDto(documentoPublicId, actual.Status, null, [], false, "Todavía no le toca otro intento."));
        }

        var documento = await db.ElectronicDocuments
            .Include(d => d.Versions)
            .Include(d => d.EmissionSetting)
            .Include(d => d.Resolution!).ThenInclude(r => r.Channels)
            .Include(d => d.ContingencyEvent)
            .Include(d => d.WaitsForDocument)
            .FirstAsync(d => d.Id == previo.Id, ct);

        try
        {
            return await IntentarConLaFilaAsync(documento, pedido, ambiguo, ahora, ct);
        }
        finally
        {
            if (documento.LeaseOwner == _dueno)
            {
                Soltar(documento);
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// La nota que esperaba al original (T738; FR-066): se registró sin el código único del original, que estaba en contingencia o sin respuesta;
    /// validado el original, la nota nace de nuevo (versión <c>ReferenceCompleted</c>) con la referencia completa, siempre que la huella
    /// económica no cambie. Sin eso, la reconstrucción no coincidiría con lo registrado y la nota nunca se transmitiría.
    /// </summary>
    private async Task<Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentVersion> CompletarReferenciaAsync(ElectronicDocument documento,
        Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentVersion version, CancellationToken ct)
    {
        if (documento.WaitsForDocument is not { UniqueCode: not null } esperado || documento.CorrectsDocumentId != esperado.Id
            || version.Reason != DocumentVersionReason.Initial)
            return version;
        var rehecho = await artefactos.ReconstruirAsync(documento, version, ct);
        if (rehecho.IsFailure || string.Equals(rehecho.Value.CanonicalSha256, version.CanonicalSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(rehecho.Value.EconomicFingerprint, version.EconomicFingerprint, StringComparison.OrdinalIgnoreCase))
            return version;

        var nueva = new Domain.Entities.ElectronicInvoicing.Transactions.ElectronicDocumentVersion
        {
            ElectronicDocumentId = documento.Id,
            VersionNumber = (short)(version.VersionNumber + 1),
            SourceDocumentPublicId = version.SourceDocumentPublicId,
            Reason = DocumentVersionReason.ReferenceCompleted,
            CanonicalSchemaVersion = (short)rehecho.Value.Documento.SchemaVersion,
            CanonicalSha256 = rehecho.Value.CanonicalSha256,
            EconomicFingerprint = rehecho.Value.EconomicFingerprint,
            CorrectionReason = $"El documento que corrige ({esperado.Number}) quedó validado: la nota lleva su código único.",
        };
        documento.Versions.Add(nueva);
        await db.SaveChangesAsync(ct);
        documento.CurrentVersionId = nueva.Id;
        return nueva;
    }

    private async Task<Result<ElectronicTransmissionResultDto>> IntentarConLaFilaAsync(ElectronicDocument documento, PedidoDeIntento pedido,
        bool ambiguo, DateTime ahora, CancellationToken ct)
    {
        var operacion = pedido == PedidoDeIntento.Consultar ? TransmissionOperation.QueryStatus : documento.Status switch
        {
            ElectronicDocumentStatus.Pending => ambiguo ? TransmissionOperation.QueryStatus : TransmissionOperation.Emit,
            ElectronicDocumentStatus.Sent => TransmissionOperation.QueryStatus,
            ElectronicDocumentStatus.IssuerContingency or ElectronicDocumentStatus.DianContingency => TransmissionOperation.TransmitContingency,
            _ => (TransmissionOperation?)null,
        };
        if (operacion is null)
            return Falla(ErroresDeDocumentosElectronicos.NoAdmite(documento.Status, "otro intento"));

        if (documento.Status == ElectronicDocumentStatus.IssuerContingency && documento.ContingencyEvent is { EstaAbierto: true })
            return Falla(new Error(TransicionesDelDocumentoElectronico.CodigoContingenciaAbierta,
                "La contingencia de facturación sigue abierta: el documento se transmite al cerrarla."));

        if (operacion != TransmissionOperation.QueryStatus && documento.WaitsForDocument is { } previo
            && previo.Status is not (ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices))
        {
            documento.NextAttemptAt = ahora + esperas.Tras(1);
            return Result.Success(new ElectronicTransmissionResultDto(documento.PublicId, documento.Status, null, [], false,
                $"Espera a que el documento {previo.Number} quede validado por la DIAN."));
        }

        var version = documento.Versions.OrderByDescending(v => v.VersionNumber).First();
        if (operacion != TransmissionOperation.QueryStatus) version = await CompletarReferenciaAsync(documento, version, ct);
        var cooperativa = ConfiguracionDeEmision.Cooperativa(tenant);
        if (cooperativa.IsFailure) return Falla(cooperativa.Error);

        DocumentoElectronicoCanonico? canonico = null;
        if (operacion != TransmissionOperation.QueryStatus)
        {
            var verificado = await artefactos.CanonicoVerificadoAsync(documento, version, ct);
            if (verificado.IsFailure) return Falla(verificado.Error);
            canonico = verificado.Value;
        }

        var actor = await actorActual.ObtenerAsync(ct);
        var clave = ContextoDeCanal.ClaveDeIdempotencia(cooperativa.Value, documento.Environment, documento.Prefix, documento.Consecutive, version.VersionNumber);
        var canal = canales.Resolver(documento.ChannelCode);
        var contexto = await ContextoAsync(documento, cooperativa.Value, clave, ct);

        var pedidoEn = reloj.UtcNow;
        var cronometro = Stopwatch.StartNew();
        ResultadoDeCanal resultado;
        string enviado;
        if (contexto.IsFailure)
        {
            resultado = ResultadoDeCanal.Sin(ChannelOutcome.ChannelUnavailable, 0,
                new MensajeDelCanal("Credencial", TipoDeMensajeDelCanal.Rechazo, contexto.Error.Message, contexto.Error.Message));
            enviado = clave;
        }
        else
        {
            var referencia = new ReferenciaDeEnvio(documento.Prefix, documento.Consecutive, documento.Environment, documento.UniqueCode,
                await UltimaReferenciaAsync(documento.Id, ct));
            enviado = canonico is null ? $"{clave}|{referencia.Numero}|{referencia.UniqueCode}|{referencia.ExternalReference}" : version.CanonicalSha256;
            resultado = await LlamarAsync(canal, operacion.Value, canonico, referencia, contexto.Value, ct);
        }
        cronometro.Stop();

        // ---------------------------------------------------------------------------- registro del resultado --
        var evento = operacion switch
        {
            TransmissionOperation.Emit => EventoDelDocumentoElectronico.Emitir,
            TransmissionOperation.TransmitContingency => EventoDelDocumentoElectronico.TransmitirContingencia,
            _ => EventoDelDocumentoElectronico.ConsultarEstado,
        };
        var estadoAntes = documento.Status;
        var eventoCerrado = documento.ContingencyEvent is not { EstaAbierto: true };
        var transicion = documento.AplicarEvento(evento, new RespuestaDelCanal(resultado.Outcome, resultado.TieneCodigoUnico, resultado.TieneRespuestaDeValidacion),
            ahora, eventoCerrado);
        if (!transicion.Procede)
        {
            documento.LastOutcome = resultado.Outcome;
            logger.LogWarning("[FE.ResultadoFueraDeLaTabla] {Numero} {Estado} {Evento} {Resultado}: {Motivo}",
                documento.Number, estadoAntes, evento, resultado.Outcome, transicion.Motivo);
        }

        if (resultado.TieneCodigoUnico)
        {
            documento.UniqueCode ??= Recortar(resultado.UniqueCode!, 96);
            documento.UniqueCodeKind ??= TipoDeCodigo(resultado.UniqueCodeKind, documento.Kind);
        }
        if (!string.IsNullOrWhiteSpace(resultado.QrContent)) documento.QrContent ??= Recortar(resultado.QrContent, 1000);
        if (transicion.Procede && transicion.Contingencia == ContingencyType.Dian04 && !string.IsNullOrWhiteSpace(resultado.DianDocumentTypeCode))
            documento.DianDocumentTypeCode = Recortar(resultado.DianDocumentTypeCode, 2);

        var respuestaDeValidacion = await artefactos.GuardarDelCanalAsync(documento, version, resultado, ct);

        var intento = await db.ElectronicDocumentTransmissions.Where(t => t.ElectronicDocumentId == documento.Id)
            .Select(t => (int?)t.AttemptNumber).MaxAsync(ct) ?? 0;
        var mensajesCrudos = resultado.Mensajes.Select(m => new { regla = m.Regla, tipo = TipoTexto(m.Tipo), texto = m.Texto }).ToList();
        var mensajesTraducidos = resultado.Mensajes
            .Select(m => new { regla = m.Regla, tipo = TipoTexto(m.Tipo), texto = m.Texto, traduccion = m.Traduccion ?? m.Texto }).ToList();
        db.ElectronicDocumentTransmissions.Add(new ElectronicDocumentTransmission
        {
            ElectronicDocumentId = documento.Id,
            VersionId = version.Id,
            AttemptNumber = intento + 1,
            Operation = operacion.Value,
            ChannelCode = documento.ChannelCode,
            RequestedAt = pedidoEn,
            CompletedAt = pedidoEn.AddMilliseconds(cronometro.ElapsedMilliseconds),
            DurationMs = (int)Math.Min(int.MaxValue, Math.Max(cronometro.ElapsedMilliseconds, resultado.DurationMs)),
            RequestedByKind = actor.Kind,
            RequestedByUserId = actor.UserId,
            RequestedByName = Recortar(actor.Name, 150),
            IdempotencyKey = Recortar(clave, 120),
            RequestSha256 = enviado.Length == 64 && enviado.All(Uri.IsHexDigit) ? enviado.ToLowerInvariant() : Sha256(enviado),
            Outcome = resultado.Outcome,
            ProviderCode = Recortar(resultado.ProviderCode, 20),
            DianStatusCode = Recortar(resultado.DianStatusCode, 10),
            IsValid = resultado.Outcome is ChannelOutcome.Validated or ChannelOutcome.ValidatedWithNotices ? true
                : resultado.Outcome is ChannelOutcome.Rejected or ChannelOutcome.InvalidData ? false : null,
            RawMessagesJson = mensajesCrudos.Count == 0 ? null : JsonSerializer.Serialize(mensajesCrudos, Json),
            TranslatedMessagesJson = mensajesTraducidos.Count == 0 ? null : JsonSerializer.Serialize(mensajesTraducidos, Json),
            ExternalReference = Recortar(resultado.ExternalReference, 100),
            ApplicationResponseAttachmentPublicId = respuestaDeValidacion,
            CorrelationId = null,
        });

        documento.AttemptCount++;
        documento.LastMessagesJson = mensajesTraducidos.Count == 0 ? documento.LastMessagesJson : JsonSerializer.Serialize(mensajesTraducidos, Json);
        documento.NextAttemptAt = SiguienteIntento(documento, transicion, ahora);
        documento.CurrentVersionId ??= version.Id;

        DianContingencyEvent? eventoAbierto = null;
        if (transicion.Procede && transicion.Contingencia == ContingencyType.Dian04)
        {
            var (evento04, abierto) = await contingencias.UnirOAbrirAsync(documento, ahora, new QuienDeclara(actor.Kind, actor.UserId, actor.Name), ct);
            if (abierto) eventoAbierto = evento04;
        }
        var respuestaDefinitivaDeLaDian = transicion.Procede
            && (documento.Status is ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices
                || (documento.Status == ElectronicDocumentStatus.Rejected && documento.RejectedBy == RejectedBy.Dian));
        if (respuestaDefinitivaDeLaDian) await contingencias.CerrarPorRespuestaAsync(documento.ChannelCode, ahora, ct);

        Soltar(documento);
        await db.SaveChangesAsync(ct);

        // ---------------------------------------------------------------------------- después de guardar --
        await DespuesAsync(documento, version, estadoAntes, transicion, resultado, eventoAbierto, ct);

        var mensajes = resultado.Mensajes.Select(m => new MensajeDelCanalDto(m.Regla, TipoTexto(m.Tipo), m.Texto, m.Traduccion)).ToList();
        return Result.Success(new ElectronicTransmissionResultDto(documento.PublicId, documento.Status, resultado.Outcome, mensajes, true));
    }

    private async Task<ResultadoDeCanal> LlamarAsync(ICanalDeEmisionElectronica canal, TransmissionOperation operacion, DocumentoElectronicoCanonico? canonico,
        ReferenciaDeEnvio referencia, ContextoDeCanal contexto, CancellationToken ct)
    {
        try
        {
            return operacion == TransmissionOperation.QueryStatus
                ? await canal.ConsultarEstadoAsync(referencia, contexto, ct)
                : await canal.EmitirAsync(canonico!, contexto, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Un adaptador no debe lanzar (§3.3). Si lo hace, la emisión pudo haber llegado: queda ambigua (se consulta).
            logger.LogError(ex, "[FE.CanalLanzo] {Canal} {Operacion} {Numero}", canal.ChannelCode, operacion, referencia.Numero);
            return ResultadoDeCanal.Sin(operacion == TransmissionOperation.QueryStatus ? ChannelOutcome.ChannelUnavailable : ChannelOutcome.InProcess);
        }
    }

    private async Task<Result<ContextoDeCanal>> ContextoAsync(ElectronicDocument documento, Guid cooperativa, string clave, CancellationToken ct)
    {
        var configuracion = documento.EmissionSetting ?? await db.ElectronicEmissionSettings.FirstAsync(s => s.Id == documento.EmissionSettingId, ct);
        CredencialesDeCanal? credencial = null;
        var resuelta = await credenciales.ResolverAsync(documento.ChannelCode, ct);
        if (resuelta.IsSuccess) credencial = resuelta.Value;
        else if (!string.Equals(documento.ChannelCode, GuardiaDeEmisionFiscal.CanalSimulado, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<ContextoDeCanal>(resuelta.Error);

        var claveTecnica = documento.Mode == EmissionMode.OwnSoftware && documento.Resolution is { } r
            ? ReglasDeResolucion.AsociacionVigente(r, documento.ChannelCode, documento.SoftwareId, documento.IssueDate)?.TechnicalKey
            : null;
        return Result.Success(new ContextoDeCanal(cooperativa, configuracion.IssuerTaxId, configuracion.IssuerCheckDigit, documento.Mode,
            documento.Environment, documento.SoftwareId, configuracion.TestSetId, claveTecnica, clave, credencial));
    }

    private Task<string?> UltimaReferenciaAsync(int documentoId, CancellationToken ct) =>
        db.ElectronicDocumentTransmissions.AsNoTracking()
            .Where(t => t.ElectronicDocumentId == documentoId && t.ExternalReference != null)
            .OrderByDescending(t => t.AttemptNumber)
            .Select(t => t.ExternalReference)
            .FirstOrDefaultAsync(ct);

    /// <summary>Cuándo le toca otra vez: nunca en un final o un rechazo; de inmediato tras <c>NotFound</c>; si no, la espera que sigue.</summary>
    private DateTime? SiguienteIntento(ElectronicDocument documento, ResultadoDeTransicion transicion, DateTime ahora)
    {
        if (documento.EsFinal || documento.Status == ElectronicDocumentStatus.Rejected) return null;
        if (transicion.ReenviarMismaVersion) return ahora;
        return ahora + esperas.Tras(documento.AttemptCount);
    }

    private async Task DespuesAsync(ElectronicDocument documento, ElectronicDocumentVersion version, ElectronicDocumentStatus antes,
        ResultadoDeTransicion transicion, ResultadoDeCanal resultado, DianContingencyEvent? eventoAbierto, CancellationToken ct)
    {
        await Seguro("circuito", async () =>
        {
            if (fallas is null) return;
            if (transicion.CuentaComoFallaDelCanal) await fallas.RegistrarFallaAsync(documento.ChannelCode, ct);
            else if (resultado.Outcome != ChannelOutcome.ChannelUnavailable) await fallas.RegistrarRespuestaAsync(documento.ChannelCode, ct);
        });

        if (eventoAbierto is not null) await Seguro("contingencia", () => contingencias.AvisarAperturaAsync(eventoAbierto, ct));

        if (documento.Status == ElectronicDocumentStatus.Rejected && antes != ElectronicDocumentStatus.Rejected)
            await Seguro("rechazo", () => alertas.LevantarAsync(new AlertaALevantar(
                TiposDeAlerta.DocumentoRechazado,
                $"Documento electrónico {documento.Number} rechazado",
                RechazoEnTexto(documento, resultado),
                EntityType: GuardadoDeArtefactos.EntidadDelDocumento,
                EntityPublicId: documento.PublicId,
                DedupKey: $"{TiposDeAlerta.DocumentoRechazado}:{documento.PublicId:N}"), ct));

        var validado = documento.Status is ElectronicDocumentStatus.Validated or ElectronicDocumentStatus.ValidatedWithNotices;
        if (validado)
            await Seguro("sin validar", () => alertas.AtenderPorProcesoAsync(GuardadoDeArtefactos.ClaveSinValidar(documento.PublicId),
                "La DIAN validó el documento.", ct));

        var entregable = validado || (documento.Status == ElectronicDocumentStatus.DianContingency && antes != ElectronicDocumentStatus.DianContingency);
        if (entregable && antes != documento.Status)
        {
            byte[]? pdf = null;
            if (representacion is not null)
                await Seguro("representación", async () => pdf = await representacion.GenerarAsync(documento, version, ct));
            if (entrega is not null)
            {
                var attached = resultado.Artefactos.FirstOrDefault(a => a.Tipo == TipoDeArtefacto.AttachedDocument);
                await Seguro("entrega", () => entrega.EntregarAsync(documento, version, pdf, attached?.Bytes, forzar: false, ct));
            }
        }
    }

    private async Task Seguro(string que, Func<Task> accion)
    {
        try
        {
            await accion();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[FE.DespuesDelIntento] Falló {Que}; el resultado del canal ya quedó registrado.", que);
        }
    }

    private static string RechazoEnTexto(ElectronicDocument documento, ResultadoDeCanal resultado)
    {
        var motivos = resultado.Mensajes.Where(m => m.Tipo == TipoDeMensajeDelCanal.Rechazo).Select(m => $"{m.Regla}: {m.Traduccion ?? m.Texto}").ToList();
        var quien = documento.RejectedBy == RejectedBy.Channel ? "El canal" : "La DIAN";
        var texto = motivos.Count == 0
            ? $"{quien} rechazó el documento {documento.Number}. Corríjalo, reemplácelo o cancélelo desde Documentos electrónicos."
            : $"{quien} rechazó el documento {documento.Number}: {string.Join(" | ", motivos)}";
        return texto.Length <= 2000 ? texto : texto[..2000];
    }

    private static UniqueCodeKind? TipoDeCodigo(string? texto, ElectronicDocumentKind tipo) =>
        Enum.TryParse<UniqueCodeKind>(texto, ignoreCase: true, out var k) ? k
        : tipo is ElectronicDocumentKind.SupportDocument or ElectronicDocumentKind.SupportDocumentAdjustmentNote ? UniqueCodeKind.Cuds
        : tipo is ElectronicDocumentKind.Invoice ? UniqueCodeKind.Cufe
        : UniqueCodeKind.Cude;

    private void Soltar(ElectronicDocument documento)
    {
        if (documento.LeaseOwner != _dueno) return;
        documento.LeaseUntil = null;
        documento.LeaseOwner = null;
    }

    private static string TipoTexto(TipoDeMensajeDelCanal tipo) => tipo == TipoDeMensajeDelCanal.Rechazo ? "Rejection" : "Notice";

    private static string Sha256(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(texto))]
    private static string? Recortar(string? texto, int maximo) => texto is null ? null : texto.Length <= maximo ? texto : texto[..maximo];

    private static Result<ElectronicTransmissionResultDto> Falla(Error error) => Result.Failure<ElectronicTransmissionResultDto>(error);
}
