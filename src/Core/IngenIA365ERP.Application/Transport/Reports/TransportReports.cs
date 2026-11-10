using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Reports;

/// <summary>
/// Las seis vistas de Transporte (feature 020, T171; R19; FR-054): vehículos, historia, vencimientos, conductores, facturación y envíos, todas
/// <see cref="TablaExportable"/> sobre <c>/api/reports/transport/{vista}</c>. Los filtros que usa cada una van en <c>Filters</c> (la pantalla
/// pinta sólo esos). El informe de envíos no lleva cuerpos ni variables sensibles: sólo estado, intentos y el último código.
/// </summary>
public static class VistasDeTransporte
{
    public const string Vehiculos = "vehiculos";
    public const string Historia = "historia";
    public const string Vencimientos = "vencimientos";
    public const string Conductores = "conductores";
    public const string Facturacion = "facturacion";
    public const string Envios = "envios";

    public static IReadOnlyList<VistaDeInformeDeRecaudoYTesoreria> Vistas { get; } =
    [
        new(Vehiculos, "Vehículos", "El parque con su propietario, su clase de vinculación, su estado y su ficha técnica básica.", "vehiculos", []),
        new(Historia, "Historia de vehículos", "Los cambios de estado, de propietario y de datos de cada vehículo en el rango, con su motivo.", "historia-vehiculos",
            ["from", "to"]),
        new(Vencimientos, "Vencimientos de documentos", "Los documentos vigentes vencidos o que vencen en los próximos días, por vehículo.", "vencimientos",
            ["days"]),
        new(Conductores, "Conductores", "Los conductores con sus vehículos asignados y las fechas de cada asignación.", "conductores", []),
        new(Facturacion, "Facturación de servicios", "Los cargos de un período por vehículo y servicio, con su factura, su ruta de pago y su estado.",
            "facturacion-servicios", ["period"]),
        new(Envios, "Envíos a sistemas externos", "Los avisos de servicios pagados en el rango, con su estado, intentos y última respuesta (sin cuerpos).",
            "envios", ["from", "to"]),
    ];

    public static VistaDeInformeDeRecaudoYTesoreria? Buscar(string clave) => Vistas.FirstOrDefault(v => v.Key == clave);
}

/// <summary>Los filtros de las vistas de Transporte (el formato lo usa la entrega del archivo).</summary>
public sealed record FiltrosDeInformeDeTransporte(string? Format, DateOnly? From, DateOnly? To, string? Period, int? Days)
{
    public object ParaAuditoria() => new { from = From, to = To, period = Period, days = Days };
}

public sealed record TransportReportQuery(string Vista, FiltrosDeInformeDeTransporte Filtros) : IRequest<Result<TablaExportable>>;

