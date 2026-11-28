using System.Globalization;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Integration;

/// <summary>
/// <b>La recogida de facturas a crédito de Comercial en el desembolso</b> (2026-11-27, pedido del dueño; feature 028, HU7, T085–T087; FR-026; research R14,
/// E-16): la implementación de Recaudo de <see cref="IRecogidaEnRecaudo"/>. Las facturas son saldo de Contabilidad por tercero y documento cruce que sólo se cobra con
/// un recibo de Recaudo; el crédito nuevo las paga con un <b>recibo sin caja</b> del canal <see cref="ReceiptChannel.LoanDisbursement"/> que arma
/// <see cref="AplicacionNoInteractiva"/> —la de las partidas, el giro de una pagaduría, el canal y el cruce de anticipos—, con el medio del cruce interno
/// (<see cref="PaymentMeansClass.InternalOffset"/>: no entra dinero) y una aplicación a cuenta por cobrar por factura, en la transacción del desembolso:
/// <list type="bullet">
/// <item><b>Cerrojos</b>: el desembolso llama <see cref="TomarCerrojosAsync"/> antes de los suyos (la configuración de Recaudo y el deudor como pagador; el orden
/// global de la 015 pone Recaudo antes que los contratos de Cartera) y la aplicación corre con <c>CerrojosTomados</c>. Sin eso, el desembolso (con el deudor y los
/// créditos de Cartera tomados) y un recibo de caja del mismo deudor (con su pagador tomado, esperando a Cartera) se interbloquearían.</item>
/// <item><b>Numeración</b>: el recibo numera con su propio tipo de documento (clase <see cref="ReceiptDocumentKind.LoanDisbursementReceipt"/>), que sólo usa el
/// desembolso: ningún otro camino tiene esa fila de numeración y no hay ciclo con las de Cartera.</item>
/// <item><b>El saldo</b> de cada factura se relee bajo el cerrojo del pagador: si ya no existe o no alcanza, la recogida falla y el desembolso no existe (el desembolso
/// ya lo vio en su relectura bajo el cerrojo: es la defensa, como <c>AppliedDiffers</c>).</item>
/// <item><b>El hecho</b> es <c>ReciboEmitido</c> (como la caja): Contabilidad acredita <c>CuentasPorCobrarComerciales</c> con el tercero y la factura, y debita el rol
/// del medio, <c>RecogidaEnElDesembolso</c>; Transporte, destino opcional, marca pagado el cargo de la factura.</item>
/// <item><b>La anulación</b> (<see cref="ReversarAsync"/>) es la de la 015 sin aprobación (<see cref="AplicacionDeAnulacion"/>, como el reverso del canal), con el
/// espejo exacto del recibo (<see cref="DineroDeLaAnulacion.SinDinero"/>); el desembolso la hace antes de sus cerrojos, porque la anulación numera con el
/// consecutivo de las anulaciones, que también usa la caja antes de llegar a Cartera. Desde la caja este recibo no se anula
/// (<see cref="SeAnulaConElCredito"/>).</item>
/// </list>
/// Valida y audita sus escrituras (<c>LosPuertosDeRecaudoYTesoreriaValidanYAuditanSusEscrituras</c>). No conoce a Cartera: sólo el puerto. (nuevo)
/// </summary>
public sealed class RecogidaEnRecaudo(
    IApplicationDbContext db,
    ICerrojoDeRecaudo cerrojo,
    IContabilidadParaRecaudo contabilidad,
    AplicacionNoInteractiva aplicacion,
    AplicacionDeAnulacion anulaciones,
    IActorActual actorActual,
    AuditoriaDeRecaudo auditoria,
    IDateTimeService reloj) : IRecogidaEnRecaudo
{
    public const string EventoAplicada = "Receipts.LoanPayoff.Applied";
    public const string EventoAnulada = "Receipts.LoanPayoff.Voided";

    public const string CodigoSinConfigurar = "Receipts.LoanPayoff.NotReady";
    public const string CodigoFacturaCambio = "Receipts.LoanPayoff.InvoiceChanged";
    public const string CodigoNoAplicada = "Receipts.LoanPayoff.NotApplied";
    public const string CodigoSeAnulaConElCredito = "Receipts.LoanPayoff.VoidWithTheLoan";

    /// <summary>El recibo de una recogida no se anula desde la caja: se anula anulando el crédito nuevo antes del giro.</summary>
    public static Error SeAnulaConElCredito(string recibo) => new ErrorConDatos(CodigoSeAnulaConElCredito,
        $"El recibo {recibo} lo emitió el desembolso de un crédito que recogió facturas: se anula anulando ese crédito antes del giro, no desde la caja.",
        new { receipt = recibo });

    public async Task<FacturasParaRecoger> PendientesAsync(Guid persona, CancellationToken ct)
    {
        if (persona == Guid.Empty) return FacturasParaRecoger.Ninguna;
        var pendientes = await CuentasPorCobrarDeRecaudo.PendientesAsync(db, contabilidad, persona, ct);
        var facturas = pendientes.OrderBy(f => f.Date).ThenBy(f => f.InvoiceNumber, StringComparer.Ordinal)
            .Select(f => new FacturaPendienteEnRecaudo(f.InvoiceNumber, f.AccountingDocumentPublicId, f.Date, f.Pending)).ToList();
        return new FacturasParaRecoger(facturas, facturas.Count == 0 ? null : await SinConfigurarAsync(ct));
    }

    public async Task TomarCerrojosAsync(Guid persona, IReadOnlyList<Guid> recibos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recibos);
        var pagador = await db.People.AsNoTracking().Where(p => p.PublicId == persona).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearPagadoresAsync(pagador is { } id ? [id] : [], ct);
        if (recibos.Count == 0) return;
        var ids = await db.IssuedReceipts.AsNoTracking().Where(r => recibos.Contains(r.PublicId)).Select(r => r.Id).ToListAsync(ct);
        if (ids.Count > 0) await cerrojo.BloquearAsync(NivelDeRecaudo.Origenes, TablasDeRecaudo.Recibos, ids.Order().ToList(), ct: ct);
    }

    public async Task<Result<ReciboDeLaRecogida>> AplicarAsync(SolicitudDeRecogidaEnRecaudo solicitud, CancellationToken ct)
    {
        ValidarSolicitud(solicitud);
        if (await SinConfigurarAsync(ct) is { } motivo) return Result.Failure<ReciboDeLaRecogida>(new Error(CodigoSinConfigurar, motivo));

        // Repetida (el desembolso es idempotente por su clave): el recibo que ya se emitió.
        var clave = AplicacionNoInteractiva.ClaveDelRecibo(solicitud.Key);
        if (await db.IssuedReceipts.AsNoTracking().FirstOrDefaultAsync(r => r.OperationKey == clave, ct) is { } previo)
            return Result.Success(await ReciboAsync(previo, ct));

        // Bajo el cerrojo del pagador: el saldo de cada factura no cambia hasta el final. La que ya no existe o no alcanza detiene la recogida.
        var pendientes = (await CuentasPorCobrarDeRecaudo.PendientesAsync(db, contabilidad, solicitud.DebtorPersonPublicId, ct))
            .ToDictionary(f => f.InvoiceNumber, StringComparer.Ordinal);
        foreach (var f in solicitud.Invoices)
        {
            var pendiente = pendientes.TryGetValue(f.InvoiceNumber.Trim(), out var cxc) ? cxc.Pending : 0m;
            if (pendiente < f.Amount)
                return Result.Failure<ReciboDeLaRecogida>(new ErrorConDatos(CodigoFacturaCambio,
                    string.Create(CultureInfo.GetCultureInfo("es-CO"),
                        $"La factura {f.InvoiceNumber} tiene un saldo de {pendiente:C0} y la recogida le aplica {f.Amount:C0}: cambió desde la vista previa. Vuelva a revisar el desembolso."),
                    new { invoice = f.InvoiceNumber, pending = pendiente, amount = f.Amount }));
        }

        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.PublicId == solicitud.BranchPublicId).Select(b => (int?)b.Id).FirstOrDefaultAsync(ct);
        if (oficina is null) return Result.Failure<ReciboDeLaRecogida>(ErroresDeRecaudo.NoEncontrado("La oficina del crédito nuevo"));
        var medio = (await MedioAsync(ct))!.Value;
        var referencia = string.IsNullOrWhiteSpace(solicitud.NewLoanNumber)
            ? solicitud.DisbursementNumber
            : $"{solicitud.DisbursementNumber} · {solicitud.NewLoanNumber}";
        var linea = new LineaNoInteractiva(solicitud.Key, ReceiptChannel.LoanDisbursement, solicitud.DebtorPersonPublicId, solicitud.ValueDate,
            solicitud.Invoices.Sum(f => f.Amount), oficina.Value,
            solicitud.Invoices.Select(f => new ParteNoInteractiva(ApplicationTarget.CommercialReceivable, null, solicitud.DebtorPersonPublicId, f.Amount,
                CrossDocument: f.InvoiceNumber.Trim(), Key: f.Key)).ToList(),
            ClaseDeMedioDeRecaudo.InternalOffset, PaymentMeansClass.InternalOffset, MeansReference: Recortar(referencia, 80), AllowLateValueDate: false,
            CerrojosTomados: true, PaymentMeansId: medio);
        var r = await aplicacion.AplicarAsync(linea, ct);
        if (r.Outcome == ProcessingLineOutcome.Rejected || r.Receipt is null)
            return Result.Failure<ReciboDeLaRecogida>(new ErrorConDatos(string.IsNullOrWhiteSpace(r.Code) ? CodigoNoAplicada : r.Code,
                $"Recaudo no pudo emitir el recibo de la recogida de facturas: {r.Message ?? "sin respuesta"}.", new { code = r.Code }));
        // Defensa: lo que no se aplicó a una factura quedaría a favor del deudor; en una recogida no entra dinero que pueda sobrar.
        if (r.Surplus > 0m || r.CreditBalance is not null || r.RejectedParts.Count > 0)
            return Result.Failure<ReciboDeLaRecogida>(new ErrorConDatos(CodigoFacturaCambio,
                "El recibo de la recogida no aplicó todo a las facturas: cambió su saldo desde la vista previa. Vuelva a revisar el desembolso.",
                new { surplus = r.Surplus, rejected = r.RejectedParts.Count }));

        var hecho = await ReciboAsync(r.Receipt, ct);
        await auditoria.AgregarAsync(EventoAplicada, ClaseDelRecibo, r.Receipt.PublicId, null, new
        {
            receipt = hecho.Number, disbursement = solicitud.DisbursementNumber, newLoan = solicitud.NewLoanNumber, valueDate = solicitud.ValueDate,
            invoices = hecho.Invoices.Select(f => new { invoice = f.InvoiceNumber, applied = f.Applied, key = f.Key }),
            by = (await actorActual.ObtenerAsync(ct)).Name,
        }, ct);
        // El hecho y la auditoría, en la unidad de trabajo del que llama (su transacción decide).
        await db.SaveChangesAsync(ct);
        return Result.Success(hecho);
    }

    public async Task<Result<ReciboDeLaRecogidaAnulado>> ReversarAsync(Guid recibo, string motivo, CancellationToken ct)
    {
        ValidarReverso(recibo, motivo);
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReciboDeLaRecogidaAnulado>(ErroresDeRecaudo.NoEncontrado("El usuario"));
        var leido = await db.IssuedReceipts.FirstOrDefaultAsync(r => r.PublicId == recibo, ct);
        if (leido is null) return Result.Failure<ReciboDeLaRecogidaAnulado>(ErroresDeRecaudo.NoEncontrado("El recibo de la recogida"));
        if (leido.Channel != ReceiptChannel.LoanDisbursement)
            return Result.Failure<ReciboDeLaRecogidaAnulado>(new Error(CodigoNoAplicada, $"El recibo {leido.Number} no es de una recogida en el desembolso."));

        // Repetida (la anulación del crédito se reintenta): la anulación que ya tiene.
        if (await db.ReceiptVoidings.AsNoTracking().FirstOrDefaultAsync(v => v.ReceiptId == leido.Id && v.Status == ReceiptVoidingState.Applied, ct) is { } hecha)
            return Result.Success(new ReciboDeLaRecogidaAnulado(leido.PublicId, hecha.PublicId, hecha.Number ?? string.Empty));
        if (leido.Status != ReceiptState.Valid) return Result.Failure<ReciboDeLaRecogidaAnulado>(ErroresDeRecaudo.AnulacionYaHecha(leido.Number));

        var documento = new ReceiptVoiding
        {
            ReceiptId = leido.Id, Reason = Recortar($"Anulación del crédito que recogió las facturas: {motivo.Trim()}", 500), RequestedByUserId = usuario,
            RequestedAt = reloj.UtcNow, InSameSession = false, VerificationCode = CloseCollectionPointDayCommandHandler.CodigoDeVerificacion(),
        };
        db.ReceiptVoidings.Add(documento);
        var aplicada = await anulaciones.AplicarAsync(documento, leido, null, usuario, "Anulación del crédito nuevo antes del giro (recogida de facturas)",
            DineroDeLaAnulacion.SinDinero, ct);
        if (aplicada.IsFailure) return Result.Failure<ReciboDeLaRecogidaAnulado>(aplicada.Error);
        await auditoria.AgregarAsync(EventoAnulada, ClaseDelRecibo, leido.PublicId, new { status = nameof(ReceiptState.Valid) },
            new { status = nameof(ReceiptState.Voided), receipt = leido.Number, voiding = documento.Number, reason = documento.Reason, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(new ReciboDeLaRecogidaAnulado(leido.PublicId, documento.PublicId, documento.Number ?? string.Empty));
    }

    /// <summary>La clase de la entidad en la auditoría.</summary>
    public const string ClaseDelRecibo = "Receipt";

    /// <summary>
    /// Por qué Recaudo no puede emitir el recibo de la recogida (nulo si puede): falta un tipo de documento activo de la clase «Recogida en el desembolso» (lo crea la
    /// cooperativa en Tipos de documento, como el del cruce de anticipos) o el medio del cruce interno (lo deja la semilla).
    /// </summary>
    public async Task<string?> SinConfigurarAsync(CancellationToken ct)
    {
        if (!await db.ReceiptDocumentTypes.AsNoTracking()
                .AnyAsync(t => t.Kind == ReceiptDocumentKind.LoanDisbursementReceipt && t.Status == ReceiptsCatalogStatus.Active, ct))
            return "Recaudo no tiene un tipo de documento activo de la clase «Recogida en el desembolso»: créelo en Recaudo → Configuración → Tipos de documento.";
        if (await MedioAsync(ct) is null)
            return "Recaudo no tiene activo el medio de pago del cruce interno («Recogida en el desembolso»): actívelo en Medios de pago.";
        return null;
    }

    /// <summary>El medio del cruce interno: el activo de la clase <see cref="PaymentMeansClass.InternalOffset"/> (lo deja la semilla), o nulo.</summary>
    private async Task<int?> MedioAsync(CancellationToken ct) =>
        await db.PaymentMeans.AsNoTracking().Where(m => m.Class == PaymentMeansClass.InternalOffset && m.IsActive).OrderBy(m => m.Id).Select(m => (int?)m.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>El recibo de la recogida como lo ve el desembolso: su número y lo aplicado a cada factura con la clave de su porción.</summary>
    private async Task<ReciboDeLaRecogida> ReciboAsync(Receipt recibo, CancellationToken ct)
    {
        var filas = await (from c in db.CommercialReceivableApplications.AsNoTracking()
                           join p in db.ReceiptApplicationPortions.AsNoTracking() on c.ReceiptApplicationId equals p.ReceiptApplicationId
                           where c.ReceiptId == recibo.Id
                           orderby c.ReceiptApplicationId
                           select new { c.InvoiceNumber, c.Amount, p.IdempotencyKey }).ToListAsync(ct);
        return new ReciboDeLaRecogida(recibo.PublicId, recibo.Number,
            filas.Select(f => new FacturaRecogidaEnRecaudo(f.InvoiceNumber, f.Amount, f.IdempotencyKey)).ToList());
    }

    /// <summary>La forma de lo que pide el desembolso (lo demás lo revisa la aplicación bajo el cerrojo).</summary>
    public static void ValidarSolicitud(SolicitudDeRecogidaEnRecaudo solicitud)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        if (string.IsNullOrWhiteSpace(solicitud.Key)) throw new ArgumentException("La recogida lleva la clave de su recibo.", nameof(solicitud));
        if (solicitud.DebtorPersonPublicId == Guid.Empty) throw new ArgumentException("La recogida lleva el deudor que paga.", nameof(solicitud));
        if (solicitud.BranchPublicId == Guid.Empty) throw new ArgumentException("La recogida lleva la oficina del crédito nuevo.", nameof(solicitud));
        if (solicitud.Invoices.Count == 0) throw new ArgumentException("La recogida lleva al menos una factura.", nameof(solicitud));
        if (solicitud.Invoices.Any(f => string.IsNullOrWhiteSpace(f.InvoiceNumber) || string.IsNullOrWhiteSpace(f.Key) || f.Amount <= 0m))
            throw new ArgumentException("Cada factura lleva su número, su clave y un valor mayor que cero.", nameof(solicitud));
        if (solicitud.Invoices.GroupBy(f => f.InvoiceNumber.Trim(), StringComparer.Ordinal).Any(g => g.Count() > 1))
            throw new ArgumentException("Una factura está dos veces en la recogida.", nameof(solicitud));
    }

    private static void ValidarReverso(Guid recibo, string motivo)
    {
        if (recibo == Guid.Empty) throw new ArgumentException("Diga qué recibo se anula.", nameof(recibo));
        if (string.IsNullOrWhiteSpace(motivo)) throw new ArgumentException("La anulación lleva su motivo.", nameof(motivo));
    }

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];
}
