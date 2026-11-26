using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Transport.Billing;
using IngenIA365ERP.Domain.Entities.Transport;
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
    public const string FacturacionPrevia = "facturacion-previa";
    public const string Envios = "envios";

    public static IReadOnlyList<VistaDeInformeDeRecaudoYTesoreria> Vistas { get; } =
    [
        new(Vehiculos, "Vehículos", "El parque con su propietario, su clase de vinculación, su estado y su ficha técnica básica.", "vehiculos", []),
        new(Historia, "Historia de vehículos", "Los cambios de estado, de propietario y de datos de cada vehículo en el rango, con su motivo.", "historia-vehiculos",
            ["from", "to"]),
        new(Vencimientos, "Vencimientos de documentos", "Los documentos vigentes vencidos o que vencen en los próximos días, por vehículo.", "vencimientos",
            ["days"]),
        new(Conductores, "Conductores", "Los conductores con sus vehículos asignados y las fechas de cada asignación.", "conductores", []),
        new(Facturacion, "Facturación de servicios",
            "Los cargos de un período (o de una corrida) por vehículo y servicio, con su factura, su estado DIAN, la obligación de Cartera y el estado del cargo.",
            "facturacion-servicios", ["period"]),
        new(FacturacionPrevia, "Vista previa de la facturación",
            "Lo que facturaría el período, vehículo por vehículo y servicio por servicio, y lo que se omitiría con su motivo. No guarda nada.",
            "facturacion-vista-previa", ["period"]),
        new(Envios, "Envíos a sistemas externos", "Los avisos de servicios pagados en el rango, con su estado, intentos y última respuesta (sin cuerpos).",
            "envios", ["from", "to"]),
    ];

    public static VistaDeInformeDeRecaudoYTesoreria? Buscar(string clave) => Vistas.FirstOrDefault(v => v.Key == clave);
}

/// <summary>
/// Los filtros de las vistas de Transporte (el formato lo usa la entrega del archivo). <c>Q</c>, <c>Status</c>, <c>Affiliation</c>,
/// <c>Service</c> y <c>ServiceFilter</c> son los del listado de vehículos (2026-11-25): «Exportar» de esa pantalla descarga lo filtrado, con
/// las mismas reglas (<see cref="FiltroDeVehiculos"/>). Sin ellos, el informe trae todo el parque, como antes.
/// </summary>
public sealed record FiltrosDeInformeDeTransporte(string? Format, DateOnly? From, DateOnly? To, string? Period, int? Days,
    string? Q = null, VehicleStatus? Status = null, AffiliationClass? Affiliation = null, Guid? Service = null, VehicleServiceFilter? ServiceFilter = null,
    Guid? Run = null)
{
    public object ParaAuditoria() => new
    {
        from = From, to = To, period = Period, days = Days, q = Q, status = Status, affiliation = Affiliation, service = Service, serviceFilter = ServiceFilter,
        run = Run,
    };
}

public sealed record TransportReportQuery(string Vista, FiltrosDeInformeDeTransporte Filtros) : IRequest<Result<TablaExportable>>;

