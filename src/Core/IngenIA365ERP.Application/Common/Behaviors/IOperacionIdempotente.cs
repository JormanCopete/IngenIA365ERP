namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Marca un comando que se ejecuta una sola vez por clave (feature 012, decisiones-transversales T13;
/// contracts/api.md §2.3). La ruta copia la cabecera <c>Idempotency-Key</c> a <see cref="OperationKey"/>
/// (<c>.ConClaveDeOperacion()</c>) y <see cref="IdempotencyBehavior{TRequest, TResponse}"/> hace el resto:
/// la primera vez ejecuta y guarda el resultado en <c>COR_OperationKeys</c> en la misma transacción; una
/// repetición devuelve ese resultado sin volver a ejecutar; la misma clave con otro contenido es
/// <c>Operation.KeyReused</c>.
///
/// <para>
/// Lo llevan todos los comandos con ruta de <c>Application/Inventory</c>, <c>Application/ElectronicInvoicing</c>,
/// <c>Application/Core/{Taxes,PaymentMeans}</c> y los de plataforma invocados desde la pantalla
/// (<c>LosComandosDeInventarioLlevanClave</c>). Los mensajes no: su <c>MessageId</c> y el recibo único del
/// destino bastan.
/// </para>
/// </summary>
public interface IOperacionIdempotente
{
    /// <summary>El UUID que generó el cliente al iniciar la operación; <see cref="Guid.Empty"/> = no vino.</summary>
    Guid OperationKey { get; }
}
