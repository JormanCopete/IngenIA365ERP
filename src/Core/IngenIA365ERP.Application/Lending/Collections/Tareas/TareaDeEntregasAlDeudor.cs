using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Collections.Tareas;

/// <summary>
/// La tarea programada <c>cartera.entregas</c> (feature 013, T0307; T75, F-06; DT §2.13; contracts/adaptadores-y-archivos.md §4.6): cada
/// <see cref="Cadencia"/>, en cada cooperativa, por <see cref="IEjecutorEnCooperativa"/>. Sale si Cartera no está activada. Para los mensajes
/// enviados de las últimas <see cref="HorasHaciaAtras"/> horas sin desenlace —y, fuera de producción, los simulados— cuyo canal declara
/// consulta de entrega, llama <c>ConsultarEntregaAsync</c> del adaptador <b>sellado</b> y registra <c>Delivered</c> o <c>Failed</c> por
/// <see cref="RegisterDebtorMessageAttemptCommand"/>. El correo no tiene consulta: su <c>Sent</c> es el último evento. Sin webhooks: se
/// resuelve por sondeo. Singleton. (nuevo)
/// </summary>
public sealed class TareaDeEntregasAlDeudor : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.entregas";

    /// <summary>Cuántas horas atrás se siguen consultando las entregas (§4.6).</summary>
    public const int HorasHaciaAtras = 72;

    /// <summary>Cuántos mensajes consulta cada pasada.</summary>
    public const int TamanoDeTanda = 200;

    /// <summary>Cada cuarto de hora.</summary>
    public static readonly TimeSpan Cadencia = TimeSpan.FromMinutes(15);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Cadencia;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var cola = servicios.GetRequiredService<ColaDeMensajesAlDeudor>();
        if (!await cola.CarteraActivadaAsync(ct)) return;

        var db = servicios.GetRequiredService<IApplicationDbContext>();
        var canales = servicios.GetRequiredService<ICanalesDeMensajeria>();
        var sender = servicios.GetRequiredService<ISender>();
        var logger = servicios.GetService<ILogger<TareaDeEntregasAlDeudor>>() ?? NullLogger<TareaDeEntregasAlDeudor>.Instance;

        foreach (var id in await cola.PorConfirmarEntregaAsync(HorasHaciaAtras, TamanoDeTanda, ct))
        {
            try
            {
                var mensaje = await db.DebtorMessages.AsNoTracking().FirstAsync(m => m.Id == id, ct);
                var ultimo = await db.DebtorMessageEvents.AsNoTracking().Where(e => e.DebtorMessageId == id)
                    .OrderByDescending(e => e.Sequence).FirstAsync(ct);
                var contexto = await ContextoDeEnvio.ArmarAsync(servicios, mensaje.ChannelCode, mensaje.PublicId, ct);
                if (contexto is null || ultimo.ProviderReference is null) continue;

                var r = await canales.Resolver(mensaje.ChannelCode).ConsultarEntregaAsync(ultimo.ProviderReference, contexto, ct);
                if (r.Outcome != ExternalOutcome.Ok || r.Valor?.Estado is not (DeliveryState.Delivered or DeliveryState.Failed)) continue;

                var registrado = await sender.Send(new RegisterDebtorMessageAttemptCommand(mensaje.PublicId,
                    await cola.SiguienteIntentoAsync(id, ct), PasoDeDespacho.Entrega)
                {
                    Outcome = r.Outcome,
                    Entrega = r.Valor.Estado,
                    ReferenciaExterna = ultimo.ProviderReference,
                    CodigoDelProveedor = r.CodigoDelProveedor,
                    Detalle = r.Valor.Motivo,
                }, ct);
                if (registrado.IsFailure)
                    logger.LogWarning("[Cartera.Mensajes] La entrega del mensaje {Mensaje} no se registró: {Codigo}", mensaje.PublicId, registrado.Error.Code);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "[Cartera.Mensajes] No se pudo consultar la entrega del mensaje al deudor {Id}; los demás siguen.", id);
            }
        }
    }
}
