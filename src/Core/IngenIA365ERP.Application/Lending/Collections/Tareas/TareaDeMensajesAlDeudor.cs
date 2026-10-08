using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Messaging;
using IngenIA365ERP.Application.Lending.Adapters;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Collections.Tareas;

/// <summary>
/// La tarea programada <c>cartera.mensajes</c> (feature 013, T0307; T75, F-06; DT §2.13; contracts/adaptadores-y-archivos.md §4.6): cada
/// minuto, en cada cooperativa, por <see cref="IEjecutorEnCooperativa"/> (el <c>ProgramadorDeTareas</c> la corre con el actor «Proceso de
/// integración» y el arrendamiento <c>scheduled.tasks</c>). Sale si Cartera no está activada. Por cada uno de hasta
/// <see cref="TamanoDeTanda"/> mensajes vencidos, en orden de <c>RequestedFor</c>:
/// <list type="number">
/// <item>un mensaje de cobranza cuyo crédito ya no tiene saldo vencido se cancela («el crédito se puso al día»);</item>
/// <item>vuelve a evaluar la ventana legal: diferido o excluido, así queda;</item>
/// <item>lee el destino del maestro <b>ahora</b> (C57) y pregunta a la guardia por <c>RealDelivery</c>: el simulado en producción no sale y
/// queda <c>Simulated</c>;</item>
/// <item>llama <c>EnviarAsync</c> del adaptador <b>sellado</b> (<see cref="ICanalesDeMensajeria"/>; el correo de la plataforma y <c>SIMULADO</c>
/// en K1, SMS y WhatsApp reales con K4) y registra el resultado por <see cref="RegisterDebtorMessageAttemptCommand"/>.</item>
/// </list>
/// Escribe sólo por comandos. Un mensaje que falla de forma inesperada se anota en el log y no detiene a los demás. Singleton. (nuevo)
/// </summary>
public sealed class TareaDeMensajesAlDeudor : ITareaProgramada
{
    public const string NombreDeLaTarea = "cartera.mensajes";

    /// <summary>Cuántos mensajes vencidos toma cada pasada (§4.6).</summary>
    public const int TamanoDeTanda = 200;

    /// <summary>Cada minuto.</summary>
    public static readonly TimeSpan Cadencia = TimeSpan.FromMinutes(1);

