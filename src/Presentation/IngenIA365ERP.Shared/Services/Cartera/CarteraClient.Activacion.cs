using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// La activación de Cartera (feature 013, US8, T0758; contracts/api.md §19.3), con <c>Lending.Activation.Execute</c>: el estado con sus requisitos y
/// avisos, la plantilla y la revisión previa de los cobros provisionales (deja el adjunto que se pasa a la activación; no guarda filas) y la
/// activación misma, con el punto del acta y la clave de la operación. (nuevo)
/// </summary>
public sealed partial class CarteraClient
{
    public const string RutaDeActivacion = GobiernoDeCarteraClient.Base + "/activation";

    public Task<ResultadoDeInventario<EstadoDeActivacionDto>> EstadoDeActivacionAsync(CancellationToken ct = default) =>
        Enviar<EstadoDeActivacionDto>(HttpMethod.Get, RutaDeActivacion, null, null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarPlantillaDeCobrosAsync(CancellationToken ct = default) =>
        Descargar($"{RutaDeActivacion}/provisional-collections/template.xlsx", "plantilla-cartera-cobros-provisionales.xlsx", ct);

    /// <summary>
    /// Revisa el archivo de cobros provisionales contra las ventas confirmadas después del corte. Con errores responde 422 <c>Import.Invalid</c>
    /// (la revisión en <c>data</c>) o <c>Lending.ProvisionalCollection.ExceedsSale</c> y no deja nada.
    /// </summary>
    public Task<ResultadoDeInventario<RevisionDeCobrosProvisionalesDto>> RevisarCobrosProvisionalesAsync(string archivo, byte[] contenido, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        SubirAsync<RevisionDeCobrosProvisionalesDto>($"{RutaDeActivacion}/provisional-collections/review", archivo, contenido, clave, ct);

    /// <summary>Activa con fecha = corte + 1 y el punto del acta; si algo falla no queda nada (todo o nada).</summary>
    public Task<ResultadoDeInventario<CarteraActivadaDto>> ActivarAsync(ActivarCarteraRequest request, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CarteraActivadaDto>(HttpMethod.Post, RutaDeActivacion, request, clave, ct);
}
