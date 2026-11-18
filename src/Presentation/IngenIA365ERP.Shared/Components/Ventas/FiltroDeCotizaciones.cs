using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Components.Ventas;

/// <summary>
/// 2026-11-18: lo que se filtra en una lista de cotizaciones (<see cref="FiltrosDeCotizaciones"/>) y cómo se traduce al filtro de la
/// API de ventas. El estado es un texto: «» todas, «borrador», «confirmada» (vigentes y vencidas), «vigente», «vencida», «anulada».
/// </summary>
public sealed class FiltroDeCotizaciones
{
    public const string Todas = "", Borrador = "borrador", Confirmada = "confirmada", Vigente = "vigente", Vencida = "vencida", Anulada = "anulada";

    public long? Numero { get; set; }
    public string? Cliente { get; set; }
    public string? Usuario { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public string Estado { get; set; } = Todas;

    private string _estadoInicial = Todas;

    public FiltroDeCotizaciones() { }

    public FiltroDeCotizaciones(string estado) { Estado = estado; _estadoInicial = estado; }

    /// <summary>Hay algo más que el estado de por defecto.</summary>
    public bool HayCriterio => Numero is not null || !string.IsNullOrWhiteSpace(Cliente) || !string.IsNullOrWhiteSpace(Usuario)
        || Desde is not null || Hasta is not null || Estado != _estadoInicial;

    public void Limpiar(string estado)
    {
        Numero = null; Cliente = null; Usuario = null; Desde = null; Hasta = null;
        Estado = estado;
        _estadoInicial = estado;
    }

    /// <summary>El filtro de la API para la página <paramref name="pagina"/>; con <paramref name="persona"/>, sólo las de ese cliente.</summary>
    public FiltroDeVentas ParaLaApi(int pagina, int tamano, Guid? persona = null)
    {
        var f = new FiltroDeVentas
        {
            Class = TextosDeVentas.ClaseCotizacion, Page = pagina, PageSize = tamano, Number = Numero, From = Desde, To = Hasta,
            Person = persona, Customer = persona is null ? Cliente : null, CreatedBy = Usuario,
        };
        switch (Estado)
        {
            case Borrador: f.Status = TextosDeVentas.EstadoBorrador; break;
            case Confirmada: f.Status = TextosDeVentas.EstadoConfirmado; break;
            case Vigente: f.Validity = "Valid"; break;
            case Vencida: f.Validity = "Expired"; break;
            case Anulada: f.Status = TextosDeVentas.EstadoAnulado; break;
        }
        return f;
    }
}
