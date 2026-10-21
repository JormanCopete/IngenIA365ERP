using IngenIA365ERP.Domain.Entities.Integration;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Lo que cada módulo pone en la bandeja de mensajes de la plataforma (feature 013, T0075; T19, PM-15; R14): qué entregas ve quien
/// pregunta (sus mensajes y su alcance), cómo se llaman sus tipos (de documento en Inventario, de hecho en Cartera), cuáles siguen
/// sin pasar a Contabilidad a hoy y adónde lleva un resultado resumido. La bandeja (<see cref="ListIntegrationMessagesQuery"/>,
/// <see cref="GetIntegrationMessageQuery"/>) y el envío posterior (<see cref="SendNotApplicableMessagesCommand"/>) son de la
/// plataforma y no conocen ningún módulo: piden la vista del suyo por <see cref="Modulo"/>. Inventario la implementa con lo de la
/// 012 (<c>VistaDeBandejaDeInventario</c>, alcance de bodega); Cartera, en la fase de US7, con alcance por oficina. (nuevo)
/// </summary>
public interface IVistaDeBandejaDelModulo
{
    /// <summary>El módulo de la bandeja (<see cref="ModulosDeOrigen"/>).</summary>
    string Modulo { get; }

    /// <summary>
    /// Las entregas que la bandeja del módulo muestra a quien pregunta: las de sus mensajes (y las que le llegan) dentro de su
    /// alcance. Fuera del alcance es lo mismo que inexistente.
    /// </summary>
    Task<IQueryable<IntegrationMessageDelivery>> VisiblesAsync(CancellationToken ct);

    /// <summary>Los códigos de tipo de los <paramref name="tipoPublicIds"/> que la pantalla elige como filtro del envío posterior.</summary>
    Task<IReadOnlyList<string>> CodigosDeTipoAsync(IReadOnlyCollection<Guid> tipoPublicIds, CancellationToken ct);

    /// <summary>Las entregas de esos códigos de tipo.</summary>
    IQueryable<IntegrationMessageDelivery> DeLosTipos(IQueryable<IntegrationMessageDelivery> entregas, IReadOnlyCollection<string> codigos);

    /// <summary>El código de tipo de un mensaje, como lo llama el modo de paso del módulo (nulo si no tiene).</summary>
    string? CodigoDeTipo(string? originDocumentTypeCode, string type);

    /// <summary>De esos códigos, los que a <paramref name="hoy"/> siguen con el modo de paso «no pasa».</summary>
    Task<IReadOnlyList<string>> TiposQueSiguenSinPasoAsync(IReadOnlyCollection<string> codigos, DateOnly hoy, CancellationToken ct);

    /// <summary>La ruta de la pantalla del lote de un resultado resumido del módulo.</summary>
    string RutaDelLoteResumido(Guid batchPublicId);
}

/// <summary>Elige la vista de un módulo entre las registradas. (nuevo)</summary>
public static class VistasDeBandeja
{
    /// <exception cref="InvalidOperationException">Si el módulo no registró su vista: es un defecto del despliegue.</exception>
    public static IVistaDeBandejaDelModulo Del(IEnumerable<IVistaDeBandejaDelModulo> vistas, string modulo) =>
        vistas.FirstOrDefault(v => string.Equals(v.Modulo, modulo, StringComparison.Ordinal))
        ?? throw new InvalidOperationException($"El módulo {modulo} no registró su vista de la bandeja de mensajes (IVistaDeBandejaDelModulo).");
}
