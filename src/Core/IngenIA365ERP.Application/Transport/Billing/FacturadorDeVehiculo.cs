using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Transport.Billing;

/// <summary>
/// Lo que se hace cuando un cargo de servicio queda pagado por primera vez (R14): emitir la solicitud al conector del servicio. Lo implementa la
/// fase de envíos; sin implementación, nada. <b>No guarda.</b>
/// </summary>
public interface INotificadorDeCargos
{
    Task AlPagarAsync(ServiceCharge cargo, CancellationToken ct);
}

/// <summary>Un servicio que se cobra a un vehículo en el período.</summary>
public sealed record CargoACobrar(TransportService Servicio, decimal Cantidad, decimal? PrecioPactado);

/// <summary>Lo que el cálculo dice de un vehículo: sus cargos, o por qué se omite.</summary>
public sealed record CargosDelVehiculo(Vehicle Vehiculo, IReadOnlyList<CargoACobrar> Cargos, string? CodigoOmision, string? MotivoOmision, bool YaFacturado)
{
    public bool Omitido => CodigoOmision is not null;
}

/// <summary>
/// Qué servicios se cobran a un vehículo en un período (feature 020, R9, R10, T109; FR-030, FR-034): sus suscripciones vigentes en el mes,
/// de servicios activos, menos lo ya facturado (cargo no anulado del mismo servicio y período), con la cantidad según la política de
/// prorrateo y el precio pactado vigente a la fecha de la factura (sin él, el precio lo pone la lista de Comercial al facturar). Un vehículo
/// que no está activo se omite con su motivo.
/// </summary>
public sealed class CalculoDeCargosDelPeriodo(IApplicationDbContext db)
{
    public async Task<CargosDelVehiculo> CalcularAsync(Vehicle v, PeriodoDeServicio periodo, BillingPolicy politica, DateOnly fechaDeFactura, CancellationToken ct)
    {
        if (v.Status != VehicleStatus.Active)
            return new(v, [], "Billing.VehicleNotActive", $"El vehículo está {(v.Status == VehicleStatus.Inactive ? "inactivo" : "retirado")}.", false);

        var suscripciones = await db.VehicleSubscriptions.AsNoTracking().Include(s => s.Service).Include(s => s.Prices)
            .Where(s => s.VehicleId == v.Id && s.From <= periodo.Fin && (s.To == null || s.To >= periodo.Inicio) && s.Service!.IsActive)
            .ToListAsync(ct);
        if (suscripciones.Count == 0) return new(v, [], "Billing.NoSubscription", "No tiene suscripciones vigentes en el período.", false);

        var codigo = periodo.Codigo;
        var cobrados = await db.ServiceCharges.AsNoTracking()
            .Where(c => c.VehicleId == v.Id && c.Period == codigo && c.Status != ChargeStatus.Voided).Select(c => c.ServiceId).ToListAsync(ct);

        var cargos = new List<CargoACobrar>();
        foreach (var s in suscripciones.OrderBy(s => s.Service!.Priority).ThenBy(s => s.Service!.Name))
        {
            if (cobrados.Contains(s.ServiceId) || cargos.Any(c => c.Servicio.Id == s.ServiceId)) continue;
            var cantidad = Prorrateo.Cantidad(periodo, s.From, s.To, politica.Proration == ProrationPolicy.Prorated);
            if (cantidad <= 0m) continue;
            var pactado = s.Prices.Where(p => !p.IsDeleted && p.VigenteEn(fechaDeFactura)).Select(p => (decimal?)p.Price).FirstOrDefault();
            cargos.Add(new CargoACobrar(s.Service!, cantidad, pactado));
        }

        if (cargos.Count == 0)
            return cobrados.Count > 0
                ? new(v, [], null, null, true)
                : new(v, [], "Billing.NoSubscription", "No tiene servicios por cobrar en el período.", false);
        return new(v, cargos, null, null, false);
    }
}

/// <summary>El resultado de facturar un vehículo.</summary>
public sealed record FacturaDeVehiculo(bool Facturado, bool YaEstaba, string? CodigoOmision, string? MotivoOmision, Guid? InvoicePublicId, string? InvoiceNumber,
    decimal Total, IReadOnlyList<ServiceCharge> Cargos)
{
    public static FacturaDeVehiculo Omitido(string codigo, string motivo) => new(false, false, codigo, motivo, null, null, 0m, []);

    public static FacturaDeVehiculo Repetido() => new(false, true, null, null, null, null, 0m, []);
}

