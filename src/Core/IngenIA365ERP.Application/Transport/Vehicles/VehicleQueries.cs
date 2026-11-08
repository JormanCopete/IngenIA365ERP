using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Transport.Vehicles;

// Feature 020 (US2; T046, T048; FR-012, FR-015, FR-016; contracts/api.md §3): las consultas del vehículo.

/// <summary>El nombre visible de una persona: la razón social si es jurídica; si no, el nombre completo.</summary>
internal static class NombreVisible
{
    public static string De(Person p) => !string.IsNullOrWhiteSpace(p.BusinessName) ? p.BusinessName!.Trim() : NombreDePersona.Completo(p);
}

/// <summary>
/// Busca vehículos por placa, número interno, radio-móvil, documento o nombre del propietario y por estado (FR-012). Paginado.
/// </summary>
public sealed record ListVehiclesQuery(string? Q = null, VehicleStatus? Status = null, Guid? OwnerPersonPublicId = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedList<VehicleListItemDto>>>;

public sealed class ListVehiclesQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListVehiclesQuery, Result<PagedList<VehicleListItemDto>>>
{
    public async Task<Result<PagedList<VehicleListItemDto>>> Handle(ListVehiclesQuery request, CancellationToken ct)
    {
        var consulta = db.Vehicles.AsNoTracking();
        if (request.Status is { } estado) consulta = consulta.Where(v => v.Status == estado);
        if (request.OwnerPersonPublicId is { } dueño) consulta = consulta.Where(v => v.OwnerPerson!.PublicId == dueño);
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            var texto = request.Q.Trim().ToUpperInvariant();
            var placa = Placa.Normalizar(texto);
            consulta = consulta.Where(v => v.Plate.Contains(placa) || v.InternalNumber == texto || v.RadioCode == texto
                                           || v.OwnerPerson!.TaxId.StartsWith(texto)
                                           || (v.OwnerPerson.FirstName + " " + v.OwnerPerson.LastName).ToUpper().Contains(texto)
                                           || (v.OwnerPerson.BusinessName ?? "").ToUpper().Contains(texto));
        }

        var pagina = Math.Max(1, request.Page);
        var tamano = Math.Clamp(request.PageSize, 1, 500);
        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderBy(v => v.InternalNumber).ThenBy(v => v.Plate)
            .Skip((pagina - 1) * tamano).Take(tamano)
            .Select(v => new { v.PublicId, v.Plate, v.InternalNumber, v.RadioCode, v.Status, v.StatusSince, v.Line, v.ModelYear,
                Marca = v.Brand == null ? null : v.Brand.Name, Dueño = v.OwnerPerson! })
            .ToListAsync(ct);
        var clases = await AfiliacionVigente.DeVariasAsync(db, filas.Select(f => f.Dueño.Id).ToList(), reloj.HoyLocal, ct);
        var items = filas.Select(f => new VehicleListItemDto(f.PublicId, f.Plate, f.InternalNumber, f.RadioCode, f.Status, f.StatusSince,
                f.Dueño.PublicId, f.Dueño.TaxId, NombreVisible.De(f.Dueño), clases.GetValueOrDefault(f.Dueño.Id, AffiliationClass.External),
                f.Marca, f.Line, f.ModelYear))
            .ToList();
        return Result.Success(new PagedList<VehicleListItemDto>(items, total, pagina, tamano));
    }
}

/// <summary>La ficha completa del vehículo (contracts/api.md §3): ficha, propietario con su clase deducida, equipos e historia.</summary>
public sealed record GetVehicleQuery(Guid VehiclePublicId) : IRequest<Result<VehicleDetailDto>>;

