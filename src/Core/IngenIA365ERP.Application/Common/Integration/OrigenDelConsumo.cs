using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// El usuario de origen del mensaje que se está consumiendo, para lo que el consumo emita por módulos que no reciben la solicitud (2026-11-23, cruce
/// automático de anticipos). T11 dice que, si actúa el proceso, el mensaje lo origina «el del mensaje consumido»: Cartera lo pasa a mano en sus emisiones
/// (<see cref="SolicitudDeEmision.UsuarioDeOrigen"/>), pero cuando un consumidor de Recaudo aplica por el contrato de obligaciones, el <c>PagoAplicado</c> lo
/// emite Cartera por dentro y no lo sabe. <see cref="EmisorDeMensajes"/> lo toma de aquí cuando la solicitud no trae uno y actúa el proceso.
///
/// <para>
/// Es un <see cref="AsyncLocal{T}"/>, como <c>ContextoAmbiental</c>: fluye por los <c>await</c> del consumo y no se filtra a los flujos que empezaron antes.
/// Sólo lo fija el consumidor que lo necesita, con <see cref="Usar"/>, y lo restaura al terminar. Es un <b>dato</b>, nunca un actor: el actor sigue siendo el
/// proceso.
/// </para>
/// </summary>
public static class OrigenDelConsumo
{
    private static readonly AsyncLocal<UserRefV1?> Actual_ = new();

    /// <summary>El usuario de origen del consumo en curso; nulo fuera de un consumo que lo fijó.</summary>
    public static UserRefV1? Actual => Actual_.Value;

    /// <summary>Fija el usuario de origen para este flujo (sólo si tiene identidad central) y lo restaura al liberar el resultado.</summary>
    public static IDisposable Usar(UserRefV1? usuario)
    {
        var anterior = Actual_.Value;
        Actual_.Value = usuario is { CentralUserId: not null } ? usuario : null;
        return new Restaurar(anterior);
    }

    private sealed class Restaurar(UserRefV1? anterior) : IDisposable
    {
        private bool _hecho;

        public void Dispose()
        {
            if (_hecho) return;
            _hecho = true;
            Actual_.Value = anterior;
        }
    }
}
