namespace IngenIA365ERP.Application.Common.Interfaces;

/// <summary>
/// Qué base lógica de Redis le corresponde a cada cooperativa.
///
/// <para>
/// El índice se <b>guarda</b> en <c>ADM_Tenants.RedisDbIndex</c>, nunca se
/// deriva por hash del identificador. Con un número limitado de ranuras el
/// principio del palomar garantiza colisiones, y una colisión aquí significa que
/// una cooperativa lee el caché de permisos de otra: exactamente el fallo que el
/// Principio IV llama catastrófico.
/// </para>
///
/// <para>
/// La ranura 0 queda reservada para lo global y no se asigna a nadie. Ahí viven
/// las claves que no pertenecen a ninguna cooperativa por naturaleza, y eso es
/// una decisión de seguridad, no de comodidad: el contador de intentos de acceso
/// cuenta por correo <i>antes</i> de elegir cooperativa —si contara por
/// cooperativa, el bloqueo se esquivaría cambiando de una a otra—; un token
/// revocado tiene que estarlo en todas partes; los almacenes del segundo factor
/// operan entre el acceso y la verificación, cuando todavía no hay cooperativa;
/// la lista de membresías de una persona <i>es</i> el conjunto de sus
/// cooperativas y no cabe dentro de ninguna; y los bloqueos distribuidos
/// protegen filas de la base administrativa, así que un bloqueo en otra base
/// lógica no excluiría nada.
/// </para>
/// </summary>
public interface ITenantCacheSlots
{
    /// <param name="tenantId">Id interno de la cooperativa, como cadena.</param>
    /// <returns>
    /// La ranura de la cooperativa, o <c>0</c> si no se puede resolver. Devolver
    /// 0 es deliberado: la clave ya lleva la cooperativa como prefijo, así que el
    /// peor caso es compartir base con lo global, no leer datos ajenos.
    /// </returns>
    ValueTask<int> RanuraDeAsync(string? tenantId, CancellationToken ct);
}

/// <summary>
/// Reserva la ranura de una cooperativa nueva. Falla si no quedan, en vez de
/// entregar una repetida: una colisión de caché entre cooperativas no da error,
/// da los permisos de otra.
/// </summary>
public interface ITenantCacheSlotAllocator
{
    Task<int> ReservarAsync(string nombreParaElMensaje, CancellationToken ct);
}

/// <summary>
/// Cuántas bases lógicas admite el Redis que hay detrás. Lo pregunta el
/// aprovisionamiento antes de asignar una ranura nueva.
///
/// <para>
/// Se lee del servidor y no de la configuración de la aplicación: el parámetro
/// <c>databases</c> de Redis es inmutable en caliente y se fija al arrancar el
/// proceso, así que lo que crea la aplicación y lo que el servidor tiene pueden
/// no coincidir. Preguntar evita descubrirlo cuando una cooperativa ya está
/// registrada y sin caché.
/// </para>
/// </summary>
public interface ICacheSlotCapacity
{
    /// <returns>Número de bases lógicas, incluida la 0. Null si no se pudo consultar.</returns>
    ValueTask<int?> RanurasDisponiblesAsync(CancellationToken ct);
}
