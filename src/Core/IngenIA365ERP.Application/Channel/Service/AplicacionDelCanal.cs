using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Channel;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Channel;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Channel.Service;

/// <summary>Un pago aprobado del canal, ya con la persona y el destino del ERP.</summary>
public sealed record PagoDelCanal(
    string Reference,
    Person Persona,
    ChannelPaymentDestination Destino,
    decimal Total,
    DateTimeOffset ApprovedAt,
    AprobacionDeLaPasarelaDto Gateway,
    IReadOnlyList<ItemDelAvisoDto> Items,
    PagadorDelAvisoDto? Payer);

/// <summary>Lo que quedó: el aviso guardado, el recibo si lo hubo y la respuesta para el canal.</summary>
public sealed record ResultadoDelPagoDelCanal(ChannelPaymentNotice Aviso, Receipt? Recibo, bool EnRevision, ChannelPaymentNoticeResultDto Respuesta);

/// <summary>
/// Feature 021 (T100–T102, T112): aplica un pago aprobado del canal como recibo «en línea» de Recaudo y guarda su <c>CDG_PaymentNotices</c>. La usan el aviso
/// del canal (a nombre del asociado) y la conciliación del archivo de la pasarela cuando un aprobado nunca se avisó (a nombre del analista que carga, FR-174
/// sin excepción). Las dos entran con la misma llave del recibo (<see cref="LlaveDelRecibo"/>, research R4): el orden entre aviso y archivo no importa. Un pago
/// aprobado nunca se rechaza: lo que no se aplica queda a favor del asociado y el aviso sale en revisión (FR-046). No abre transacción: corre en la de quien
/// llama. (nuevo)
/// </summary>
public sealed class AplicacionDelCanal(
    IApplicationDbContext db,
    AplicacionNoInteractiva aplicacion,
    IContratoDeObligaciones obligaciones,
    IContratoDeCuentas cuentas,
    IAlertas alertas,
    IDateTimeService reloj,
    IngenIA365ERP.Application.Common.Integration.Accounting.IContabilidadParaRecaudo? contabilidad = null)
{
    public const string Aplicado = "Applied";
    public const string EnRevision = "InReview";

    /// <summary>Colombia no tiene horario de verano.</summary>
    private static readonly TimeSpan HoraDeColombia = TimeSpan.FromHours(-5);

    /// <summary>
    /// La llave del recibo en línea (research R4): <c>GW:{convenio:N}:{referencia}</c>, la misma con que la conciliación del archivo de la pasarela aplica
    /// la línea de esa referencia, así el orden entre aviso y archivo no importa.
    /// </summary>
    public static string LlaveDelRecibo(Guid convenio, string referencia) =>
        string.Create(CultureInfo.InvariantCulture, $"GW:{convenio:N}:{referencia}");

    /// <summary>PSE es transferencia; lo demás (tarjeta) va como tarjeta de crédito: la pasarela no distingue débito de crédito.</summary>
    public static bool EsPse(AprobacionDeLaPasarelaDto g) =>
        (g.Franchise?.Contains("PSE", StringComparison.OrdinalIgnoreCase) ?? false)
        || string.Equals(g.PaymentMethodName, "PSE", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Aplica y guarda el aviso. <paramref name="notaInicial"/> va al motivo (la conciliación anota que el pago entró por el archivo). Con
    /// <paramref name="cerrojosTomados"/>, quien llama ya bloqueó la configuración y al pagador (la tanda del archivo).
    /// </summary>
    public async Task<ResultadoDelPagoDelCanal> AplicarAsync(PagoDelCanal p, string? notaInicial, bool cerrojosTomados, CancellationToken ct)
    {
        var convenio = await db.CollectionAgreements.AsNoTracking().FirstAsync(c => c.Id == p.Destino.CollectionAgreementId, ct);
        var config = await Common.ConfiguracionDelCanal.VigenteAsync(db, reloj.UtcNow, ct);
        var politica = config?.SurplusPolicy ?? SurplusPolicy.AdvanceElseCreditBalance;
        var fecha = DateOnly.FromDateTime(p.ApprovedAt.ToOffset(HoraDeColombia).DateTime);
        var esPse = EsPse(p.Gateway);
        var medioDelContrato = esPse ? ClaseDeMedioDeRecaudo.Transfer : ClaseDeMedioDeRecaudo.CreditCard;

        var items = p.Items.Select(Resolver).ToList();
        var reparto = await ExcedenteDelAviso.RepartirAsync(items, p.Persona.PublicId, fecha, medioDelContrato, politica, obligaciones, cuentas, ct,
            (documento, c) => FacturaAsync(p.Persona.PublicId, documento, c));

        Receipt? recibo = null;
        var notas = new List<string>();
        if (notaInicial is not null) notas.Add(notaInicial);
        notas.AddRange(reparto.Notas);
        // En revisión sólo si algo no se aplicó a un producto (saldo a favor, parte rechazada) o no hubo recibo; un anticipo es una aplicación.
        var enRevision = true;
        if (reparto.Partes.Count > 0)
        {
            var r = await aplicacion.AplicarAsync(new LineaNoInteractiva(
                LlaveDelRecibo(convenio.PublicId, p.Reference), ReceiptChannel.Online, p.Persona.PublicId, fecha, p.Total, convenio.BranchId,
                reparto.Partes, medioDelContrato, esPse ? PaymentMeansClass.Transfer : PaymentMeansClass.CreditCard, convenio.OwnBankAccountPublicId,
                Recortar($"{p.Reference} · {p.Gateway.Authorization}", 60), CerrojosTomados: cerrojosTomados), ct);
            if (r.Outcome is ProcessingLineOutcome.Applied or ProcessingLineOutcome.AlreadyApplied && r.Receipt is not null)
            {
                recibo = r.Receipt;
                notas.AddRange(r.RejectedParts.Select(x => $"Una parte no se aplicó ({x.Message}); quedó a favor del asociado."));
                enRevision = r.CreditBalance is not null || r.RejectedParts.Count > 0;
            }
            else
            {
                notas.Add($"Recaudo no pudo aplicar el pago ({r.Message ?? r.Code}); la cooperativa debe registrarlo a mano.");
            }
        }
        else
        {
            notas.Add("Ningún producto admitió el pago; la cooperativa debe registrarlo y devolverlo o reubicarlo.");
        }

        var clases = await ClasesDeCuentaAsync(p.Persona.PublicId, fecha, p.Items, ct);
        var aviso = new ChannelPaymentNotice
        {
            Reference = p.Reference, PersonId = p.Persona.Id, DestinationId = p.Destino.Id, Total = p.Total,
            GatewayRequestId = p.Gateway.RequestId, InternalReference = p.Gateway.InternalReference,
            Authorization = Recortar(p.Gateway.Authorization, 60), GatewayReceipt = Recortar(p.Gateway.Receipt, 60),
            Franchise = Recortar(p.Gateway.Franchise, 30), PaymentMethodName = Recortar(p.Gateway.PaymentMethodName, 80),
            IssuerName = Recortar(p.Gateway.IssuerName, 120), ApprovedAt = p.ApprovedAt.UtcDateTime,
            PayerIp = Recortar(p.Payer?.Ip, 46), PayerUserAgent = Recortar(p.Payer?.UserAgent, 255), ChannelAccountId = p.Payer?.AccountId,
            Outcome = enRevision ? NoticeOutcome.InReview : NoticeOutcome.Applied,
            OutcomeReason = notas.Count > 0 ? Recortar(string.Join(" ", notas), 300) : null,
            ReceiptId = recibo?.Id,
            Items = p.Items.Select(i => new ChannelPaymentNoticeItem
            {
                ConceptKind = ClaseDelItem(i, clases), ProductPublicId = ClaveDeConcepto.Leer(i.ConceptKey)!.Value.Producto, Modality = i.Modality, Amount = i.Amount,
                PrepaymentChoice = i.PrepaymentChoice switch
                {
                    "ReduceTerm" => ChannelPrepaymentChoice.ReduceTerm,
                    "ReduceInstallment" => ChannelPrepaymentChoice.ReduceInstallment,
                    _ => null,
                },
            }).ToList(),
        };
        db.ChannelPaymentNotices.Add(aviso);
        await db.SaveChangesAsync(ct);

        if (enRevision)
        {
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.CanalPagoEnRevision, "Pago en línea en revisión",
                $"El pago en línea {p.Reference} por {p.Total.ToString("N0", CultureInfo.GetCultureInfo("es-CO"))} pesos quedó en revisión: {aviso.OutcomeReason}",
                "ChannelPaymentNotice", aviso.PublicId, DedupKey: $"Canal.PagoEnRevision:{aviso.PublicId:N}", ModuloDeOrigen: "Channel",
                ScopeBranchPublicId: await db.Branches.AsNoTracking().Where(b => b.Id == convenio.BranchId).Select(b => (Guid?)b.PublicId).FirstOrDefaultAsync(ct)), ct);
        }
        return new ResultadoDelPagoDelCanal(aviso, recibo, enRevision,
            new ChannelPaymentNoticeResultDto(enRevision ? EnRevision : Aplicado, recibo?.PublicId, recibo?.Number, aviso.OutcomeReason));
    }

    /// <summary>La respuesta de un aviso ya guardado (repetición, FR-044).</summary>
    public async Task<ChannelPaymentNoticeResultDto> RespuestaAsync(ChannelPaymentNotice n, CancellationToken ct)
    {
        var recibo = n.ReceiptId is { } id
            ? await db.IssuedReceipts.AsNoTracking().Where(r => r.Id == id).Select(r => new { r.PublicId, r.Number }).FirstOrDefaultAsync(ct)
            : null;
        return new ChannelPaymentNoticeResultDto(n.Outcome == NoticeOutcome.Applied ? Aplicado : EnRevision, recibo?.PublicId, recibo?.Number, n.OutcomeReason);
    }

    /// <summary>Feature 021, C2b: la factura del comprobante contable y su saldo pendiente hoy (nula si ya no tiene saldo o no hay Contabilidad).</summary>
    private async Task<(string Factura, decimal Pendiente)?> FacturaAsync(Guid persona, Guid documento, CancellationToken ct)
    {
        if (contabilidad is null) return null;
        var f = (await Receipts.CashDesk.CuentasPorCobrarDeRecaudo.PendientesAsync(db, contabilidad, persona, ct))
            .FirstOrDefault(x => x.AccountingDocumentPublicId == documento);
        return f is null ? null : (f.InvoiceNumber, f.Pending);
    }

    private static ExcedenteDelAviso.ItemResuelto Resolver(ItemDelAvisoDto i)
    {
        var clave = ClaveDeConcepto.Leer(i.ConceptKey)!.Value;
        var (esCredito, producto) = clave;
        EleccionDeAbono? eleccion = i.PrepaymentChoice switch
        {
            "ReduceTerm" => EleccionDeAbono.ReduceTerm,
            "ReduceInstallment" => EleccionDeAbono.ReduceInstallment,
            _ => null,
        };
        return new ExcedenteDelAviso.ItemResuelto(esCredito, producto, i.Modality, i.Amount, ModosDelCanal.DeCredito(i.Modality),
            ModosDelCanal.DeCuenta(i.Modality), eleccion, clave.EsFactura);
    }

    /// <summary>La clase del concepto del aviso: crédito, o la de la cuenta según Captaciones (aportes si no la encuentra).</summary>
    private static ConceptKind ClaseDelItem(ItemDelAvisoDto i, IReadOnlyDictionary<Guid, ConceptKind> clases)
    {
        var clave = ClaveDeConcepto.Leer(i.ConceptKey)!.Value;
        if (clave.EsFactura) return ConceptKind.CommercialReceivable;
        return clave.EsCredito ? ConceptKind.Loan : clases.GetValueOrDefault(clave.Producto, ConceptKind.Contribution);
    }

    private async Task<IReadOnlyDictionary<Guid, ConceptKind>> ClasesDeCuentaAsync(Guid persona, DateOnly fecha, IReadOnlyList<ItemDelAvisoDto> items,
        CancellationToken ct)
    {
        if (!items.Any(i => ClaveDeConcepto.Leer(i.ConceptKey)!.Value.EsCuenta)) return new Dictionary<Guid, ConceptKind>();
        var r = await cuentas.ConsultarAsync(new SolicitudDeConsultaDeCuentas(ClavesDeRecaudo.Recaudador, persona, fecha), ct);
        return (r.Value?.Accounts ?? [])
            .Select(c => (c.AccountPublicId, Clase: ClaveDeConcepto.ClaseDeCuenta(c.Nature, c.DepositClass)))
            .Where(c => c.Clase is not null)
            .ToDictionary(c => c.AccountPublicId, c => c.Clase!.Value);
    }

    private static string? Recortar(string? v, int n) => string.IsNullOrWhiteSpace(v) ? null : v.Length <= n ? v : v[..n];
}
