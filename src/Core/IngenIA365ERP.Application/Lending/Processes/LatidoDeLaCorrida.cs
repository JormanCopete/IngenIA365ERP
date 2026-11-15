using IngenIA365ERP.Application.Common.Interfaces;

namespace IngenIA365ERP.Application.Lending.Processes;

/// <summary>
/// El latido de la corrida de Cartera que se está conduciendo (2026-11-15). El conductor renueva <c>lending.process</c> entre tandas, pero una sola
/// tanda puede durar más que el arrendamiento: el cierre del ciclo de COOFLOPAL del 13-nov tardó 11 minutos, el arrendamiento (dos minutos) venció,
/// la otra réplica de la API tomó la misma corrida <c>Running</c>, las dos chocaron por su <c>RowVersion</c> y la corrida quedó <c>Failed</c>, que no
/// deja nacer el ciclo siguiente. Los pasos largos llaman a <see cref="LatirAsync"/> entre sus piezas; el conductor lo enciende con la renovación del
/// arrendamiento al tomar una corrida y lo apaga al soltarla. Apagado (la orden manual, las pruebas que no lo encienden) no hace nada. Renueva a lo
/// sumo una vez por período; si la renovación dice que el arrendamiento ya es de otra réplica, lanza <see cref="TurnoDeCarteraPerdidoException"/>: la
/// otra réplica sigue la corrida y ésta se retira sin marcarla. (nuevo)
/// </summary>
public sealed class LatidoDeLaCorrida(IDateTimeService reloj)
{
    private Func<CancellationToken, Task<bool>>? _renovar;
    private TimeSpan _cada;
    private DateTime _ultimo;

    /// <summary>¿Está encendido?</summary>
    public bool Encendido => _renovar is not null;

    /// <summary>Lo enciende con la renovación del arrendamiento, a lo sumo una vez cada <paramref name="cada"/>.</summary>
    public void Encender(Func<CancellationToken, Task<bool>> renovar, TimeSpan cada)
    {
        ArgumentNullException.ThrowIfNull(renovar);
        _renovar = renovar;
        _cada = cada;
        _ultimo = reloj.UtcNow;
    }

    public void Apagar() => _renovar = null;

    /// <summary>Renueva el arrendamiento si pasó el período desde la última vez; sin él, lanza <see cref="TurnoDeCarteraPerdidoException"/>.</summary>
    public async Task LatirAsync(CancellationToken ct)
    {
        if (_renovar is not { } renovar) return;
        var ahora = reloj.UtcNow;
        if (ahora - _ultimo < _cada) return;
        _ultimo = ahora;
        if (!await renovar(ct)) throw new TurnoDeCarteraPerdidoException();
    }
}

/// <summary>
/// La réplica perdió el arrendamiento <c>lending.process</c> a mitad de una corrida: otra la tomó y la sigue. El conductor descarta lo suyo y se
/// retira sin marcar la corrida <c>Failed</c>. (nuevo)
/// </summary>
public sealed class TurnoDeCarteraPerdidoException()
    : Exception("Otra réplica tomó el arrendamiento lending.process: ella sigue la corrida.");
