using System.Diagnostics;
using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Transport;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Transport;
using IngenIA365ERP.Domain.Connectors;
using IngenIA365ERP.Domain.Enums.Connectors;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Core.Connectors;

/// <summary>
/// Los conectores como destino de la bandeja (feature 020, R16 a R18; T161; contracts/mensajes.md §3 de la 020): <c>Destino = CON</c>, el único
/// consumidor de <c>SolicitudDeConector</c> v1. Por cada unidad:
/// <list type="number">
/// <item>un envío anulado (el pago se reversó antes de salir) se rechaza sin llamar; uno ya enviado o cerrado a mano, ya está;</item>
/// <item>conector deshabilitado o sin credenciales: <c>Retry</c> y el envío <c>OnHold</c> (sale cuando se habilite o se pongan);</item>
/// <item>con las llamadas agotadas (<c>maxAttempts</c> de la versión sellada; las vueltas en espera no cuentan): <c>Rejected</c> con <c>Conector.ReintentosAgotados</c>, el envío
/// para revisar y una alerta;</item>
/// <item>si no, se autentica (token en memoria), se renderizan ruta y cuerpo con las variables del mensaje, se llama por
/// <see cref="IEjecutorDeConector"/> y se traduce con el mapa de respuestas: <c>Done</c> → <c>Processed</c> con la referencia
/// <c>externalId</c>; <c>DoneWithWarning</c> → <c>Processed</c> con aviso; <c>Retry</c> → <c>Retry</c>; <c>ReAuth</c> → se olvida el
/// token y se repite <b>una vez</b> (si vuelve, <c>Retry</c>); <c>ManualReview</c> o lo no previsto → <c>Rejected</c> y revisar.</item>
/// </list>
/// Cada intento queda registrado con <see cref="RegisterConnectorCallCommand"/>. El identificador externo es el mismo en todos los reintentos.
/// Nunca lanza: una excepción es <c>Retry</c>.
/// </summary>
public sealed class DestinoConectores(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    SesionDeConector sesion,
    AlmacenDeCredencialesDeConector almacen,
    IEjecutorDeConector ejecutor,
    ISender sender,
    IDateTimeService reloj,
    ILogger<DestinoConectores> logger,
    IAlertas? alertas = null) : IDestinoDeMensajes
{
    public const string CodigoAgotado = "Conector.ReintentosAgotados";
    public const string CodigoPagoAnulado = "Connector.PaymentVoided";
    public const string CodigoRevision = "Connector.ManualReview";

    public string Destino => IntegrationDestinations.Connectors;

    public bool Acepta(string type, int version) => version == 1 && string.Equals(type, SolicitudDeConectorV1.Type, StringComparison.Ordinal);

    public async Task<IReadOnlyList<ResultadoDeUnidad>> ConsumirAsync(TrabajoDeConsumo trabajo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trabajo);
        var resultados = new List<ResultadoDeUnidad>(trabajo.Unidades.Count);
        foreach (var unidad in trabajo.Unidades)
        {
            try
            {
                resultados.Add(new ResultadoDeUnidad(unidad, await UnaAsync(unidad, ct)));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Conectores.Envio] No se pudo enviar {Origen}: se reintenta.", unidad.OriginPublicId);
                resultados.Add(new ResultadoDeUnidad(unidad, new ResultadoDeConsumo.Retry(ex.Message)));
            }
        }
        return resultados;
    }

    private async Task<ResultadoDeConsumo> UnaAsync(UnidadDeConsumo unidad, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(unidad.MessagePublicIds, Destino, ct);
        if (leidos.IsFailure) return new ResultadoDeConsumo.Retry(leidos.Error.Message, leidos.Error.Code);
        var mensaje = leidos.Value.FirstOrDefault();
        if (mensaje?.Payload is not SolicitudDeConectorV1 solicitud)
            return new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Conectores no acepta {mensaje?.Type} v{mensaje?.Version}.");

        var envio = await db.ConnectorRequests.AsNoTracking().FirstOrDefaultAsync(r => r.MessagePublicId == mensaje.MessageId, ct);
        if (envio is null) return new ResultadoDeConsumo.Retry("El envío todavía no existe en la cooperativa.");
        switch (envio.Status)
        {
            case ConnectorRequestStatus.PaymentVoided:
                return new ResultadoDeConsumo.Rejected(CodigoPagoAnulado, "El pago se anuló antes de avisar: no se envía.");
            case ConnectorRequestStatus.Sent or ConnectorRequestStatus.SentWithWarning or ConnectorRequestStatus.ResolvedManually:
                return new ResultadoDeConsumo.AlreadyProcessed(solicitud.ExternalId);
        }

        var conector = await db.Connectors.AsNoTracking().Include(c => c.Versions).FirstOrDefaultAsync(c => c.Id == envio.ConnectorId, ct);
        var version = conector?.Versions.FirstOrDefault(v => v.Number == envio.VersionNumber);
        if (version is null) return new ResultadoDeConsumo.Retry("La versión del conector todavía no está en la cooperativa.");
        var d = DefinicionDeConector.Leer(version.DefinitionJson, out var problema);
        if (d is null) return await RevisarAsync(envio.MessagePublicId, solicitud, [], problema?.Mensaje ?? "La definición del conector no se puede leer.", ct);

        if (!conector!.IsEnabled || (d.Auth.Kind != ConnectorAuthKind.None && !await almacen.TieneAsync(conector.Id, ct)))
        {
            await sender.Send(new RegisterConnectorCallCommand(envio.MessagePublicId, [], ConnectorRequestStatus.OnHold), ct);
            return new ResultadoDeConsumo.Retry(conector.IsEnabled
                ? "El conector no tiene credenciales: el envío espera a que el administrador las ponga."
                : "El conector está deshabilitado: el envío espera a que se habilite.", "Connector.OnHold");
        }

        // Cuentan sólo las llamadas a la operación (no las vueltas en espera): un envío que esperó credenciales no llega agotado.
        if (envio.Attempts >= d.MaxAttempts)
            return await RevisarAsync(envio.MessagePublicId, solicitud, [], $"Se agotaron los {d.MaxAttempts} intentos.", ct, CodigoAgotado);

        var valores = Valores(solicitud.Variables);
        var sensibles = new HashSet<string>(solicitud.Sensitive, StringComparer.Ordinal);
        var (ruta, faltanRuta) = RenderizadorDePlantilla.Renderizar(d.Operation.Path, valores);
        var (cuerpo, faltanCuerpo) = RenderizadorDePlantilla.Renderizar(d.Operation.Body, valores);
        if (ruta is null || cuerpo is null)
            return await RevisarAsync(envio.MessagePublicId, solicitud, [],
                $"Faltan datos para el envío: {string.Join(", ", faltanRuta.Concat(faltanCuerpo).Distinct())}.", ct);
        var url = SesionDeConector.Unir(version.BaseUrl, ruta);
        var urlSaneada = SesionDeConector.Unir(version.BaseUrl, RenderizadorDePlantilla.Sanear(d.Operation.Path, valores, sensibles)).ToString();
        var cuerpoSaneado = RenderizadorDePlantilla.Sanear(d.Operation.Body, valores, sensibles);

        var llamadas = new List<LlamadaDeConector>();
        ConnectorOutcome resultado = ConnectorOutcome.ManualReview;
        RespuestaHttp? respuesta = null;
        for (var vuelta = 0; vuelta < 2; vuelta++)
        {
            var inicioLogin = reloj.UtcNow;
            var auth = await sesion.AutenticarAsync(conector.Id, version.Number, version.BaseUrl, d, forzar: vuelta > 0, ct);
            if (auth.Login is { } login)
                llamadas.Add(new LlamadaDeConector(ConnectorCallKind.Login, inicioLogin, login.DuracionMs, login.Codigo,
                    auth.Ok ? ConnectorOutcome.Done : ConnectorOutcome.Retry, null, null, SesionDeConector.Unir(version.BaseUrl, d.Auth.Path ?? "/").ToString()));
            if (!auth.Ok)
            {
                // Red o servidor caído en el inicio de sesión: se reintenta; un rechazo del proveedor (4xx) pide revisar las credenciales.
                if (auth.Codigo is 0 or >= 500)
                {
                    await sender.Send(new RegisterConnectorCallCommand(envio.MessagePublicId, llamadas, ConnectorRequestStatus.Retrying), ct);
                    return new ResultadoDeConsumo.Retry(auth.Mensaje, "Connector.AuthUnavailable");
                }
                return await RevisarAsync(envio.MessagePublicId, solicitud, llamadas, auth.Mensaje, ct);
            }

            var cabeceras = new Dictionary<string, string>(auth.Cabeceras);
            var inicio = reloj.UtcNow;
            var reloj2 = Stopwatch.StartNew();
            respuesta = await ejecutor.EjecutarAsync(new SolicitudHttp(d.Operation.Method.ToUpperInvariant(), url, cabeceras,
                string.IsNullOrEmpty(cuerpo) ? null : cuerpo, d.TimeoutSeconds, $"conector:{conector.Id}"), ct);
            reloj2.Stop();
            resultado = respuesta.DireccionBloqueada ? ConnectorOutcome.ManualReview : MapaDeRespuestas.Clasificar(d.Responses, respuesta.Codigo, respuesta.Cuerpo);
            llamadas.Add(new LlamadaDeConector(ConnectorCallKind.Operation, inicio, respuesta.DuracionMs > 0 ? respuesta.DuracionMs : (int)reloj2.ElapsedMilliseconds,
                respuesta.Codigo, resultado, respuesta.Extracto(), cuerpoSaneado, urlSaneada));

            if (resultado != ConnectorOutcome.ReAuth) break;
            sesion.Invalidar(conector.Id, version.Number);
        }

        var texto = respuesta?.Extracto() ?? respuesta?.Error;
        switch (resultado)
        {
            case ConnectorOutcome.Done:
                await sender.Send(new RegisterConnectorCallCommand(envio.MessagePublicId, llamadas, ConnectorRequestStatus.Sent), ct);
                return new ResultadoDeConsumo.Processed(solicitud.ExternalId, null, null);
            case ConnectorOutcome.DoneWithWarning:
                await sender.Send(new RegisterConnectorCallCommand(envio.MessagePublicId, llamadas, ConnectorRequestStatus.SentWithWarning,
                    texto ?? "El sistema externo respondió con un aviso."), ct);
                return new ResultadoDeConsumo.Processed(solicitud.ExternalId, null, null);
            case ConnectorOutcome.Retry or ConnectorOutcome.ReAuth:
                await sender.Send(new RegisterConnectorCallCommand(envio.MessagePublicId, llamadas, ConnectorRequestStatus.Retrying), ct);
                return new ResultadoDeConsumo.Retry($"El sistema externo respondió {respuesta?.Codigo}: {texto}", "Connector.Retry");
            default:
                return await RevisarAsync(envio.MessagePublicId, solicitud, llamadas,
                    $"El sistema externo respondió {respuesta?.Codigo}: {texto ?? "sin cuerpo"}", ct);
        }
    }

    /// <summary>El envío queda para revisar a mano, con su alerta, y la entrega <c>Rejected</c> (no se reintenta sola).</summary>
    private async Task<ResultadoDeConsumo> RevisarAsync(Guid mensaje, SolicitudDeConectorV1 solicitud, IReadOnlyList<LlamadaDeConector> llamadas, string motivo,
        CancellationToken ct, string codigo = CodigoRevision)
    {
        await sender.Send(new RegisterConnectorCallCommand(mensaje, llamadas, ConnectorRequestStatus.Review, motivo), ct);
        if (alertas is not null)
        {
            try
            {
                await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.ConectorReintentosAgotados,
                    "Un envío a un sistema externo pide revisión",
                    $"{motivo} Origen {solicitud.OriginRef}. Reintente o ciérrelo a mano en Transporte › Envíos.",
                    "ConnectorRequest", solicitud.ExternalId, DedupKey: $"{TiposDeAlerta.ConectorReintentosAgotados}:{solicitud.ExternalId:N}",
                    RecipientPermissions: [TransportPermissions.NotificationsManage], ModuloDeOrigen: ModulosDeOrigen.Transporte), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[Conectores.Alerta] No se pudo levantar la alerta del envío {Envio}.", solicitud.ExternalId);
            }
        }
        return new ResultadoDeConsumo.Rejected(codigo, motivo.Length > 500 ? motivo[..500] : motivo);
    }

    /// <summary>Las variables del mensaje con su tipo del catálogo (las fechas como fecha, los valores como número).</summary>
    public static Dictionary<string, object?> Valores(IReadOnlyDictionary<string, string> variables)
    {
        var valores = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (nombre, texto) in variables)
        {
            valores[nombre] = VariablesDeConector.Buscar(nombre)?.Tipo switch
            {
                TipoDeVariable.Decimal when decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
                TipoDeVariable.Entero when long.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var e) => e,
                TipoDeVariable.Fecha when DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) => f,
                _ => texto,
            };
        }
        return valores;
    }

    /// <summary>Una unidad por mensaje: cada aviso es su propio recibo.</summary>
    public IReadOnlyList<TrabajoDeConsumo> PlanearLote(IReadOnlyList<MensajeEntrante> entregas)
    {
        ArgumentNullException.ThrowIfNull(entregas);
        return entregas
            .GroupBy(m => (m.Envelope.OriginModule, m.Envelope.Origin.PublicId, m.Envelope.OriginEventKey))
            .Select(g => TrabajoDeConsumo.DeUnaUnidad(new UnidadDeConsumo(Destino, g.Key.PublicId, g.Key.OriginEventKey,
                g.Select(m => m.MessageId).ToList(),
                g.Select(m => m.Entrega.BatchPublicId).FirstOrDefault(b => b is not null),
                g.Max(m => m.Entrega.Attempts))
            {
                OriginModule = g.Key.OriginModule,
            }))
            .ToList();
    }
}