public sealed class TransportReportQueryHandler(IApplicationDbContext db, IDateTimeService reloj) : IRequestHandler<TransportReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(TransportReportQuery request, CancellationToken ct)
    {
        var vista = VistasDeTransporte.Buscar(request.Vista);
        if (vista is null) return Result.Failure<TablaExportable>("Report.ViewNotFound", "No existe ese informe.");
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        return request.Vista switch
        {
            VistasDeTransporte.Vehiculos => Result.Success(await VehiculosAsync(vista.Name, hoy, ct)),
            VistasDeTransporte.Historia => Result.Success(await HistoriaAsync(vista.Name, f.From ?? hoy.AddMonths(-1), f.To ?? hoy, ct)),
            VistasDeTransporte.Vencimientos => Result.Success(await VencimientosAsync(vista.Name, hoy, Math.Clamp(f.Days ?? 30, 0, 365), ct)),
            VistasDeTransporte.Conductores => Result.Success(await ConductoresAsync(vista.Name, hoy, ct)),
            VistasDeTransporte.Facturacion => await FacturacionAsync(vista.Name, f.Period ?? PeriodoDeServicio.De(hoy).Codigo, ct),
            _ => Result.Success(await EnviosAsync(vista.Name, f.From ?? hoy.AddMonths(-1), f.To ?? hoy, ct)),
        };
    }

    private async Task<TablaExportable> VehiculosAsync(string titulo, DateOnly hoy, CancellationToken ct)
    {
        var filas = await db.Vehicles.AsNoTracking().Include(v => v.OwnerPerson).Include(v => v.Brand)
            .OrderBy(v => v.InternalNumber).ToListAsync(ct);
        var clases = await AfiliacionVigente.DeVariasAsync(db, filas.Select(v => v.OwnerPersonId).Distinct().ToList(), hoy, ct);
        return new TablaExportable(titulo, $"Con corte al {hoy:dd/MM/yyyy}",
        [
            new("Número interno"), new("Placa"), new("Radio-móvil"), new("Documento del propietario"), new("Propietario"), new("Vinculación"),
            new("Estado"), new("Marca"), new("Línea"), new("Modelo", TipoDeColumna.Entero), new("Ingreso", TipoDeColumna.Fecha),
        ],
        filas.Select(v => new FilaExportable(
        [
            v.InternalNumber, v.Plate, v.RadioCode, v.OwnerPerson?.TaxId, v.OwnerPerson is { } p ? NombreVisible.De(p) : null,
            AfiliacionVigente.Texto(clases.GetValueOrDefault(v.OwnerPersonId, AffiliationClass.External)), Estado(v.Status), v.Brand?.Name, v.Line, (int?)v.ModelYear, v.JoinedOn,
        ])).ToList(), null, [$"{filas.Count} vehículo(s)."]);
    }

    private async Task<TablaExportable> HistoriaAsync(string titulo, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var filas = await db.VehicleEvents.AsNoTracking().Include(e => e.Vehicle)
            .Where(e => e.EffectiveDate >= desde && e.EffectiveDate <= hasta)
            .OrderBy(e => e.EffectiveDate).ThenBy(e => e.Id).ToListAsync(ct);
        return new TablaExportable(titulo, $"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}",
        [
            new("Fecha", TipoDeColumna.Fecha), new("Número interno"), new("Placa"), new("Cambio"), new("Antes"), new("Después"), new("Motivo"), new("Por"),
        ],
        filas.Select(e => new FilaExportable(
        [
            e.EffectiveDate, e.Vehicle?.InternalNumber, e.Vehicle?.Plate, e.Kind.ToString(), e.FromValue, e.ToValue, e.Reason, e.ChangedByName ?? e.CreatedBy,
        ])).ToList(), null, []);
    }

    private async Task<TablaExportable> VencimientosAsync(string titulo, DateOnly hoy, int dias, CancellationToken ct)
    {
        var limite = hoy.AddDays(dias);
        var filas = await db.VehicleDocuments.AsNoTracking().Include(d => d.Vehicle).Include(d => d.DocumentType)
            .Where(d => d.SupersededAt == null && d.ExpiresOn <= limite && d.Vehicle!.Status != VehicleStatus.Retired)
            .OrderBy(d => d.ExpiresOn).ThenBy(d => d.Vehicle!.InternalNumber).ToListAsync(ct);
        return new TablaExportable(titulo, $"Vencidos y por vencer hasta el {limite:dd/MM/yyyy}",
        [
            new("Número interno"), new("Placa"), new("Documento"), new("Número"), new("Emisor"), new("Vence", TipoDeColumna.Fecha),
            new("Días", TipoDeColumna.Entero), new("Situación"),
        ],
        filas.Select(d =>
        {
            var restan = d.ExpiresOn.DayNumber - hoy.DayNumber;
            return new FilaExportable(
            [
                d.Vehicle?.InternalNumber, d.Vehicle?.Plate, d.DocumentType?.Name, d.Number, d.Issuer, d.ExpiresOn, restan, restan < 0 ? "Vencido" : "Por vencer",
            ], Resaltada: restan < 0);
        }).ToList(), null, ["Los vencimientos se informan: no bloquean ni inactivan el vehículo."]);
    }

    private async Task<TablaExportable> ConductoresAsync(string titulo, DateOnly hoy, CancellationToken ct)
    {
        var filas = await db.DriverAssignments.AsNoTracking().Include(a => a.Driver!).ThenInclude(d => d.Person).Include(a => a.Vehicle)
            .OrderBy(a => a.Driver!.Person!.TaxId).ThenBy(a => a.From).ToListAsync(ct);
        return new TablaExportable(titulo, $"Con corte al {hoy:dd/MM/yyyy}",
        [
            new("Documento"), new("Conductor"), new("Número interno"), new("Placa"), new("Desde", TipoDeColumna.Fecha), new("Hasta", TipoDeColumna.Fecha),
            new("Principal"), new("Vigente"),
        ],
        filas.Select(a => new FilaExportable(
        [
            a.Driver?.Person?.TaxId, a.Driver?.Person is { } p ? NombreVisible.De(p) : null, a.Vehicle?.InternalNumber, a.Vehicle?.Plate, a.From, a.To,
            a.IsPrimary ? "Sí" : "No", a.VigenteEn(hoy) ? "Sí" : "No",
        ])).ToList(), null, []);
    }

    private async Task<Result<TablaExportable>> FacturacionAsync(string titulo, string periodo, CancellationToken ct)
    {
        if (!PeriodoDeServicio.TryParse(periodo, out var p))
            return Result.Failure<TablaExportable>("Transport.Billing.PeriodInvalid", "El período va como aaaamm.");
        var filas = await db.ServiceCharges.AsNoTracking().Include(c => c.Vehicle).Include(c => c.Service).Include(c => c.OwnerPerson)
            .Where(c => c.Period == p.Codigo).OrderBy(c => c.Vehicle!.InternalNumber).ThenBy(c => c.Service!.Priority).ToListAsync(ct);
        var columnas = new List<ColumnaExportable>
        {
            new("Número interno"), new("Placa"), new("Propietario"), new("Servicio"), new("Factura"), new("Valor", TipoDeColumna.Moneda), new("Ruta"),
            new("Estado"), new("Pagado", TipoDeColumna.Fecha), new("Revisar"),
        };
        var total = filas.Where(c => c.Status != ChargeStatus.Voided).Sum(c => c.Amount);
        return Result.Success(new TablaExportable(titulo, $"Período {p.Texto()}", columnas,
            filas.Select(c => new FilaExportable(
            [
                c.Vehicle?.InternalNumber, c.Vehicle?.Plate, c.OwnerPerson is { } o ? NombreVisible.De(o) : null, c.Service?.Name, c.InvoiceNumber, c.Amount,
                c.Route.ToString(), c.Status.ToString(), c.PaidAt is { } pagado ? DateOnly.FromDateTime(pagado) : (DateOnly?)null,
                c.ReviewFlag == ChargeReviewFlag.None ? null : c.ReviewFlag.ToString(),
            ])).ToList(),
            new FilaExportable(["Total", null, null, null, null, total, null, null, null, null], Resaltada: true),
            ["Los cargos anulados no suman."]));
    }

    private async Task<TablaExportable> EnviosAsync(string titulo, DateOnly desde, DateOnly hasta, CancellationToken ct)
    {
        var inicio = desde.ToDateTime(TimeOnly.MinValue);
        var fin = hasta.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var filas = await (
            from e in db.ConnectorRequests.AsNoTracking()
            join c in db.Connectors.AsNoTracking() on e.ConnectorId equals c.Id
            join g in db.ServiceCharges.AsNoTracking() on (Guid?)e.PublicId equals g.NotifiedRequestPublicId into cargos
            from g in cargos.DefaultIfEmpty()
            where e.CreatedAt >= inicio && e.CreatedAt < fin
            orderby e.Id
            select new
            {
                e.CreatedAt, Conector = c.Name, Interno = g != null ? g.Vehicle!.InternalNumber : null, Placa = g != null ? g.Vehicle!.Plate : null,
                Servicio = g != null ? g.Service!.Name : null, Periodo = g != null ? g.Period : null, Factura = g != null ? g.InvoiceNumber : null,
                Valor = g != null ? (decimal?)g.Amount : null, e.Status, e.Attempts, e.LastStatusCode, e.SentAt, e.ExternalId,
            }).ToListAsync(ct);
        return new TablaExportable(titulo, $"Del {desde:dd/MM/yyyy} al {hasta:dd/MM/yyyy}",
        [
            new("Fecha", TipoDeColumna.Fecha), new("Conector"), new("Número interno"), new("Placa"), new("Servicio"), new("Período"), new("Factura"),
            new("Valor", TipoDeColumna.Moneda), new("Estado"), new("Intentos", TipoDeColumna.Entero), new("Último código", TipoDeColumna.Entero),
            new("Enviado", TipoDeColumna.Fecha), new("Identificador externo"),
        ],
        filas.Select(x => new FilaExportable(
        [
            DateOnly.FromDateTime(x.CreatedAt), x.Conector, x.Interno, x.Placa, x.Servicio, x.Periodo, x.Factura, x.Valor, x.Status.ToString(), x.Attempts,
            x.LastStatusCode, x.SentAt is { } s ? DateOnly.FromDateTime(s) : (DateOnly?)null, x.ExternalId.ToString("D"),
        ])).ToList(), null, ["Sin cuerpos ni datos personales: el detalle de cada intento está en Transporte › Envíos."]);
    }

    private static string Estado(VehicleStatus estado) => estado switch
    {
        VehicleStatus.Active => "Activo",
        VehicleStatus.Inactive => "Inactivo",
        VehicleStatus.Retired => "Retirado",
        _ => estado.ToString(),
    };
}
