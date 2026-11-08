using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Transport.Notifications;
using IngenIA365ERP.Domain.Entities.Transport;
using IngenIA365ERP.Domain.Enums.Connectors;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Transport;
using IngenIA365ERP.Domain.Transport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Transport.Integration;

/// <summary>
/// Aplica a los cargos de servicios lo que dicen los pagos y anulaciones que escucha Transporte (feature 020, R12 a R14; T156;
/// contracts/mensajes.md §3 de la 020). Sin ruta (<c>LosComandosDeConsumoNoTienenRuta</c>): lo manda <see cref="DestinoTransporte"/>.
/// Responde siempre un <see cref="ResultadoDeConsumo"/>; lo que no toca ningún cargo es <c>AlreadyProcessed</c> sin escribir nada.
/// </summary>
public sealed record ApplyChargeAllocationsCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ApplyChargeAllocationsCommandValidator : AbstractValidator<ApplyChargeAllocationsCommand>
{
    public ApplyChargeAllocationsCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

/// <summary>
/// Una unidad de trabajo por mensaje (un solo <c>SaveChanges</c>):
/// <list type="number">
/// <item>escribe las asignaciones, idempotentes por cargo y mensaje (el mismo mensaje no asigna dos veces);</item>
/// <item>recalcula el estado de cada cargo tocado: <c>Paid</c> si lo asignado cubre su valor (o Cartera dice que la obligación quedó
/// saldada), <c>Open</c> si una reversa lo baja;</item>
/// <item>emite el aviso al sistema externo de los que pasan a <c>Paid</c> por primera vez;</item>
/// <item>los que se reabren después de avisar dejan su envío <c>PaymentVoided</c>, la marca <see cref="ChargeReviewFlag.PaymentVoidedAfterNotice"/>
/// y una alerta (el sistema externo no deshace);</item>
/// <item>un cargo que ya avisó y vuelve a quedar pagado no avisa otra vez: <see cref="ChargeReviewFlag.PaidAgainAfterVoid"/> y alerta.</item>
/// </list>
/// Por mensaje: <c>PagoAplicado</c> y <c>PagoReversado</c> buscan el cargo por la obligación (y si no la conocen, la preguntan a Cartera por
/// la forma de pago de la venta); <c>ReciboEmitido</c> reparte sus aplicaciones a cuentas por cobrar comerciales entre los cargos de la
/// factura por prioridad; <c>ReciboAnulado</c> reversa lo asignado por ese recibo; <c>DocumentoAnulado</c> anula los cargos abiertos de la
/// factura y deja para revisar los pagados.
/// </summary>
public sealed class ApplyChargeAllocationsCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    EmisionDeSolicitudDeConector emision,
    IDateTimeService reloj,
    IConsultaDeObligaciones? cartera = null,
    IAlertas? alertas = null,
    ILogger<ApplyChargeAllocationsCommandHandler>? logger = null) : IRequestHandler<ApplyChargeAllocationsCommand, Result<ResultadoDeConsumo>>
{
    private const string ConceptoCapital = "Principal";
    private const string AplicacionACuentaPorCobrar = "CommercialReceivable";

    private sealed record Aviso(string Tipo, ServiceCharge Cargo, string Asunto);

    public async Task<Result<ResultadoDeConsumo>> Handle(ApplyChargeAllocationsCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Transport, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);

        var ahora = reloj.UtcNow;
        var avisos = new List<Aviso>();
        var tocados = new Dictionary<int, (ServiceCharge Cargo, bool Saldada, PagoQueSalda Pago)>();
        var escribio = false;

        foreach (var m in leidos.Value)
        {
            if (await db.ChargeAllocations.AsNoTracking().AnyAsync(a => a.SourceMessagePublicId == m.MessageId, ct)) continue;
            switch (m.Payload)
            {
                case PagoAplicadoV1 p:
                {
                    var cargo = await CargoDeLaObligacionAsync(p.Loan.LoanPublicId, p.DebtorPersonPublicId, ct);
                    if (cargo is null) break;
                    var capital = p.Amounts.Where(a => a.Concept == ConceptoCapital).Sum(a => a.Amount);
                    if (capital > 0m) Asignar(cargo, AllocationSource.LendingPayment, m.MessageId, null, p.Collector.Receipt.Number, capital, p.ValueDate, null, ahora);
                    Tocar(tocados, cargo, p.PaidOff, new PagoQueSalda(p.Collector.Receipt.Number, p.ValueDate));
                    escribio = true;
                    break;
                }
                case PagoReversadoV1 r:
                {
                    var cargo = await CargoDeLaObligacionAsync(r.Loan.LoanPublicId, r.DebtorPersonPublicId, ct);
                    if (cargo is null) break;
                    var capital = r.Amounts.Where(a => a.Concept == ConceptoCapital).Sum(a => Math.Abs(a.Amount));
                    if (capital > 0m) Asignar(cargo, AllocationSource.LendingPayment, m.MessageId, null, r.Collector.Receipt.Number, -capital, r.ValueDate, null, ahora);
                    Tocar(tocados, cargo, false, new PagoQueSalda(null, null));
                    escribio = true;
                    break;
                }
                case ReciboEmitidoV1 e:
                    escribio |= await ReciboAsync(e, m.MessageId, tocados, ahora, ct);
                    break;
                case ReciboAnuladoV1 a:
                    escribio |= await AnulacionDeReciboAsync(a, m.MessageId, tocados, ahora, ct);
                    break;
                case DocumentoAnuladoV1 d:
                    escribio |= await FacturaAnuladaAsync(d, avisos, ct);
                    break;
                default:
                    return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted",
                        $"Transporte no acepta {m.Type} v{m.Version}."));
            }
        }

        foreach (var (cargo, saldada, pago) in tocados.Values) await RecalcularAsync(cargo, saldada, pago, avisos, ahora, ct);

        if (!escribio && avisos.Count == 0) return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.AlreadyProcessed(null, SinComprobante: MotivoSinComprobante.Informational));
        await db.SaveChangesAsync(ct);
        await AlertarAsync(avisos, ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
    }

    /// <summary>
    /// El cargo de una obligación de Cartera. Si ningún cargo la conoce todavía, pregunta a Cartera por las formas de pago a crédito de los
    /// cargos abiertos del deudor y guarda las que respondan (así la siguiente vez la encuentra por índice).
    /// </summary>
    private async Task<ServiceCharge?> CargoDeLaObligacionAsync(Guid obligacion, Guid deudor, CancellationToken ct)
    {
        var cargo = await db.ServiceCharges.Include(c => c.Allocations).Include(c => c.Service)
            .FirstOrDefaultAsync(c => c.LoanPublicId == obligacion && c.Status != ChargeStatus.Voided, ct);
        if (cargo is not null || cartera is null) return cargo;

        var persona = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => p.PublicId == deudor).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (persona is null) return null;
        var candidatos = await db.ServiceCharges.Include(c => c.Allocations).Include(c => c.Service)
            .Where(c => c.OwnerPersonId == persona && c.Route == ChargeRoute.Lending && c.LoanPublicId == null && c.PaymentPublicId != null
                        && c.Status != ChargeStatus.Voided)
            .ToListAsync(ct);
        if (candidatos.Count == 0) return null;

        var obligaciones = await cartera.PorPagoDeVentaAsync(candidatos.Select(c => c.PaymentPublicId!.Value).ToList(), ct);
        foreach (var o in obligaciones)
            foreach (var c in candidatos.Where(c => c.PaymentPublicId == o.PaymentPublicId)) c.LoanPublicId = o.ObligationPublicId;
        return candidatos.FirstOrDefault(c => c.LoanPublicId == obligacion);
    }

    private async Task<bool> ReciboAsync(ReciboEmitidoV1 e, Guid mensaje, Dictionary<int, (ServiceCharge, bool, PagoQueSalda)> tocados, DateTime ahora, CancellationToken ct)
    {
        var escribio = false;
        foreach (var aplicacion in e.Applications.Where(a => a.Target == AplicacionACuentaPorCobrar && !string.IsNullOrWhiteSpace(a.ProductNumber)))
        {
            var cargos = await db.ServiceCharges.Include(c => c.Allocations).Include(c => c.Service)
                .Where(c => c.InvoiceNumber == aplicacion.ProductNumber && c.Route == ChargeRoute.CommercialReceivable && c.Status == ChargeStatus.Open)
                .ToListAsync(ct);
            if (cargos.Count == 0) continue;

            var (partes, _) = RepartoPorPrioridad.Repartir(aplicacion.Amount,
                cargos.Select(c => new CargoPorRepartir(c.Id, c.Service?.Priority ?? int.MaxValue, c.Id, c.Amount - Asignado(c))));
            foreach (var parte in partes)
            {
                var cargo = cargos.First(c => c.Id == parte.CargoId);
                Asignar(cargo, AllocationSource.CommercialReceipt, mensaje, e.ReceiptPublicId, e.Number, parte.Importe, e.ValueDate, null, ahora);
                Tocar(tocados, cargo, false, new PagoQueSalda(e.Number, e.ValueDate));
                escribio = true;
            }
        }
        return escribio;
    }

    private async Task<bool> AnulacionDeReciboAsync(ReciboAnuladoV1 a, Guid mensaje, Dictionary<int, (ServiceCharge, bool, PagoQueSalda)> tocados, DateTime ahora, CancellationToken ct)
    {
        var originales = await db.ChargeAllocations.AsNoTracking()
            .Where(x => x.ReceiptPublicId == a.ReceiptPublicId && x.Amount > 0m && x.ReversesAllocationId == null).ToListAsync(ct);
        if (originales.Count == 0) return false;
        var yaReversadas = await db.ChargeAllocations.AsNoTracking()
            .Where(x => x.ReversesAllocationId != null && originales.Select(o => o.Id).Contains(x.ReversesAllocationId.Value))
            .Select(x => x.ReversesAllocationId!.Value).ToListAsync(ct);

        var escribio = false;
        foreach (var original in originales.Where(o => !yaReversadas.Contains(o.Id)))
        {
            var cargo = await db.ServiceCharges.Include(c => c.Allocations).Include(c => c.Service).FirstAsync(c => c.Id == original.ChargeId, ct);
            Asignar(cargo, AllocationSource.CommercialReceipt, mensaje, a.ReceiptPublicId, a.ReceiptNumber, -original.Amount, a.VoidedOn, original.Id, ahora);
            Tocar(tocados, cargo, false, new PagoQueSalda(null, null));
            escribio = true;
        }
        return escribio;
    }

    private async Task<bool> FacturaAnuladaAsync(DocumentoAnuladoV1 d, List<Aviso> avisos, CancellationToken ct)
    {
        var cargos = await db.ServiceCharges.Include(c => c.Allocations)
            .Where(c => c.InvoicePublicId == d.VoidedDocument.PublicId && c.Status != ChargeStatus.Voided).ToListAsync(ct);
        var escribio = false;
        foreach (var c in cargos)
        {
            if (c.Status == ChargeStatus.Open && c.NotifiedRequestPublicId is null && Asignado(c) == 0m)
            {
                // Sin pago ni aviso: el cargo se anula y el vehículo puede volver a facturarse en el período.
                c.Status = ChargeStatus.Voided;
                escribio = true;
            }
            else if (c.ReviewFlag != ChargeReviewFlag.PaymentVoidedAfterNotice)
            {
                c.ReviewFlag = ChargeReviewFlag.PaymentVoidedAfterNotice;
                await MarcarEnvioAnuladoAsync(c, ct);
                avisos.Add(new Aviso(TiposDeAlerta.TransportePagoAnuladoTrasAviso, c, $"Se anuló la factura {c.InvoiceNumber}, que ya tenía pagos o aviso"));
                escribio = true;
            }
        }
        return escribio;
    }

    private async Task RecalcularAsync(ServiceCharge cargo, bool saldada, PagoQueSalda pago, List<Aviso> avisos, DateTime ahora, CancellationToken ct)
    {
        if (cargo.Status == ChargeStatus.Voided) return;
        var pagado = saldada || Asignado(cargo) >= cargo.Amount;
        if (pagado && cargo.Status == ChargeStatus.Open)
        {
            cargo.Status = ChargeStatus.Paid;
            cargo.PaidAt = ahora;
            if (cargo.NotifiedRequestPublicId is null)
            {
                await emision.EmitirAsync(cargo, pago, ct);
            }
            else
            {
                // Ya avisó una vez (y ese pago se anuló): no se avisa de nuevo, se revisa en el sistema externo.
                cargo.ReviewFlag = ChargeReviewFlag.PaidAgainAfterVoid;
                avisos.Add(new Aviso(TiposDeAlerta.TransportePagadoOtraVez, cargo, $"El servicio de la factura {cargo.InvoiceNumber} volvió a quedar pagado"));
            }
        }
        else if (!pagado && cargo.Status == ChargeStatus.Paid)
        {
            cargo.Status = ChargeStatus.Open;
            cargo.PaidAt = null;
            if (cargo.NotifiedRequestPublicId is not null)
            {
                cargo.ReviewFlag = ChargeReviewFlag.PaymentVoidedAfterNotice;
                if (await MarcarEnvioAnuladoAsync(cargo, ct))
                    avisos.Add(new Aviso(TiposDeAlerta.TransportePagoAnuladoTrasAviso, cargo, $"Se anuló el pago del servicio de la factura {cargo.InvoiceNumber}"));
            }
        }
    }

    /// <summary>El envío del cargo queda «pago anulado»; devuelve verdadero si ya había salido (hay que revisar el sistema externo).</summary>
    private async Task<bool> MarcarEnvioAnuladoAsync(ServiceCharge cargo, CancellationToken ct)
    {
        if (cargo.NotifiedRequestPublicId is not { } envioId) return false;
        var envio = await db.ConnectorRequests.FirstOrDefaultAsync(r => r.PublicId == envioId, ct);
        if (envio is null) return false;
        var salio = envio.Status is ConnectorRequestStatus.Sent or ConnectorRequestStatus.SentWithWarning or ConnectorRequestStatus.ResolvedManually;
        envio.Status = ConnectorRequestStatus.PaymentVoided;
        return salio;
    }

    private void Asignar(ServiceCharge cargo, AllocationSource fuente, Guid mensaje, Guid? recibo, string? numero, decimal importe, DateOnly? fechaValor,
        int? reversa, DateTime ahora)
    {
        var asignacion = new ChargeAllocation
        {
            Charge = cargo, ChargeId = cargo.Id, Source = fuente, SourceMessagePublicId = mensaje, ReceiptPublicId = recibo, ReceiptNumber = numero,
            Amount = importe, ReversesAllocationId = reversa, AppliedAt = ahora, ValueDate = fechaValor, CreatedBy = "Proceso de integración",
        };
        cargo.Allocations.Add(asignacion);
        db.ChargeAllocations.Add(asignacion);
    }

    private static void Tocar(Dictionary<int, (ServiceCharge Cargo, bool Saldada, PagoQueSalda Pago)> tocados, ServiceCharge cargo, bool saldada, PagoQueSalda pago)
    {
        if (tocados.TryGetValue(cargo.Id, out var previo))
            tocados[cargo.Id] = (cargo, previo.Saldada || saldada, pago.Recibo is null ? previo.Pago : pago);
        else
            tocados[cargo.Id] = (cargo, saldada, pago);
    }

    private static decimal Asignado(ServiceCharge cargo) => cargo.Allocations.Where(a => !a.IsDeleted).Sum(a => a.Amount);

    private async Task AlertarAsync(IReadOnlyList<Aviso> avisos, CancellationToken ct)
    {
        if (alertas is null) return;
        foreach (var a in avisos)
        {
            try
            {
                await alertas.LevantarAsync(new AlertaALevantar(a.Tipo, a.Asunto,
                    $"{a.Asunto} (período {a.Cargo.Period}). Revise el envío en Transporte › Envíos y el sistema externo.",
                    nameof(ServiceCharge), a.Cargo.PublicId, DedupKey: $"{a.Tipo}:{a.Cargo.PublicId:N}",
                    RecipientPermissions: [TransportPermissions.NotificationsManage], ModuloDeOrigen: ModulosDeOrigen.Transporte), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogWarning(ex, "[Transporte.Alerta] No se pudo levantar {Tipo} del cargo {Cargo}.", a.Tipo, a.Cargo.PublicId);
            }
        }
    }
}
