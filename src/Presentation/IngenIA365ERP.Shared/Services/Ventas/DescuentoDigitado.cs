namespace IngenIA365ERP.Shared.Services.Ventas;

/// <summary>
/// El descuento del diálogo F7 del punto de venta (2026-11-13): un porcentaje (0–100) <b>o</b> un valor en pesos, nunca los dos —el servidor
/// rechaza los dos juntos con <c>Inventory.Discount.PercentAndAmount</c>—. El último que se escribe manda y borra el otro, así lo que se ve es
/// lo que se manda; hasta esa fecha, con los dos escritos, se mandaba el porcentaje aunque el último fuera el valor. (nuevo)
/// </summary>
public sealed class DescuentoDigitado
{
    private decimal? _porcentaje, _valor;

    /// <summary>El porcentaje, de 0 a 100. Escribirlo borra el valor.</summary>
    public decimal? Porcentaje
    {
        get => _porcentaje;
        set
        {
            _porcentaje = value;
            if (value is not null) _valor = null;
        }
    }

    /// <summary>El valor en pesos. Escribirlo borra el porcentaje.</summary>
    public decimal? Valor
    {
        get => _valor;
        set
        {
            _valor = value;
            if (value is not null) _porcentaje = null;
        }
    }

    /// <summary>Deja el diálogo en blanco.</summary>
    public void Limpiar() => (_porcentaje, _valor) = (null, null);

    /// <summary>Lo que se manda: el porcentaje como fracción (5 → 0,05) o el valor; nulo si no hay ninguno mayor que cero.</summary>
    public DescuentoRequest? Pedido() =>
        _porcentaje is > 0m ? new DescuentoRequest(Percent: _porcentaje / 100m)
        : _valor is > 0m ? new DescuentoRequest(Amount: _valor)
        : null;
}