/// <summary>
/// Factura un vehículo en el período por Comercial (feature 020, R10, T110; FR-030 a FR-032, FR-058, FR-059): un borrador de venta con una
/// línea por servicio en orden de prioridad (con su precio pactado, que no es descuento), <b>un pago por línea</b> —a crédito, con la línea
/// de Cartera del servicio, o de contado en la caja abierta— y la confirmación, todo por los comandos de Comercial
/// (<c>ElTransporteFacturaPorComercial</c>). Comercial emite un <c>VentaACreditoRegistrada</c> por cada pago a crédito, así que Cartera crea
/// una obligación por servicio. Luego registra los cargos, que son la marca de «ya facturado» (índice único por vehículo, servicio y período).
/// La factura lleva <c>ExternalReference = TRN:{vehículo}:{aaaamm}</c>: si una vuelta anterior dejó el borrador o la factura, se retoma.
/// Un fallo conocido de Comercial deja el vehículo omitido con su motivo. Corre en la petición de quien factura: el mensaje a Cartera lo
/// origina la persona (T11 de la 012).
/// </summary>
public sealed class FacturadorDeVehiculo(
    IApplicationDbContext db,
    ISender sender,
    FacturasDeServicios facturas,
    CalculoDeCargosDelPeriodo calculo,
    IActorActual actor,
    IDateTimeService reloj,
    IConsultaDeObligaciones? cartera = null,
    INotificadorDeCargos? notificador = null)
{
    public static string Referencia(Vehicle v, PeriodoDeServicio p) => $"TRN:{v.PublicId:N}:{p.Codigo}";

    public async Task<FacturaDeVehiculo> FacturarAsync(Vehicle v, PeriodoDeServicio periodo, BillingPaymentMode modo, BillingRun corrida, BillingPolicy politica, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var fecha = hoy < periodo.Inicio ? periodo.Inicio : hoy;
        var referencia = Referencia(v, periodo);
        var existente = await facturas.PorReferenciaAsync(referencia, ct);

        // Una vuelta anterior ya confirmó la factura: se reconstruyen los cargos que falten.
        if (existente is { Status: DocumentStatus.Confirmed })
        {
            var yaCalculo = await calculo.CalcularAsync(v, periodo, politica, existente.OperationDate, ct);
            if (yaCalculo.Cargos.Count == 0) return FacturaDeVehiculo.Repetido();
            return await RegistrarCargosAsync(v, periodo, modo, corrida, existente, yaCalculo.Cargos, ct);
        }

        var calculado = await calculo.CalcularAsync(v, periodo, politica, fecha, ct);
        if (calculado.YaFacturado) return FacturaDeVehiculo.Repetido();
        if (calculado.Omitido) return FacturaDeVehiculo.Omitido(calculado.CodigoOmision!, calculado.MotivoOmision!);

        var dueño = await db.People.AsNoTracking().FirstAsync(p => p.Id == v.OwnerPersonId, ct);
        var clase = await AfiliacionVigente.DeAsync(db, dueño, fecha, ct);

        var tipo = await db.InventoryDocumentTypes.AsNoTracking().Where(t => t.Code == politica.SalesDocumentTypeCode).Select(t => (Guid?)t.PublicId).FirstOrDefaultAsync(ct);
        if (tipo is null) return FacturaDeVehiculo.Omitido("Billing.DocumentTypeNotFound", $"No existe el tipo de documento {politica.SalesDocumentTypeCode}.");
        var bodega = politica.WarehouseCode is null
            ? null
            : await db.Warehouses.AsNoTracking().Where(w => w.Code == politica.WarehouseCode).Select(w => (Guid?)w.PublicId).FirstOrDefaultAsync(ct);
        if (bodega is null) return FacturaDeVehiculo.Omitido("Billing.NoWarehouse", "La política de facturación no tiene una bodega válida.");

        var lineas = new List<SaveInventoryDraftLine>();
        foreach (var c in calculado.Cargos)
        {
            var unidad = await facturas.UnidadBaseAsync(c.Servicio.ProductPublicId, ct);
            if (unidad is null) return FacturaDeVehiculo.Omitido("Billing.ProductNotFound", $"El producto del servicio {c.Servicio.Name} no existe.");
            lineas.Add(new SaveInventoryDraftLine(null, c.Servicio.ProductPublicId, unidad.Value, c.Cantidad,
                Notes: Recortar($"{c.Servicio.Name} {periodo.Texto()}{(c.PrecioPactado is null ? "" : " · precio pactado de suscripción")}", 200))
            {
                AgreedPrice = c.PrecioPactado,
            });
        }

        var vence = politica.DueDay is >= 1 and <= 28 ? new DateOnly(periodo.Anio, periodo.Mes, politica.DueDay) : periodo.Fin;
        if (vence < fecha) vence = fecha;
        var borrador = new SaveInventoryDraftRequest(tipo.Value, fecha, bodega, null, null, dueño.PublicId, referencia, null, null,
            Recortar($"Servicios de {periodo.Texto()} · vehículo {v.Plate} (N.º {v.InternalNumber})", 1000), null, null, null, lineas,
            Sales: new DatosDeVentaDelBorrador(null, vence, null, []));

        // 1. Borrador sin pagos, para que Comercial precifique; 2. los pagos, uno por línea; 3. la confirmación.
        var guardado = await sender.Send(new SaveInventoryDraftCommand(existente?.PublicId, DocumentClassGroup.Sales, borrador) { OperationKey = Guid.NewGuid() }, ct);
        if (guardado.IsFailure) return Omision(guardado.Error);
        var precificada = await facturas.PorPublicIdAsync(guardado.Value.PublicId, ct);
        if (precificada is null || precificada.Lines.Count != calculado.Cargos.Count)
            return FacturaDeVehiculo.Omitido("Billing.DraftMismatch", "El borrador de la factura no quedó con una línea por servicio.");

        var montos = RepartoDePagosPorLinea.Repartir(precificada.AmountDue, precificada.Lines.Select(l => l.Weight).ToList());
        var pagos = new List<DocumentPaymentInput>();
        if (modo == BillingPaymentMode.Cash)
        {
            var usuario = (await actor.ObtenerAsync(ct)).UserId;
            var sesion = usuario is int u ? await facturas.SesionDeCajaAbiertaAsync(u, ct) : null;
            var efectivo = await facturas.MedioDeEfectivoAsync(ct);
            if (sesion is null || efectivo is null)
                return FacturaDeVehiculo.Omitido("Payments.CashSessionRequired", "Para facturar de contado abra su caja en Comercial.");
            pagos.AddRange(montos.Select(m => new DocumentPaymentInput(efectivo.Value, m, Tendered: m, CashSessionPublicId: sesion)));
        }
        else
        {
            var plazo = (short)Math.Max(0, vence.DayNumber - fecha.DayNumber);
            for (var i = 0; i < calculado.Cargos.Count; i++)
            {
                var s = calculado.Cargos[i].Servicio;
                var codigo = clase == AffiliationClass.Associate ? s.AssociateCreditMeansCode ?? s.CustomerCreditMeansCode : s.CustomerCreditMeansCode ?? s.AssociateCreditMeansCode;
                if (codigo is null)
                    return FacturaDeVehiculo.Omitido("Billing.NoCreditMeans", $"El servicio {s.Name} no tiene medio de pago a crédito para {AfiliacionVigente.Texto(clase).ToLowerInvariant()}s.");
                var medio = await db.PaymentMeans.AsNoTracking().Where(m => m.Code == codigo).Select(m => (Guid?)m.PublicId).FirstOrDefaultAsync(ct);
                if (medio is null) return FacturaDeVehiculo.Omitido("Billing.NoCreditMeans", $"No existe el medio de pago {codigo}.");
                pagos.Add(new DocumentPaymentInput(medio.Value, montos[i],
                    Credit: new PaymentCreditInput(1, plazo, plazo, FirstDueDate: vence, SuggestedLineCode: s.CreditLineCode)));
            }
        }

        var conPagos = await sender.Send(new SaveInventoryDraftCommand(guardado.Value.PublicId, DocumentClassGroup.Sales,
            borrador with { Sales = new DatosDeVentaDelBorrador(null, vence, null, pagos), RowVersion = null }) { OperationKey = Guid.NewGuid() }, ct);
        if (conPagos.IsFailure) return Omision(conPagos.Error);

        var confirmada = await sender.Send(new ConfirmInventoryDocumentCommand(guardado.Value.PublicId, DocumentClassGroup.Sales) { OperationKey = Guid.NewGuid() }, ct);
        if (confirmada.IsFailure) return Omision(confirmada.Error);
        if (confirmada.Value.Status != DocumentStatus.Confirmed)
            return FacturaDeVehiculo.Omitido("Billing.PendingApproval", "La factura quedó pendiente de una aprobación en Comercial.");

        var factura = await facturas.PorPublicIdAsync(guardado.Value.PublicId, ct);
        return factura is null
            ? FacturaDeVehiculo.Omitido("Billing.InvoiceNotFound", "No se encontró la factura confirmada.")
            : await RegistrarCargosAsync(v, periodo, modo, corrida, factura, calculado.Cargos, ct);
    }

    /// <summary>Un cargo por línea de la factura, con el pago que lo respalda; de contado nacen pagados.</summary>
    private async Task<FacturaDeVehiculo> RegistrarCargosAsync(Vehicle v, PeriodoDeServicio periodo, BillingPaymentMode modo, BillingRun corrida,
        FacturaDeServicios factura, IReadOnlyList<CargoACobrar> cargos, CancellationToken ct)
    {
        var conCartera = cartera is not null
                         && (await cartera.ObligacionesAsync(new ConsultaDeObligaciones([], factura.OperationDate, false), ct)).LendingStatus == EstadoDeCartera.Active;
        var ahora = reloj.UtcNow;
        var registrados = new List<ServiceCharge>();
        var pagos = factura.Payments.OrderBy(p => p.LineNumber).ToList();
        foreach (var linea in factura.Lines.OrderBy(l => l.LineNumber))
        {
            var cargo = cargos.FirstOrDefault(c => c.Servicio.ProductPublicId == linea.ProductPublicId && registrados.All(r => r.ServiceId != c.Servicio.Id));
            if (cargo is null) continue;
            var indice = factura.Lines.OrderBy(l => l.LineNumber).ToList().IndexOf(linea);
            var pago = indice < pagos.Count ? pagos[indice] : null;
            var deContado = pago is { IsCredit: false } || modo == BillingPaymentMode.Cash;
            var ruta = deContado ? ChargeRoute.Cash : conCartera && cargo.Servicio.CreditLineCode is not null ? ChargeRoute.Lending : ChargeRoute.CommercialReceivable;
            var c = new ServiceCharge
            {
                VehicleId = v.Id, ServiceId = cargo.Servicio.Id, Period = periodo.Codigo, BillingRunId = corrida.Id, BillingRun = corrida.Id == 0 ? corrida : null,
                OwnerPersonId = v.OwnerPersonId, InvoicePublicId = factura.PublicId, InvoiceNumber = factura.DisplayNumber ?? string.Empty,
                InvoiceLinePublicId = linea.PublicId, PaymentPublicId = pago?.PublicId, Amount = pago?.Amount ?? linea.Weight, Route = ruta,
                Status = ChargeStatus.Open, CreatedAt = ahora,
            };
            db.ServiceCharges.Add(c);
            registrados.Add(c);

            // De contado el cargo nace pagado, con su asignación (R13); la notificación al sistema externo la emite la fase de envíos.
            if (deContado)
            {
                c.Status = ChargeStatus.Paid;
                c.PaidAt = ahora;
                db.ChargeAllocations.Add(new ChargeAllocation
                {
                    Charge = c, Source = AllocationSource.CashAtInvoice, ReceiptNumber = factura.DisplayNumber, Amount = c.Amount, AppliedAt = ahora,
                    ValueDate = factura.OperationDate, CreatedAt = ahora,
                });
            }
        }
        foreach (var pagado in registrados.Where(r => r.Status == ChargeStatus.Paid)) await AlPagarAsync(pagado, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (Choque(ex))
        {
            db.DescartarCambiosSalvo(corrida);
            return FacturaDeVehiculo.Repetido();
        }
        return new FacturaDeVehiculo(true, false, null, null, factura.PublicId, factura.DisplayNumber, registrados.Sum(r => r.Amount), registrados);
    }

    /// <summary>Lo que pasa cuando un cargo queda pagado al facturar (la emisión al conector, fase de envíos). Sin guardar.</summary>
    private Task AlPagarAsync(ServiceCharge cargo, CancellationToken ct) => notificador?.AlPagarAsync(cargo, ct) ?? Task.CompletedTask;

    private static FacturaDeVehiculo Omision(Error e) => FacturaDeVehiculo.Omitido(e.Code, e.Message);

    private static bool Choque(DbUpdateException ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e.Message.Contains("UK_TRN_ServiceCharges_Vehicle_Service_Period", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}
