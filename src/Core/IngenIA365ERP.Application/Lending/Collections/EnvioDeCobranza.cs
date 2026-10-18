using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Collections;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>
/// El envío de los mensajes de cobranza que motiva el sistema —el de la etapa, el aviso previo, el aviso del gasto— por el primer canal que la ley
/// y el titular permiten (feature 013, K4, T0943, T0947, T0950; FR-122): correo, SMS y WhatsApp en ese orden; el canal se salta si la ventana lo
/// <b>excluye</b> (sin autorización, sin evidencia, sin destino) o si su plantilla no sirve (no hay vigente, o la de WhatsApp no tiene la del proveedor). Si ninguno sirve, el mensaje se encola por
/// el primero que tenga plantilla y queda <c>Excluded</c> con su motivo: así queda constancia de por qué no salió. Diferir no es excluir: un canal
/// autorizado que hoy no puede salir se usa y el mensaje queda <c>Deferred</c>. Siempre por <see cref="ColaDeMensajesAlDeudor"/>, sin guardar. (nuevo)
/// </summary>
public static class EnvioDeCobranza
{
    /// <summary>El orden en que se prueban los canales.</summary>
    public static IReadOnlyList<MessageChannel> Canales { get; } = [MessageChannel.Email, MessageChannel.Sms, MessageChannel.WhatsApp];

    public static async Task<Result<MensajeEncolado>> EncolarAsync(ColaDeMensajesAlDeudor cola, Func<MessageChannel, PedidoDeMensajeAlDeudor> pedido,
        DateTime ahoraUtc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cola);
        ArgumentNullException.ThrowIfNull(pedido);
        Error? primerError = null;
        MessageChannel? conPlantilla = null;
        foreach (var canal in Canales)
        {
            var p = pedido(canal);
            var ventana = await cola.EvaluarAsync(new ConsultaDeVentana(p.PersonId, p.LoanId, canal, p.Purpose, p.ProgramadoPara ?? ahoraUtc, false), ct);
            if (ventana.IsFailure) return Result.Failure<MensajeEncolado>(ventana.Error);
            var excluido = ventana.Value.Decision is Excluido;
            if (excluido && conPlantilla is not null) continue;
            if (excluido)
            {
                // Se recuerda el primer canal con plantilla, para dejar la constancia si ninguno sirve.
                if (await TienePlantillaAsync(cola, p, ct)) conPlantilla = canal;
                continue;
            }
            var r = await cola.EncolarAsync(p, ct);
            if (r.IsSuccess) return r;
            primerError ??= r.Error;
            // Sin Cartera activada no sale nada; cualquier otro problema del canal (sin plantilla, WhatsApp sin la plantilla del proveedor) prueba el siguiente.
            if (r.Error.Code == ErroresDeMensajesAlDeudor.CodigoCarteraSinActivar) return r;
        }
        if (conPlantilla is { } constancia) return await cola.EncolarAsync(pedido(constancia), ct);
        return Result.Failure<MensajeEncolado>(primerError ?? ErroresDeMensajesAlDeudor.PlantillaInexistente(pedido(Canales[0]).TemplateCode, null));
    }

    private static Task<bool> TienePlantillaAsync(ColaDeMensajesAlDeudor cola, PedidoDeMensajeAlDeudor p, CancellationToken ct) =>
        cola.HayPlantillaAsync(p.TemplateCode, p.Channel, p.Purpose, ct);
}
