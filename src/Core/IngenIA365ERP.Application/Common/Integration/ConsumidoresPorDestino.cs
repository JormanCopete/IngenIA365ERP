namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Un solo consumidor por destino (feature 013, T0076; T15; contracts/mensajes.md §13.1): dos <see cref="IDestinoDeMensajes"/> con el
/// mismo <see cref="IDestinoDeMensajes.Destino"/> hacen fallar el arranque con <c>Integration.Destination.DuplicateConsumer</c>. Antes
/// el despachador tomaba el primero sin avisar; así, cuando llegue la Tesorería real con su doble todavía registrado, el despliegue no
/// arranca en vez de repartir los giros entre los dos. Lo llama <c>Program.cs</c> al construir la aplicación y lo fija
/// <c>UnSoloConsumidorPorDestino</c>. (nuevo)
/// </summary>
public static class ConsumidoresPorDestino
{
    /// <summary>Comprueba que ningún destino tenga dos consumidores.</summary>
    /// <exception cref="InvalidOperationException">Con el código <c>Integration.Destination.DuplicateConsumer</c> y el destino.</exception>
    public static void Validar(IEnumerable<IDestinoDeMensajes> consumidores)
    {
        ArgumentNullException.ThrowIfNull(consumidores);
        var repetidos = consumidores
            .GroupBy(c => c.Destino, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => $"{g.Key} ({string.Join(", ", g.Select(c => c.GetType().Name))})")
            .ToList();
        if (repetidos.Count > 0)
        {
            throw new InvalidOperationException(
                $"[{ErroresDeIntegracion.CodigoConsumidorDuplicado}] Hay dos consumidores para el destino {string.Join("; ", repetidos)}: " +
                "cada destino de la bandeja tiene uno solo. Retire el que sobra (un doble de prueba se retira al llegar el real).");
        }
    }
}
