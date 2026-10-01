namespace IngenIA365ERP.Application.Common.Behaviors;

/// <summary>
/// Marca un comando que se hace desde una caja del punto de venta (feature 012, §2.16, T36): la auditoría
/// registra el canal <c>pos</c> en lugar del que traiga la cabecera <c>X-Canal</c>, y la sesión de caja
/// queda en el evento.
/// </summary>
public interface IOperacionDePuntoDeVenta
{
    /// <summary>La sesión de caja abierta en la que se hace la operación.</summary>
    Guid CashSessionPublicId { get; }
}
