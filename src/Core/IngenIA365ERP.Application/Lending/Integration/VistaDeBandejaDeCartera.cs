using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La bandeja de mensajes vista desde Cartera (feature 013, US7; T0075, T19, PM-15; contabilidad.md §10.1): lo que necesitan
/// <see cref="ListIntegrationMessagesQuery"/>, el reproceso y <see cref="SendNotApplicableMessagesCommand"/> («Enviar lo que no pasaba»,
/// <c>POST /api/lending/messages/send-not-applicable</c>) con <c>Modulo = CAR</c>. Muestra los mensajes de Cartera y los que le llegan (destino
/// <c>Lending</c>), por el alcance de oficinas de quien pregunta (<see cref="IAlcanceDeCartera"/>): la sucursal del sobre es la de la oficina; sin
/// <c>Lending.Scope.AllOffices</c> se ven las de sus oficinas. Los tipos son los de <c>LND_FactTypes</c> (su código es el <c>Type</c> del mensaje)
/// y el modo vigente lo resuelve <see cref="ModoDePasoDeCartera"/> —vigencia del tipo, general o el defecto, y <c>NoPasa</c> sin contabilidad
/// iniciada—. (nuevo)
/// </summary>
public sealed class VistaDeBandejaDeCartera(IApplicationDbContext db, IAlcanceDeCartera alcanceDeCartera, ModoDePasoDeCartera modoDePaso)
    : IVistaDeBandejaDelModulo
{
    public string Modulo => ModulosDeOrigen.Cartera;

    public async Task<IQueryable<IntegrationMessageDelivery>> VisiblesAsync(CancellationToken ct)
    {
        var alcance = await alcanceDeCartera.ObtenerAsync(ct);
        var entregas = db.IntegrationMessageDeliveries.AsNoTracking()
            .Where(d => d.Message!.OriginModule == ModulosDeOrigen.Cartera || d.Destination == IntegrationDestinations.Lending);
        if (alcance.TodasLasOficinas) return entregas;
        var oficinas = alcance.Oficinas.ToList();
        var sucursales = db.Branches.AsNoTracking().Where(b => oficinas.Contains(b.Id)).Select(b => b.PublicId);
        return entregas.Where(d => sucursales.Contains(d.Message!.BranchPublicId));
    }

    public async Task<IReadOnlyList<string>> CodigosDeTipoAsync(IReadOnlyCollection<Guid> tipoPublicIds, CancellationToken ct)
    {
        var publicos = tipoPublicIds.ToList();
        return await db.LendingFactTypes.AsNoTracking().Where(t => publicos.Contains(t.PublicId) && !t.IsDeleted).Select(t => t.Code).ToListAsync(ct);
    }

    public IQueryable<IntegrationMessageDelivery> DeLosTipos(IQueryable<IntegrationMessageDelivery> entregas, IReadOnlyCollection<string> codigos)
    {
        var lista = codigos.ToList();
        return entregas.Where(d => lista.Contains(d.Message!.Type));
    }

    public string? CodigoDeTipo(string? originDocumentTypeCode, string type) => type;

    public async Task<IReadOnlyList<string>> TiposQueSiguenSinPasoAsync(IReadOnlyCollection<string> codigos, DateOnly hoy, CancellationToken ct)
    {
        var deCartera = CatalogoDeMensajes.DelModulo(ModulosDeOrigen.Cartera).Select(t => t.Type).ToHashSet(StringComparer.Ordinal);
        var sinPaso = new List<string>();
        foreach (var codigo in codigos.Where(deCartera.Contains).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var modo = await modoDePaso.ResolverAsync(codigo, hoy, ct);
            if (modo.IsSuccess && modo.Value is ModoDeEntrega.Sellado { Modo: DeliveryMode.NotPosted }) sinPaso.Add(codigo);
        }
        return sinPaso;
    }

    public string RutaDelLoteResumido(Guid batchPublicId) => $"/contabilidad/cartera/lotes/{batchPublicId:D}";
}
