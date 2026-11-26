using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Common.Text;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Vehicles;

/// <summary>Cómo se filtra el listado de vehículos por servicio (2026-11-25, pedido del dueño).</summary>
public enum VehicleServiceFilter
{
    /// <summary>Suscrito al servicio indicado.</summary>
    Subscribed = 1,

    /// <summary>No suscrito al servicio indicado (puede tener otros).</summary>
    NotSubscribed = 2,

    /// <summary>Sin ningún servicio.</summary>
    None = 3,

    /// <summary>Con algún servicio.</summary>
    Any = 4,
}

/// <summary>Los filtros del listado de vehículos, iguales en pantalla y en la exportación (FR-012).</summary>
public sealed record FiltrosDeVehiculos(
    string? Q = null,
    VehicleStatus? Status = null,
    Guid? OwnerPersonPublicId = null,
    AffiliationClass? Affiliation = null,
    Guid? ServicePublicId = null,
    VehicleServiceFilter? ServiceFilter = null);

/// <summary>
/// Un solo sitio para filtrar el parque (feature 020, FR-012; 2026-11-25): lo usan el listado paginado y el informe de vehículos que
/// exporta lo filtrado, así lo que se ve y lo que se descarga es lo mismo. Todo se traduce a SQL —también la vinculación, que es deducida
/// (<see cref="AfiliacionVigente.VigenteEn"/>), y el servicio— para que el listado pagine en el servidor.
///
/// <para>
/// <b>Búsqueda</b>: placa (contiene, sin guiones ni espacios), número interno o radio-móvil (exactos) y el propietario: con letras, todos los
/// términos en su texto de búsqueda (nombres, apellidos, razón social, en cualquier orden y sin tildes); sólo con dígitos, su documento
/// (exacto hasta 4 dígitos, para que un lateral corto no traiga a todos los documentos que lo contienen; desde 5, el que empieza así).
/// </para>
/// <para>
/// <b>Servicio</b>: cuenta una suscripción viva que no haya terminado antes de hoy, aunque empiece después: «sin ningún servicio» no
/// debe ofrecer para suscribir un vehículo que ya quedó suscrito desde el mes que viene.
/// </para>
/// </summary>
public static class FiltroDeVehiculos
{
    /// <summary>Desde cuántos dígitos el documento se busca por comienzo y no exacto.</summary>
    public const int DigitosParaBuscarPorComienzo = 5;

    public static IQueryable<Vehicle> Aplicar(IApplicationDbContext db, IQueryable<Vehicle> consulta, FiltrosDeVehiculos f, DateOnly hoy)
    {
        if (f.Status is { } estado) consulta = consulta.Where(v => v.Status == estado);
        if (f.OwnerPersonPublicId is { } dueño) consulta = consulta.Where(v => v.OwnerPerson!.PublicId == dueño);
        if (!string.IsNullOrWhiteSpace(f.Q)) consulta = Buscar(db, consulta, f.Q);

        if (f.Affiliation is { } clase)
        {
            var asociados = db.Associates.AsNoTracking().Where(AfiliacionVigente.VigenteEn(hoy)).Select(a => a.PersonId);
            consulta = clase == AffiliationClass.Associate
                ? consulta.Where(v => asociados.Contains(v.OwnerPersonId))
                : consulta.Where(v => !asociados.Contains(v.OwnerPersonId));
        }

        var filtro = f.ServiceFilter ?? (f.ServicePublicId is null ? null : VehicleServiceFilter.Subscribed);
        if (filtro is null) return consulta;
        var vivas = SuscripcionesNoTerminadas(db, hoy);
        switch (filtro.Value)
        {
            case VehicleServiceFilter.Subscribed when f.ServicePublicId is { } servicio:
            {
                var con = vivas.Where(s => s.Service!.PublicId == servicio).Select(s => s.VehicleId);
                return consulta.Where(v => con.Contains(v.Id));
            }
            case VehicleServiceFilter.NotSubscribed when f.ServicePublicId is { } servicio:
            {
                var con = vivas.Where(s => s.Service!.PublicId == servicio).Select(s => s.VehicleId);
                return consulta.Where(v => !con.Contains(v.Id));
            }
            case VehicleServiceFilter.None or VehicleServiceFilter.NotSubscribed:
            {
                var con = vivas.Select(s => s.VehicleId);
                return consulta.Where(v => !con.Contains(v.Id));
            }
            default:
            {
                var con = vivas.Select(s => s.VehicleId);
                return consulta.Where(v => con.Contains(v.Id));
            }
        }
    }

    /// <summary>Las suscripciones vivas que no terminaron antes de <paramref name="hoy"/> (vigentes o por empezar).</summary>
    public static IQueryable<VehicleSubscription> SuscripcionesNoTerminadas(IApplicationDbContext db, DateOnly hoy) =>
        db.VehicleSubscriptions.AsNoTracking().Where(s => !s.IsDeleted && (s.To == null || s.To >= hoy));

    private static IQueryable<Vehicle> Buscar(IApplicationDbContext db, IQueryable<Vehicle> consulta, string q)
    {
        var texto = q.Trim().ToUpperInvariant();
        var placa = Placa.Normalizar(texto);
        IQueryable<int>? dueños = null;
        if (texto.Any(char.IsLetter))
        {
            var patrones = NormalizadorDeBusqueda.PatronesContiene(q);
            if (patrones.Count > 0)
            {
                var escape = NormalizadorDeBusqueda.CaracterDeEscape.ToString();
                var personas = db.People.AsNoTracking();
                foreach (var patron in patrones) personas = personas.Where(p => EF.Functions.Like(p.SearchText, patron, escape));
                dueños = personas.Select(p => p.Id);
            }
        }
        else
        {
            var documento = UsuarioDeDocumento.NormalizarNumero(q);
            if (documento.Length > 0)
            {
                dueños = documento.Length >= DigitosParaBuscarPorComienzo
                    ? db.People.AsNoTracking().Where(p => p.TaxId.StartsWith(documento)).Select(p => p.Id)
                    : db.People.AsNoTracking().Where(p => p.TaxId == documento).Select(p => p.Id);
            }
        }

        if (placa.Length == 0)
            return dueños is null
                ? consulta.Where(v => v.InternalNumber == texto || v.RadioCode == texto)
                : consulta.Where(v => v.InternalNumber == texto || v.RadioCode == texto || dueños.Contains(v.OwnerPersonId));
        return dueños is null
            ? consulta.Where(v => v.Plate.Contains(placa) || v.InternalNumber == texto || v.RadioCode == texto)
            : consulta.Where(v => v.Plate.Contains(placa) || v.InternalNumber == texto || v.RadioCode == texto || dueños.Contains(v.OwnerPersonId));
    }
}