    /// <summary>Cuánto espera un canal caído antes de reintentar (desde ahí se busca la ventana permitida siguiente).</summary>
    public static readonly TimeSpan EsperaTrasCanalCaido = TimeSpan.FromMinutes(5);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) =>
        ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Cadencia;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var cola = servicios.GetRequiredService<ColaDeMensajesAlDeudor>();
        if (!await cola.CarteraActivadaAsync(ct)) return;

        var logger = servicios.GetService<ILogger<TareaDeMensajesAlDeudor>>() ?? NullLogger<TareaDeMensajesAlDeudor>.Instance;
        var despacho = new Despacho(servicios, cola);
        foreach (var id in await cola.VencidosAsync(TamanoDeTanda, ct))
        {
            try
            {
                await despacho.DespacharAsync(id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "[Cartera.Mensajes] No se pudo despachar el mensaje al deudor {Id}; los demás siguen.", id);
            }
        }
    }

    /// <summary>El despacho de un mensaje con los servicios del ámbito de la cooperativa. (nuevo)</summary>
    private sealed class Despacho(IServiceProvider servicios, ColaDeMensajesAlDeudor cola)
    {
        private readonly IApplicationDbContext _db = servicios.GetRequiredService<IApplicationDbContext>();
        private readonly ISender _sender = servicios.GetRequiredService<ISender>();
        private readonly ICanalesDeMensajeria _canales = servicios.GetRequiredService<ICanalesDeMensajeria>();
        private readonly GuardiaDeCanalesExternos _guardia = servicios.GetRequiredService<GuardiaDeCanalesExternos>();
        private readonly IDateTimeService _reloj = servicios.GetRequiredService<IDateTimeService>();

        public async Task DespacharAsync(int id, CancellationToken ct)
        {
            var mensaje = await _db.DebtorMessages.AsNoTracking().FirstAsync(m => m.Id == id, ct);
            var intento = await cola.SiguienteIntentoAsync(mensaje.Id, ct);

            // 2. La causa desapareció: un mensaje de cobranza de un crédito al día no sale.
            if (VentanaLegalDeContacto.EsDeCobranza(mensaje.Purpose) && mensaje.LoanId is { } loanId && await cola.CreditoAlDiaAsync(loanId, ct))
            {
                await RegistrarAsync(new RegisterDebtorMessageAttemptCommand(mensaje.PublicId, intento, PasoDeDespacho.Cancelado)
                {
                    CodigoDelProveedor = RegisterDebtorMessageAttemptCommandHandler.MotivoCreditoAlDia,
                    Detalle = RegisterDebtorMessageAttemptCommandHandler.TextoCreditoAlDia,
                }, ct);
                return;
            }

            // La ventana otra vez: un contacto, una revocación o un pago registrados después cambian la respuesta.
            var ventana = await cola.EvaluarParaDespachoAsync(mensaje, ct);
            if (ventana.IsFailure) throw new InvalidOperationException($"{ventana.Error.Code}: {ventana.Error.Message}");
            switch (ventana.Value.Decision)
            {
                case Diferido d:
                    await RegistrarAsync(new RegisterDebtorMessageAttemptCommand(mensaje.PublicId, intento, PasoDeDespacho.Diferido)
                    {
                        SiguienteIntento = ventana.Value.PermitidoDesde!.Value.UtcDateTime, Motivos = d.Motivos,
                    }, ct);
                    return;
                case Excluido x:
                    await RegistrarAsync(new RegisterDebtorMessageAttemptCommand(mensaje.PublicId, intento, PasoDeDespacho.Excluido)
                    {
                        Motivos = [x.Motivo],
                    }, ct);
                    return;
            }

            // 3. El destino se lee del maestro al despachar (C57).
            var persona = await _db.People.AsNoTracking().FirstAsync(p => p.Id == mensaje.PersonId, ct);
            var destino = ColaDeMensajesAlDeudor.DestinoDe(persona, mensaje.Channel);
            if (destino is null)
            {
                await RegistrarAsync(new RegisterDebtorMessageAttemptCommand(mensaje.PublicId, intento, PasoDeDespacho.Excluido)
                {
                    Motivos = [ContactWindowReason.NoDestination],
                }, ct);
                return;
            }

            var veredicto = _guardia.Evaluar(EfectoLegalDeCanal.RealDelivery, mensaje.ChannelCode, simulado: false);
            if (!veredicto.TieneEfecto)
            {
                await RegistrarAsync(new RegisterDebtorMessageAttemptCommand(mensaje.PublicId, intento, PasoDeDespacho.Envio)
                {
                    Outcome = ExternalOutcome.Ok, Simulado = true, Destino = destino, Detalle = veredicto.Motivo,
                }, ct);
                return;
            }

            // 4. El adaptador sellado.
            var resultado = await EnviarAsync(mensaje, persona.TaxId, destino, ct);
            var comando = new RegisterDebtorMessageAttemptCommand(mensaje.PublicId, intento, PasoDeDespacho.Envio)
            {
                Outcome = resultado.Outcome,
                Simulado = resultado.Simulado,
                Destino = destino,
                ReferenciaExterna = resultado.Valor?.ReferenciaExterna ?? resultado.ReferenciaExterna,
                CodigoDelProveedor = resultado.CodigoDelProveedor,
                Detalle = resultado.Detalle,
            };
            if (resultado.Outcome == ExternalOutcome.ChannelUnavailable)
                comando = comando with { SiguienteIntento = await cola.SiguienteVentanaAsync(mensaje, _reloj.UtcNow + EsperaTrasCanalCaido, ct) };
            await RegistrarAsync(comando, ct);
        }

        private async Task<ResultadoExterno<EnvioDeMensaje>> EnviarAsync(DebtorMessage mensaje, string documento, string destino, CancellationToken ct)
        {
            var canal = _canales.Resolver(mensaje.ChannelCode);
            var contexto = await ContextoDeEnvio.ArmarAsync(servicios, mensaje.ChannelCode, mensaje.PublicId, ct);
            if (contexto is null)
                return ResultadoExterno<EnvioDeMensaje>.Sin(ExternalOutcome.ChannelUnavailable, mensaje.ChannelCode,
                    CodigosDeCanalExterno.EsSimulado(mensaje.ChannelCode), "No se pudo resolver la credencial del canal.");

            var proveedor = mensaje.TemplateId is { } plantilla
                ? await _db.NotificationTemplates.AsNoTracking().Where(t => t.Id == plantilla).Select(t => t.ProviderTemplateId).FirstOrDefaultAsync(ct)
                : null;
            return await canal.EnviarAsync(new MensajeSaliente(mensaje.PublicId, destino, mensaje.Subject, mensaje.RenderedBody, proveedor, null, [],
                documento), contexto, ct);
        }

        private async Task RegistrarAsync(RegisterDebtorMessageAttemptCommand comando, CancellationToken ct)
        {
            var r = await _sender.Send(comando, ct);
            if (r.IsFailure) throw new InvalidOperationException($"{r.Error.Code}: {r.Error.Message}");
        }
    }
}

/// <summary>
/// El contexto de una llamada al adaptador de mensajería (§3.1): la cooperativa resuelta, el código sellado, la credencial (el correo de la
/// plataforma no la pide) y el mensaje como clave de idempotencia. Nulo si no se pudo resolver la cooperativa o la credencial: el envío
/// cuenta como «canal no disponible». (nuevo)
/// </summary>
internal static class ContextoDeEnvio
{
    public static async Task<ContextoDeCanalExterno?> ArmarAsync(IServiceProvider servicios, string channelCode, Guid mensajePublicId, CancellationToken ct)
    {
        if (CooperativaDelCanal.Resuelta(servicios.GetRequiredService<ICurrentTenantService>()) is not { } cooperativa) return null;
        var ambienteDePruebas = !servicios.GetRequiredService<ColaDeMensajesAlDeudor>().EnProduccion;

        CredencialDeCanalExterno? credencial = null;
        if (!string.Equals(channelCode, CodigosDeCanalExterno.Correo, StringComparison.OrdinalIgnoreCase))
        {
            var resuelta = await servicios.GetRequiredService<ICredencialesDeCanalExterno>().ResolverAsync(channelCode, ct);
            if (resuelta.IsFailure) return null;
            credencial = resuelta.Value;
        }
        return new ContextoDeCanalExterno(cooperativa, channelCode, null, credencial, mensajePublicId.ToString("N"), ambienteDePruebas);
    }
}
