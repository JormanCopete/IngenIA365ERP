using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Integration;

/// <summary>
/// El cruce automático de un anticipo de concepto (2026-11-23, decisión 8a del dueño: «la administración pagada por adelantado se cruza sola al facturar el
/// cargo»). Cartera avisa con <c>ObligacionCreada</c> v1 —destino opcional de Recaudo— que nació la obligación de una venta a crédito; si un concepto de
/// anticipo cubre su línea (<see cref="MiscConcept.AdvanceCreditLineCode"/>) y el deudor tiene anticipo de ese concepto, Recaudo genera un recibo sin cajero
/// (canal <see cref="ReceiptChannel.AdvanceCrossing"/>, clase <see cref="ReceiptDocumentKind.AdvanceCrossingReceipt"/>) que paga la cuota del ciclo con el medio
/// del anticipo, hasta lo disponible (abono parcial si no alcanza), y baja el anticipo de lo más antiguo a lo más nuevo.
/// <list type="bullet">
/// <item>Recaudo no conoce Transporte ni Comercial: sólo ve la línea, el crédito y su deudor (<see cref="IBusquedaDeObligaciones"/>).</item>
/// <item>Los mensajes del cruce (el recibo a Contabilidad, el <c>PagoAplicado</c> de Cartera) los origina quien facturó, el usuario de origen del aviso
/// (<see cref="OrigenDelConsumo"/>, T11). Sin él, no se cruza.</item>
/// <item>Lo que impide el cruce —sin tipo de documento, sin el usuario de origen, el contrato que rechaza— no se reintenta: alerta
/// <c>Recaudo.CruceDeAnticipoPendiente</c> y el anticipo queda intacto para cruzarlo en la caja.</item>
/// <item>Repetido, sin efecto (recibo único en <c>RCD_InboundMessages</c> y la clave del recibo por crédito y concepto).</item>
/// </list>
/// Sin ruta: lo manda <see cref="DestinoRecaudo"/>. (nuevo)
/// </summary>
public sealed record ConsumeObligationCreatedCommand(IReadOnlyList<Guid> MessagePublicIds) : IRequest<Result<ResultadoDeConsumo>>, IReintentableAnteConcurrencia;

public sealed class ConsumeObligationCreatedCommandValidator : AbstractValidator<ConsumeObligationCreatedCommand>
{
    public ConsumeObligationCreatedCommandValidator() => RuleFor(x => x.MessagePublicIds).NotEmpty();
}

public sealed class ConsumeObligationCreatedCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ICerrojoDeRecaudo cerrojo,
    IContratoDeObligaciones obligaciones,
    IBusquedaDeObligaciones busqueda,
    AplicacionNoInteractiva aplicacion,
    IAlertas alertas,
    IDateTimeService reloj) : IRequestHandler<ConsumeObligationCreatedCommand, Result<ResultadoDeConsumo>>
{
    /// <summary>El estado con que nace una obligación vigente de una venta (los demás —rechazada, migrada, cancelada antes del corte— no se cruzan).</summary>
    public const string EstadoValidada = "Validated";

    /// <summary>El código con que queda el recibo del aviso cuando el cruce no se pudo hacer.</summary>
    public const string CodigoPendiente = "Receipts.AdvanceCrossing.Pending";

    /// <summary>La clave del recibo de cruce de un crédito con un concepto (la del contrato lleva el prefijo de Recaudo).</summary>
    public static string Clave(Guid credito, Guid concepto) => string.Create(CultureInfo.InvariantCulture, $"{ClavesDeRecaudo.Codigo}:cruce:{credito:N}:{concepto:N}");

    public async Task<Result<ResultadoDeConsumo>> Handle(ConsumeObligationCreatedCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Receipts, ct);
        if (leidos.IsFailure) return Result.Failure<ResultadoDeConsumo>(leidos.Error);
        var mensaje = leidos.Value[0];
        if (await db.ReceiptsInboundMessages.AsNoTracking().FirstOrDefaultAsync(r => r.MessagePublicId == mensaje.MessageId, ct) is { } previo)
            return Result.Success<ResultadoDeConsumo>(previo.Outcome == InboundMessageOutcome.Processed
                ? new ResultadoDeConsumo.AlreadyProcessed(previo.PublicId, SinComprobante: MotivoSinComprobante.Informational)
                : new ResultadoDeConsumo.Rejected(previo.Code ?? "Integration.VersionNotAccepted", previo.Detail ?? "El aviso fue rechazado al recibirlo."));
        if (mensaje.Payload is not ObligacionCreadaV1 obligacion || mensaje.Version != 1)
            return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Rejected("Integration.VersionNotAccepted", $"Recaudo no acepta {mensaje.Type} v{mensaje.Version}."));

        return await TransaccionExplicita.EjecutarAsync(db, () => CruzarAsync(mensaje, obligacion, ct), ct);
    }

    private async Task<Result<ResultadoDeConsumo>> CruzarAsync(MensajeEntrante mensaje, ObligacionCreadaV1 o, CancellationToken ct)
    {
        var credito = o.Loan.LoanPublicId;
        if (!string.Equals(o.Status, EstadoValidada, StringComparison.Ordinal) || o.Amount <= 0m || credito == Guid.Empty)
            return await SinCruceAsync(mensaje, $"La obligación {o.Loan.LoanNumber} quedó {o.Status}: no hay nada que cruzar.", ct);
        var conceptos = await AnticiposDeConcepto.QueCubrenAsync(db, o.CreditLineCode, ct);
        if (conceptos.Count == 0) return await SinCruceAsync(mensaje, $"Ningún anticipo cubre la línea {o.CreditLineCode}.", ct);

        var deudor = (await busqueda.PorNumeroAsync(new BusquedaPorNumero(o.Loan.LoanNumber), ct))
            .FirstOrDefault(x => x.Kind == ClaseDeObligacionEncontrada.Loan && x.ObligationPublicId == credito);
        var persona = deudor is null ? null : await db.People.AsNoTracking().Where(p => p.PublicId == deudor.PersonPublicId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        if (deudor is null || persona is null)
            return await PendienteAsync(mensaje, o, null, $"no se encontró el deudor del crédito {o.Loan.LoanNumber}", ct);

        var ids = conceptos.Select(c => c.Id).ToList();
        var oficina = await db.PayerCreditBalances.AsNoTracking()
            .Where(s => s.PersonId == persona && s.Status == PayerCreditBalanceState.Open && s.Balance > 0m && s.MiscConceptId != null && ids.Contains(s.MiscConceptId.Value))
            .OrderBy(s => s.OpenedOn).ThenBy(s => s.Id).Select(s => (int?)s.BranchId).FirstOrDefaultAsync(ct);
        if (oficina is null) return await SinCruceAsync(mensaje, $"{deudor.PersonName} no tiene anticipo de {string.Join(", ", conceptos.Select(c => c.Code))}.", ct);

        if (!await db.ReceiptDocumentTypes.AsNoTracking().AnyAsync(t => t.Kind == ReceiptDocumentKind.AdvanceCrossingReceipt && t.Status == ReceiptsCatalogStatus.Active, ct))
            return await PendienteAsync(mensaje, o, oficina, "no hay un tipo de documento activo de la clase «Cruce de anticipo» en la configuración de Recaudo", ct);
        if (mensaje.Envelope.OriginUser?.CentralUserId is null)
            return await PendienteAsync(mensaje, o, oficina, "el aviso de Cartera no dice quién facturó, y el cruce se registra a su nombre", ct);

        using var origen = OrigenDelConsumo.Usar(mensaje.Envelope.OriginUser);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync([persona.Value], ct);
        var hoy = reloj.HoyLocal;
        var recibos = new List<Receipt>();
        foreach (var concepto in conceptos)
        {
            // Bajo el cerrojo del deudor: lo disponible no cambia hasta el final.
            var saldos = await db.PayerCreditBalances.AsNoTracking()
                .Where(s => s.PersonId == persona && s.MiscConceptId == concepto.Id && s.Status == PayerCreditBalanceState.Open && s.Balance > 0m)
                .OrderBy(s => s.OpenedOn).ThenBy(s => s.Id).ToListAsync(ct);
            var disponible = saldos.Sum(s => s.Balance);
            if (disponible <= 0m) continue;

            // Lo que la cuota del ciclo admite: lo que sobra de la simulación no se cruza (seguiría como anticipo).
            var sim = await obligaciones.SimularAsync(new SolicitudDeSimulacion(ClavesDeRecaudo.Recaudador, credito, ModalidadDePago.CycleInstallment, disponible, hoy), ct);
            if (sim.Result == ResultadoDelContrato.Rejected || sim.Value is null)
                return await PendienteAsync(mensaje, o, oficina, $"Cartera no admitió el pago ({sim.Code}: {sim.Message})", ct, recibos);
            var valor = disponible - Math.Max(0m, sim.Value.Surplus);
            if (valor <= 0m) continue;

            var medio = await db.PaymentMeans.AsNoTracking().FirstAsync(m => m.Id == concepto.AdvanceMeansId, ct);
            var clave = Clave(credito, concepto.PublicId);
            var r = await aplicacion.AplicarAsync(new LineaNoInteractiva(clave, ReceiptChannel.AdvanceCrossing, deudor.PersonPublicId, hoy, valor, saldos[0].BranchId,
                [new ParteNoInteractiva(ApplicationTarget.Loan, credito, deudor.PersonPublicId, valor, LoanModality: ModalidadDePago.CycleInstallment, Key: clave)],
                ClavesDeRecaudo.ClaseDelContrato(medio.Class), medio.Class, MeansReference: o.Loan.LoanNumber, CerrojosTomados: true, PaymentMeansId: medio.Id), ct);
            if (r.Outcome == ProcessingLineOutcome.Rejected)
                return await PendienteAsync(mensaje, o, oficina, $"el recibo de cruce no se aplicó ({r.Code}: {r.Message})", ct, recibos);
            if (r.Outcome == ProcessingLineOutcome.Applied && r.Receipt is not null) recibos.Add(r.Receipt);
        }

        var detalle = recibos.Count == 0
            ? $"El anticipo ya cubría la cuota del crédito {o.Loan.LoanNumber}: nada que cruzar."
            : string.Create(CultureInfo.InvariantCulture,
                $"Cruzados {recibos.Sum(r => r.TotalApplied):N0} al crédito {o.Loan.LoanNumber} con {string.Join(", ", recibos.Select(r => r.Number))}.");
        return await SinCruceAsync(mensaje, detalle, ct);
    }

    /// <summary>El aviso queda procesado con su detalle (con o sin cruce).</summary>
    private async Task<Result<ResultadoDeConsumo>> SinCruceAsync(MensajeEntrante m, string detalle, CancellationToken ct, string? codigo = null)
    {
        db.ReceiptsInboundMessages.Add(Recibo(m, InboundMessageOutcome.Processed, codigo, detalle));
        await db.SaveChangesAsync(ct);
        return Result.Success<ResultadoDeConsumo>(new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational));
    }

    /// <summary>
    /// El cruce no se pudo hacer: alerta para cruzarlo en la caja y el aviso queda procesado (no se reintenta). Lo que ya se cruzó con otro concepto en este aviso
    /// queda (<paramref name="hechos"/>).
    /// </summary>
    private async Task<Result<ResultadoDeConsumo>> PendienteAsync(MensajeEntrante m, ObligacionCreadaV1 o, int? oficina, string motivo, CancellationToken ct,
        IReadOnlyList<Receipt>? hechos = null)
    {
        var ya = hechos is { Count: > 0 } ? $" Ya se cruzó con {string.Join(", ", hechos.Select(r => r.Number))}." : string.Empty;
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.RecaudoCruceDeAnticipoPendiente, "Anticipo por cruzar en la caja",
            $"El crédito {o.Loan.LoanNumber} (línea {o.CreditLineCode}) tiene un anticipo que lo cubre, pero no se cruzó solo: {motivo}. Crúcelo en la caja.{ya}",
            "Loan", o.Loan.LoanPublicId, DedupKey: $"{TiposDeAlerta.RecaudoCruceDeAnticipoPendiente}:{o.Loan.LoanPublicId:N}", ModuloDeOrigen: "Receipts",
            ScopeBranchPublicId: oficina is { } id ? await db.Branches.AsNoTracking().Where(b => b.Id == id).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct) : null), ct);
        return await SinCruceAsync(m, $"Sin cruzar: {motivo}.{ya}", ct, CodigoPendiente);
    }

    private ReceiptsInboundMessage Recibo(MensajeEntrante m, InboundMessageOutcome resultado, string? codigo, string? detalle) => new()
    {
        MessagePublicId = m.MessageId, Type = m.Type, Version = (short)m.Version, OriginModule = m.OriginModule, ReceivedAt = reloj.UtcNow, Outcome = resultado, Code = codigo,
        Detail = detalle is { Length: > 1000 } ? detalle[..1000] : detalle,
    };
}
