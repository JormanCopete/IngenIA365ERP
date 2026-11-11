namespace IngenIA365ERP.Domain.Transport;

/// <summary>Un cargo abierto que puede recibir parte de un pago: su prioridad (la del servicio), su orden y lo que le falta.</summary>
/// <param name="Orden">El orden de la línea en la factura (o el de creación del cargo): desempata la prioridad.</param>
public sealed record CargoPorRepartir(int Id, int Prioridad, int Orden, decimal Saldo);

/// <summary>Lo que recibe un cargo de un reparto.</summary>
public sealed record ParteDelReparto(int CargoId, decimal Importe);

/// <summary>
/// El reparto de un importe entre los cargos abiertos de una factura (feature 020, R13; FR-056): primero el servicio de menor número de
/// prioridad, y entre iguales el de la línea anterior; ninguno recibe más que su saldo, y lo que sobra se devuelve. Pura: sin base ni reloj.
/// Lo usa Transporte cuando un recibo comercial paga la factura de servicios sin decir a qué línea (la ruta sin Cartera).
/// </summary>
public static class RepartoPorPrioridad
{
    public static (IReadOnlyList<ParteDelReparto> Partes, decimal Sobrante) Repartir(decimal importe, IEnumerable<CargoPorRepartir> cargos)
    {
        ArgumentNullException.ThrowIfNull(cargos);
        if (importe <= 0m) return ([], Math.Max(importe, 0m));

        var partes = new List<ParteDelReparto>();
        var restante = importe;
        foreach (var c in cargos.Where(c => c.Saldo > 0m).OrderBy(c => c.Prioridad).ThenBy(c => c.Orden).ThenBy(c => c.Id))
        {
            if (restante <= 0m) break;
            var parte = Math.Min(restante, c.Saldo);
            partes.Add(new ParteDelReparto(c.Id, parte));
            restante -= parte;
        }
        return (partes, restante);
    }
}