public sealed class TransportReportQueryHandler(IApplicationDbContext db, IDateTimeService reloj, ISender? sender = null, IConsultaDeObligaciones? cartera = null)
    : IRequestHandler<TransportReportQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(TransportReportQuery request, CancellationToken ct)
    {
        var vista = VistasDeTransporte.Buscar(request.Vista);
        if (vista is null) return Result.Failure<TablaExportable>("Report.ViewNotFound", "No existe ese informe.");
        var f = request.Filtros;
        var hoy = reloj.HoyLocal;
        return request.Vista switch
        {
            VistasDeTransporte.Vehiculos => Result.Success(await VehiculosAsync(vista.Name, hoy,
                new FiltrosDeVehiculos(f.Q, f.Status, null, f.Affiliation, f.Service, f.ServiceFilter), ct)),
            VistasDeTransporte.Historia => Result.Success(await HistoriaAsync(vista.Name, f.From ?? hoy.AddMonths(-1), f.To ?? hoy, ct)),
            VistasDeTransporte.Vencimientos => Result.Success(await VencimientosAsync(vista.Name, hoy, Math.Clamp(f.Days ?? 30, 0, 365), ct)),
            VistasDeTransporte.Conductores => Result.Success(await ConductoresAsync(vista.Name, hoy, ct)),
            VistasDeTransporte.Facturacion => await FacturacionAsync(vista.Name, f.Period ?? PeriodoDeServicio.De(hoy).Codigo, f.Run, ct),
            VistasDeTransporte.FacturacionPrevia => await VistaPreviaAsync(vista.Name, f.Period ?? PeriodoDeServicio.De(hoy).Codigo, ct),
            _ => Result.Success(await EnviosAsync(vista.Name, f.From ?? hoy.AddMonths(-1), f.To ?? hoy, ct)),
        };
    }

    private async Task<TablaExportable> VehiculosAsync(string titulo, DateOnly hoy, FiltrosDeVehiculos filtros, CancellationToken ct)
    {
        var filas = await FiltroDeVehiculos.Aplicar(db, db.Vehicles.AsNoTracking(), filtros, hoy).Include(v => v.OwnerPerson).Include(v => v.Brand)
            .OrderBy(v => v.InternalNumber).ThenBy(v => v.Plate).ToListAsync(ct);
        var clases = await AfiliacionVigente.DeVariasAsync(db, filas.Select(v => v.OwnerPersonId).Distinct().ToList(), hoy, ct);
        var servicios = await ServiciosDelListado.DeAsync(db, filas.Select(v => v.Id).ToList(), hoy, ct);
        string? Servicios(int vehiculo) => servicios.TryGetValue(vehiculo, out var s) && s.Count > 0 ? string.Join("; ", s.Select(ServiciosDelListado.Texto)) : null;
        return new TablaExportable(titulo, $"Con corte al {hoy:dd/MM/yyyy}",
        [
            new("Número interno"), new("Placa"), new("Radio-móvil"), new("Documento del propietario"), new("Propietario"), new("Vinculación"),
            new("Estado"), new("Marca"), new("Línea"), new("Modelo", TipoDeColumna.Entero), new("Ingreso", TipoDeColumna.Fecha), new("Servicios"),
        ],
        filas.Select(v => new FilaExportable(
        [
            v.InternalNumber, v.Plate, v.RadioCode, v.OwnerPerson?.TaxId, v.OwnerPerson is { } p ? NombreVisible.De(p) : null,
            AfiliacionVigente.Texto(clases.GetValueOrDefault(v.OwnerPersonId, AffiliationClass.External)), Estado(v.Status), v.Brand?.Name, v.Line, (int?)v.ModelYear, v.JoinedOn,
            Servicios(v.Id),
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

    /// <summary>
    /// Los cargos de un período, o de una corrida (<c>run</c>), para validar la facturación (2026-11-25, pedido del dueño): factura, vehículo,
    /// propietario, servicio, valor, lo pagado, el estado DIAN de la factura, la obligación de Cartera y el estado del cargo.
    /// </summary>
    private async Task<Result<TablaExportable>> FacturacionAsync(string titulo, string periodo, Guid? corrida, CancellationToken ct)
    {
        var consulta = db.ServiceCharges.AsNoTracking().Include(c => c.Vehicle).Include(c => c.Service).Include(c => c.OwnerPerson).Include(c => c.Allocations)
            .AsQueryable();
        string subtitulo;
        if (corrida is { } run)
        {
            var r = await db.TransportBillingRuns.AsNoTracking().Where(x => x.PublicId == run).Select(x => new { x.Id, x.Period, x.StartedAt, x.StartedBy })
                .FirstOrDefaultAsync(ct);
            if (r is null) return Result.Failure<TablaExportable>("Billing.RunNotFound", "No existe esa corrida.");
            consulta = consulta.Where(c => c.BillingRunId == r.Id);
            subtitulo = $"Corrida del período {(PeriodoDeServicio.TryParse(r.Period, out var pr) ? pr.Texto() : r.Period)}, iniciada por {r.StartedBy}"
                        + (r.StartedAt is { } inicio ? $" el {inicio:dd/MM/yyyy HH:mm} (UTC)" : "");
        }
        else
        {
            if (!PeriodoDeServicio.TryParse(periodo, out var p))
                return Result.Failure<TablaExportable>("Transport.Billing.PeriodInvalid", "El período va como aaaamm.");
            consulta = consulta.Where(c => c.Period == p.Codigo);
            subtitulo = $"Período {p.Texto()}";
        }
        var filas = await consulta.OrderBy(c => c.Vehicle!.InternalNumber).ThenBy(c => c.InvoiceNumber).ThenBy(c => c.Service!.Priority).ToListAsync(ct);

        var dian = await new FacturasDeServicios(db).EstadosDianAsync(filas.Select(c => c.InvoicePublicId).ToList(), ct);
        var pagos = filas.Where(c => c.PaymentPublicId is not null).Select(c => c.PaymentPublicId!.Value).Distinct().ToList();
        var obligaciones = cartera is null || pagos.Count == 0
            ? new Dictionary<Guid, ObligacionDeVentaDto>()
            : (await cartera.PorPagoDeVentaAsync(pagos, ct)).GroupBy(o => o.PaymentPublicId).ToDictionary(g => g.Key, g => g.First());
        string? Obligacion(ServiceCharge c) =>
            c.PaymentPublicId is { } pago && obligaciones.TryGetValue(pago, out var o) ? $"{o.Number} ({o.Status})" : c.Route == ChargeRoute.Lending ? "Sin obligación todavía" : null;

        var columnas = new List<ColumnaExportable>
        {
            new("Factura"), new("Estado DIAN"), new("Número interno"), new("Placa"), new("Documento del propietario"), new("Propietario"), new("Servicio"),
            new("Período"), new("Valor", TipoDeColumna.Moneda), new("Pagado", TipoDeColumna.Moneda), new("Ruta"), new("Obligación de Cartera"),
            new("Estado del cargo"), new("Pagado el", TipoDeColumna.Fecha), new("Revisar"),
        };
        var vivos = filas.Where(c => c.Status != ChargeStatus.Voided).ToList();
        return Result.Success(new TablaExportable(titulo, subtitulo, columnas,
            filas.Select(c => new FilaExportable(
            [
                c.InvoiceNumber, dian.GetValueOrDefault(c.InvoicePublicId, "No electrónica"), c.Vehicle?.InternalNumber,
                c.Vehicle?.Plate ?? (c.ExternalPlate is { } placaExterna ? $"Externo, {placaExterna} · lateral {c.VehicleReference}" : $"Externo, lateral {c.VehicleReference}"), c.OwnerPerson?.TaxId, c.OwnerPerson is { } o ? NombreVisible.De(o) : null,
                c.Service?.Name, c.Period, c.Amount, c.Allocations.Where(a => !a.IsDeleted).Sum(a => a.Amount), Ruta(c.Route), Obligacion(c), EstadoDeCargo(c.Status),
                c.PaidAt is { } pagado ? DateOnly.FromDateTime(pagado) : (DateOnly?)null, Revision(c.ReviewFlag),
            ])).ToList(),
            new FilaExportable(["Total", null, null, null, null, null, null, null, vivos.Sum(c => c.Amount),
                vivos.Sum(c => c.Allocations.Where(a => !a.IsDeleted).Sum(a => a.Amount)), null, null, null, null, null], Resaltada: true),
            [$"{filas.Select(c => c.InvoicePublicId).Distinct().Count()} factura(s); {filas.Count(c => c.Status == ChargeStatus.Voided)} cargo(s) anulado(s), que no suman."]));
    }

    /// <summary>
    /// La vista previa del período como tabla (2026-11-25, pedido del dueño): una fila por servicio de cada vehículo que se facturaría y una por
    /// vehículo omitido con su motivo, en hojas aparte. Es la misma consulta de la pantalla: no guarda nada.
    /// </summary>
    private async Task<Result<TablaExportable>> VistaPreviaAsync(string titulo, string periodo, CancellationToken ct)
    {
        if (sender is null) return Result.Failure<TablaExportable>("Report.Unavailable", "La vista previa no está disponible.");
        var r = await sender.Send(new PreviewServiceBillingQuery(periodo), ct);
        if (r.IsFailure) return Result.Failure<TablaExportable>(r.Error);
        var v = r.Value;
        var columnas = new List<ColumnaExportable>
        {
            new("Número interno"), new("Placa"), new("Propietario"), new("Servicio"), new("Cantidad", TipoDeColumna.Decimal), new("Precio", TipoDeColumna.Moneda),
            new("Precio pactado"), new("Valor", TipoDeColumna.Moneda), new("Motivo de omisión"),
        };
        var filas = new List<FilaExportable>();
        foreach (var i in v.Items)
        {
            foreach (var l in i.Lines)
            {
                var valor = l.UnitPrice is { } precio ? Math.Round(l.Quantity * precio, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
                filas.Add(new FilaExportable([i.InternalNumber, i.Plate, i.OwnerName, l.Service, l.Quantity, l.UnitPrice, l.AgreedPrice ? "Sí" : "No", valor, null],
                    Seccion: "Por facturar"));
            }
        }
        foreach (var o in v.Omitted)
            filas.Add(new FilaExportable([o.InternalNumber, o.Plate, null, null, null, null, null, null, o.Reason], Seccion: "Omitidos"));
        var notas = new List<string> { $"{v.Vehicles} vehículo(s) por facturar; {v.Omitted.Count} omitido(s); {v.AlreadyBilled} ya tenían su factura del período." };
        if (v.Warning is { } aviso) notas.Add(aviso);
        return Result.Success(new TablaExportable(titulo, $"Período {v.Period} · sin guardar", columnas, filas,
            new FilaExportable(["Total", null, null, null, null, null, null, v.Total, null], Resaltada: true), notas));
    }

    private static string Ruta(ChargeRoute ruta) => ruta switch
    {
        ChargeRoute.Lending => "Cartera",
        ChargeRoute.CommercialReceivable => "Cuenta por cobrar",
        ChargeRoute.Cash => "Contado",
        _ => ruta.ToString(),
    };

    private static string EstadoDeCargo(ChargeStatus estado) => estado switch
    {
        ChargeStatus.Open => "Abierto",
        ChargeStatus.Paid => "Pagado",
        ChargeStatus.Voided => "Anulado",
        _ => estado.ToString(),
    };

    private static string? Revision(ChargeReviewFlag marca) => marca switch
    {
        ChargeReviewFlag.None => null,
        ChargeReviewFlag.PaymentVoidedAfterNotice => "Pago anulado después del aviso",
        ChargeReviewFlag.PaidAgainAfterVoid => "Pagado otra vez después de anulado",
        ChargeReviewFlag.NoConnector => "Sin conector",
        ChargeReviewFlag.NoticeOff => "Aviso apagado",
        _ => marca.ToString(),
    };

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
                e.CreatedAt, Conector = c.Name, Interno = g != null ? g.Vehicle!.InternalNumber : null, Placa = g != null ? (g.Vehicle != null ? g.Vehicle.Plate : (g.ExternalPlate != null ? "Externo, " + g.ExternalPlate + " · lateral " + g.VehicleReference : "Externo, lateral " + g.VehicleReference)) : null,
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