public sealed class GetVehicleQueryHandler(IApplicationDbContext db, VehicleRegistrar registrar, IDateTimeService reloj, IServiceProvider servicios)
    : IRequestHandler<GetVehicleQuery, Result<VehicleDetailDto>>
{
    public async Task<Result<VehicleDetailDto>> Handle(GetVehicleQuery request, CancellationToken ct)
    {
        var v = await db.Vehicles.AsNoTracking()
            .Include(x => x.OwnerPerson)
            .Include(x => x.Brand).Include(x => x.VehicleClass).Include(x => x.BodyType).Include(x => x.Fuel)
            .Include(x => x.TransitAuthority).Include(x => x.ServiceClass).Include(x => x.Modality).Include(x => x.RollingGroup)
            .FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure<VehicleDetailDto>(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));

        var hoy = reloj.HoyLocal;
        var dueño = v.OwnerPerson!;
        var clase = await AfiliacionVigente.DeAsync(db, dueño, hoy, ct);

        var equipos = await db.VehicleEquipment.AsNoTracking().Where(e => e.VehicleId == v.Id).Include(e => e.EquipmentType)
            .OrderBy(e => e.EquipmentType!.Name)
            .Select(e => new VehicleEquipmentDto(e.PublicId, new CatalogRefDto(e.EquipmentType!.PublicId, e.EquipmentType.Code, e.EquipmentType.Name), e.Code, e.Status))
            .ToListAsync(ct);

        var eventos = await db.VehicleEvents.AsNoTracking().Where(e => e.VehicleId == v.Id)
            .OrderByDescending(e => e.EffectiveDate).ThenByDescending(e => e.Id).ToListAsync(ct);
        var personas = eventos.Where(e => e.Kind == VehicleEventKind.OwnerChanged)
            .SelectMany(e => new[] { e.FromValue, e.ToValue }).Where(x => Guid.TryParse(x, out _)).Select(Guid.Parse).Distinct().ToList();
        var nombres = personas.Count == 0
            ? []
            : (await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personas.Contains(p.PublicId)).ToListAsync(ct))
                .ToDictionary(p => p.PublicId.ToString(), p => $"{p.TaxId} · {NombreVisible.De(p)}");
        string? Etiqueta(VehicleEvent e, string? valor) => valor is null ? null
            : e.Kind == VehicleEventKind.OwnerChanged ? nombres.GetValueOrDefault(valor, valor)
            : Enum.TryParse<VehicleStatus>(valor, out var s) ? TextoDeEstado(s) : valor;

        var detalle = new VehicleDetailDto
        {
            PublicId = v.PublicId,
            Plate = v.Plate,
            InternalNumber = v.InternalNumber,
            RadioCode = v.RadioCode,
            Status = v.Status,
            StatusSince = v.StatusSince,
            RetiredOn = v.RetiredOn,
            RetirementReason = v.RetirementReason,
            Owner = new OwnerDto(dueño.PublicId, dueño.IdType, dueño.TaxId, NombreVisible.De(dueño), clase, v.OwnerSince),
            Data = await registrar.ComoEntradaAsync(v, ct),
            Brand = Ref(v.Brand), VehicleClass = Ref(v.VehicleClass), BodyType = Ref(v.BodyType), Fuel = Ref(v.Fuel),
            TransitAuthority = Ref(v.TransitAuthority), ServiceClass = Ref(v.ServiceClass), Modality = Ref(v.Modality), RollingGroup = Ref(v.RollingGroup),
            Equipment = equipos,
            History = eventos.Select(e => new VehicleEventDto(e.PublicId, e.Kind, e.EffectiveDate, e.FromValue, e.ToValue, Etiqueta(e, e.FromValue),
                Etiqueta(e, e.ToValue), e.FromAffiliation, e.ToAffiliation, e.Reason, e.Source, e.ChangedByName, e.CreatedAt)).ToList(),
        };

        // Las historias de documentos (US4), conductores (US5) y servicios (US6) completan la ficha.
        foreach (var parte in servicios.GetServices<IParteDeLaFichaDelVehiculo>())
            detalle = await parte.CompletarAsync(detalle, v, ct);
        return Result.Success(detalle);
    }

    private static CatalogRefDto? Ref(TransportCatalogItem? c) => c is null ? null : new CatalogRefDto(c.PublicId, c.Code, c.Name);

    public static string TextoDeEstado(VehicleStatus s) => s switch
    {
        VehicleStatus.Active => "Activo",
        VehicleStatus.Inactive => "Inactivo",
        VehicleStatus.Retired => "Retirado",
        _ => s.ToString(),
    };
}

/// <summary>Una parte de la ficha del vehículo que aporta otra historia (documentos, conductores, servicios).</summary>
public interface IParteDeLaFichaDelVehiculo
{
    Task<VehicleDetailDto> CompletarAsync(VehicleDetailDto ficha, Vehicle vehiculo, CancellationToken ct);
}

/// <summary>
/// Los saldos vencidos del propietario actual de un vehículo en la cooperativa (FR-015): las obligaciones de Cartera con valor vencido y las
/// cuentas por cobrar comerciales pendientes. <b>Sólo informa</b>: ni el cambio de propietario ni el retiro se bloquean por esto.
/// </summary>
public sealed record GetOwnerDebtsQuery(Guid VehiclePublicId) : IRequest<Result<OwnerDebtsDto>>;

public sealed class GetOwnerDebtsQueryHandler(
    IApplicationDbContext db,
    IDateTimeService reloj,
    IConsultaDeObligaciones? cartera = null,
    IContabilidadParaRecaudo? contabilidad = null)
    : IRequestHandler<GetOwnerDebtsQuery, Result<OwnerDebtsDto>>
{
    public async Task<Result<OwnerDebtsDto>> Handle(GetOwnerDebtsQuery request, CancellationToken ct)
    {
        var v = await db.Vehicles.AsNoTracking().Include(x => x.OwnerPerson).FirstOrDefaultAsync(x => x.PublicId == request.VehiclePublicId, ct);
        if (v is null) return Result.Failure<OwnerDebtsDto>(ErroresDeTransporte.VehiculoNoEncontrado(request.VehiclePublicId));
        var dueño = v.OwnerPerson!;
        var items = new List<OwnerDebtDto>();
        var notas = new List<string>();

        if (cartera is not null)
        {
            var obligaciones = await cartera.ObligacionesAsync(new ConsultaDeObligaciones([dueño.PublicId], reloj.HoyLocal, IncludeAsCoDebtor: false), ct);
            if (obligaciones.LendingStatus == EstadoDeCartera.Active)
            {
                items.AddRange(obligaciones.People.SelectMany(p => p.Obligations)
                    .Where(o => o.IsLive && o.DueAmount > 0)
                    .Select(o => new OwnerDebtDto("Cartera", o.Number, o.CreditLineName, o.DueAmount, o.DaysPastDue)));
            }
            else
            {
                notas.Add("Cartera no está activa en la cooperativa.");
            }
        }

        if (contabilidad is not null)
        {
            try
            {
                var comerciales = await CuentasPorCobrarDeRecaudo.PendientesAsync(db, contabilidad, dueño.PublicId, ct);
                items.AddRange(comerciales.Where(c => c.Pending > 0)
                    .Select(c => new OwnerDebtDto("Cuentas por cobrar", c.InvoiceNumber, $"Factura del {c.Date:dd/MM/yyyy}", c.Pending, null)));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                notas.Add("No se pudieron consultar las cuentas por cobrar comerciales.");
            }
        }

        return Result.Success(new OwnerDebtsDto(dueño.PublicId, NombreVisible.De(dueño), items.Sum(i => i.OverdueAmount), items,
            notas.Count == 0 ? null : string.Join(' ', notas)));
    }
}
