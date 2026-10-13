using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using IngenIA365ERP.Domain.Lending.Reversos;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payments;

/// <summary>
/// El punto de enganche para anular los documentos emitidos (paz y salvo, certificados) cuyo corte es igual o posterior al pago reversado
/// (feature 013, US5, T0543; FR-080; <c>VigenciaDeDocumentos</c>). Vacío hasta la fase de US13, que lo conecta. (nuevo)
/// </summary>
public interface IVigenciaDeDocumentosDeCartera
{
    /// <summary>Los documentos del crédito que dejan de valer desde <paramref name="desde"/> (en la vista previa, sin anular).</summary>
    Task<IReadOnlyList<(Guid PublicId, string Kind, string Number)>> AfectadosAsync(int loanId, DateOnly desde, CancellationToken ct);

    /// <summary>Anula esos documentos con la operación de reverso; devuelve sus <c>PublicId</c>.</summary>
    Task<IReadOnlyList<Guid>> AnularAsync(int loanId, DateOnly desde, LoanOperation reverso, CancellationToken ct);
}

/// <summary>Sin documentos que anular (hasta US13). (nuevo)</summary>
public sealed class SinDocumentosQueAnular : IVigenciaDeDocumentosDeCartera
{
    public Task<IReadOnlyList<(Guid PublicId, string Kind, string Number)>> AfectadosAsync(int loanId, DateOnly desde, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<(Guid, string, string)>>([]);

    public Task<IReadOnlyList<Guid>> AnularAsync(int loanId, DateOnly desde, LoanOperation reverso, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);
}

/// <summary>Lo que pide un reverso: el pago, el motivo, quién lo inicia (<c>Collector</c> o <c>Lending</c>), su clave y el recibo que lo anula. (nuevo)</summary>
public sealed record ReversoEnCurso(LoanOperation Pago, string Motivo, string IniciadoPor, string Clave, ReferenciaDeRecibo? Recibo = null);

/// <summary>Un pago redistribuido por el reverso: su operación y lo que cubría antes y después. (nuevo)</summary>
public sealed record PagoRedistribuido(Guid OperationPublicId, string Number, IReadOnlyList<LineaDeDistribucionDto> Antes, IReadOnlyList<LineaDeDistribucionDto> Despues);

/// <summary>
/// La vista previa de un reverso (api.md §10): la huella, lo que restituye por concepto, los pagos posteriores redistribuidos, los documentos que
/// se anularían, si el crédito se reabre y si se avisa al recaudador. (nuevo)
/// </summary>
public sealed record VistaDelReverso(
    string Huella,
    IReadOnlyDictionary<LoanConcept, decimal> PorConcepto,
    IReadOnlyList<PagoRedistribuido> Redistribuidos,
    IReadOnlyList<(Guid PublicId, string Kind, string Number)> DocumentosAAnular,
    bool Reabre,
    bool AvisaAlRecaudador,
    IReadOnlyList<LineaDeDistribucionDto> Ajustes,
    SaldosDeObligacionDto SaldosDespues,
    PlanResumidoDto? PlanDespues);

/// <summary>Un reverso registrado (o el original, si la clave ya se usó con el mismo motivo). (nuevo)</summary>
public sealed record ReversoRegistrado(LoanOperation Reverso, ComprobanteDeReversoDto Comprobante, UnidadEmitida Mensajes, bool YaReversado);

/// <summary>
/// El <b>núcleo del reverso de un pago</b> (feature 013, US5, T0543; FR-002, FR-080, FR-081, T33, C11, SC-004; interfaces §3.8, data-model §7.11):
/// lo usan <c>ReversarAsync</c> del contrato, <c>ReverseLoanPaymentCommand</c> (la pantalla de Cartera) y la anulación de un crédito con recogidas
/// (US3). Bajo el cerrojo que tomó quien llama:
/// <list type="bullet">
/// <item>un reverso no se reversa (<c>Lending.Reversal.OfReversal</c>), sólo se reversan pagos (<c>Lending.LoanOperation.NotPayment</c>) y un pago
/// tiene a lo sumo un reverso (<c>Lending.Reversal.AlreadyReversed</c>); la clave <c>{clave}:reverso</c> repetida con el mismo motivo devuelve el
/// reverso original y con otro, <c>Lending.Contract.KeyReused</c>;</item>
/// <item>la operación <c>PaymentReversal</c> con <c>ReversesOperationId</c> y, en el pago, <c>ReversedByOperationId</c> (se escribe una vez);</item>
/// <item>un espejo por cada fila propia del pago (<see cref="EspejosDelReverso.DelPago"/>, <c>ReversesTransactionId</c>) y, en los días ya causados,
/// las diferencias por familia contra la reconstrucción sin el pago (<see cref="EfectoEnElLibro"/>): los pagos posteriores redistribuidos con su
/// fecha valor y su elección (<c>PaymentReallocation</c>) y la causación (<c>AccrualAdjustment</c>/<c>SuspenseReclassification</c>), con la fecha
/// contable del período abierto (T36); lo cerrado no se reescribe;</item>
/// <item>el crédito vuelve a <c>Active</c> si el pago lo había cancelado; el plan, a la versión que deja la reconstrucción si el pago (o un posterior)
/// lo había regenerado;</item>
/// <item>la emisión de <c>PagoReversado</c> (hereda el modo del pago, C08) y, si lo inició Cartera, <c>PagoReversadoPorCartera</c>;</item>
/// <item>el enganche de <see cref="IVigenciaDeDocumentosDeCartera"/> (vacío hasta US13).</item>
/// </list>
/// (nuevo)
/// </summary>
public sealed class ReversionDePago(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    RegistroDeCartera registro,
    NumeradorDeCartera numerador,
    EmisionDeCartera emision,
    PartesDeHechosDeCartera partes,
    FechaContable fechaContable,
    AplicacionDePagos aplicacion,
    IVigenciaDeDocumentosDeCartera documentos,
    IActorActual actorActual,
    IDateTimeService reloj)
{
    public const string PrefijoDeReverso = "REV-";

    /// <summary>El método del contrato en la huella de la clave (§3.10).</summary>
    public const string MetodoReversar = "Lending.ObligationContract.Reversar";

    /// <summary>La clave derivada de la operación de reverso (§3.10).</summary>
    public static string ClaveDeReverso(string clave) => clave + IngenIA365ERP.Application.Common.Integration.ClavesDeRecaudador.SufijoReverso;

    private sealed record Calculo(Loan Credito, LoanControl Control, CreditoCargado Cargado, EfectoEnElLibro Efecto, IReadOnlyList<LoanTransaction> Originales,
        IReadOnlyList<MovimientoDeLibro> Espejos, DateOnly Marca);

    /// <summary>Las reglas de lo que se puede reversar; nulo si el pago se reversa.</summary>
    public static Error? Reversable(LoanOperation pago)
    {
        ArgumentNullException.ThrowIfNull(pago);
        if (pago.Kind == LoanOperationKind.PaymentReversal || pago.ReversesOperationId is not null) return ErroresDePagos.ReversoDeReverso();
        if (pago.Kind != LoanOperationKind.Payment) return ErroresDePagos.NoEsPago(pago.Number);
        if (pago.ReversedByOperationId is not null) return ErroresDePagos.YaReversado(pago.Number);
        return null;
    }

    /// <summary>La vista previa del reverso, sin escribir (api.md §10).</summary>
    public async Task<Result<VistaDelReverso>> PrevisualizarAsync(LoanOperation pago, CancellationToken ct)
    {
        if (Reversable(pago) is { } error) return Result.Failure<VistaDelReverso>(error);
        var calculo = await CalcularAsync(pago, null, ct);
        if (calculo.IsFailure) return Result.Failure<VistaDelReverso>(calculo.Error);
        var c = calculo.Value;
        var afectados = await documentos.AfectadosAsync(c.Credito.Id, pago.ValueDate, ct);
        return Result.Success(Vista(c, pago, afectados));
    }

    /// <summary>Reversa el pago (ver la clase). El crédito y su control ya están bloqueados por quien llama.</summary>
    public async Task<Result<ReversoRegistrado>> ReversarAsync(ReversoEnCurso r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var huella = HuellaDeOperacion.Calcular(MetodoReversar, new { payment = r.Pago.PublicId, reason = r.Motivo.Trim(), initiatedBy = r.IniciadoPor });
        var existente = await db.LoanOperations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.CollectorCode == r.Pago.CollectorCode && o.IdempotencyKey == r.Clave, ct);
        if (existente is not null)
        {
            if (existente.RequestSha256 != huella || existente.ReversesOperationId != r.Pago.Id)
                return Result.Failure<ReversoRegistrado>(ErroresDePagos.ClaveReusada(r.Clave));
            var creditoDelOriginal = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == existente.LoanId, ct);
            return Result.Success(new ReversoRegistrado(existente, await ComprobanteAsync(existente, r.Pago, creditoDelOriginal, ct), UnidadEmitida.Vacia, true));
        }
        var pago = await db.LoanOperations.FirstAsync(o => o.Id == r.Pago.Id, ct);
        if (Reversable(pago) is { } error) return Result.Failure<ReversoRegistrado>(error);

        var reverso = default(LoanOperation);
        var contable = await fechaContable.CalcularAsync(reloj.HoyLocal, ct);
        if (contable.IsFailure) return Result.Failure<ReversoRegistrado>(contable.Error);
        var actor = await actorActual.ObtenerAsync(ct);
        var control = await db.LoanControls.FirstAsync(c => c.LoanId == pago.LoanId, ct);
        var credito = await db.Loans.FirstAsync(l => l.Id == pago.LoanId, ct);
        var sucursal = await emision.SucursalContableAsync(credito.BranchId, ct);
        if (sucursal.IsFailure) return Result.Failure<ReversoRegistrado>(sucursal.Error);

        var numero = await numerador.NumeroDeOperacionAsync(LoanOperationKind.PaymentReversal, PrefijoDeReverso, ct);
        var payload = System.Text.Json.JsonSerializer.Serialize(new
        {
            payment = pago.PublicId, paymentNumber = pago.Number, reason = r.Motivo.Trim(), initiatedBy = r.IniciadoPor, collector = pago.CollectorCode,
        });
        reverso = registro.RegistrarOperacion(control, reloj.UtcNow, sello => new LoanOperation
        {
            LoanId = credito.Id,
            Kind = LoanOperationKind.PaymentReversal,
            Number = numero,
            Sequence = sello.Sequence,
            ValueDate = pago.ValueDate,
            AccountingDate = contable.Value,
            RegisteredAt = sello.RegisteredAt,
            Amount = pago.Amount - (pago.ExcessAmount ?? 0m),
            ActorKind = actor.Kind,
            ActorUserId = actor.UserId,
            OriginUserCentralId = actor.CentralUserId,
            CollectorClass = pago.CollectorClass,
            CollectorCode = pago.CollectorCode,
            IdempotencyKey = r.Clave,
            RequestSha256 = huella,
            PaymentModality = pago.PaymentModality,
            PaymentMeansClass = pago.PaymentMeansClass,
            PayerPersonId = pago.PayerPersonId,
            ReceiptNumber = r.Recibo?.Number,
            ReceiptDate = r.Recibo?.Date,
            ReceiptBranchPublicId = r.Recibo?.OfficePublicId,
            ReceiptMeansReference = r.Recibo?.MeansReference,
            ReversesOperationId = pago.Id,
            Reason = r.Motivo.Trim(),
            Payload = payload,
            InputsSha256 = TramoDeDesembolso.Sha256(payload),
            RulesVersion = pago.RulesVersion,
            PrevalidationOutcome = pago.PrevalidationOutcome,
        });

        var calculo = await CalcularAsync(pago, reverso, ct);
        if (calculo.IsFailure) return Result.Failure<ReversoRegistrado>(calculo.Error);
        var c = calculo.Value;
        var efecto = c.Efecto;

        // El plan, si el pago (o un posterior redistribuido) lo había regenerado.
        var regenero = await db.RepaymentScheduleVersions.AsNoTracking().AnyAsync(x => x.LoanId == credito.Id && x.OperationId == pago.Id, ct)
                       || efecto.Reasignaciones.Any(x => x.Antes.PlanNuevo is not null || x.Despues.PlanNuevo is not null);
        Domain.Entities.Lending.Schedules.RepaymentScheduleVersion? versionDelPlan = null;
        if (regenero && efecto.Despues.Plan is { } plan)
        {
            var numeroDelPlan = (await db.RepaymentScheduleVersions.AsNoTracking().Where(x => x.LoanId == credito.Id).MaxAsync(x => (int?)x.VersionNumber, ct) ?? 0) + 1;
            versionDelPlan = registro.RegistrarPlanRegenerado(credito.Id, numeroDelPlan, c.Cargado.Vigentes, reverso, plan, c.Cargado.Entrada!.Festivos);
        }

        var reabre = credito.Status == LoanStatus.PaidOff && efecto.Despues.ValorParaCancelar > 0m;
        if (reabre) RegistroDeCartera.CambiarEstado(credito, LoanStatus.Active, reverso.ValueDate);

        var datos = (await partes.DatosAsync([c.Cargado], ct))[credito.Id];
        var pagador = pago.PayerPersonId is { } pid
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == pid).Select(x => x.PublicId).FirstAsync(ct)
            : datos.DeudorPublicId;
        var recaudador = new CollectorRefV1
        {
            CollectorCode = pago.CollectorCode ?? string.Empty,
            CollectorClass = pago.CollectorClass ?? string.Empty,
            IdempotencyKey = pago.IdempotencyKey ?? string.Empty,
            MeansClass = pago.PaymentMeansClass ?? string.Empty,
            PayerPersonPublicId = pagador,
            Receipt = new ReceiptRefV1
            {
                Number = pago.ReceiptNumber ?? string.Empty, Date = pago.ReceiptDate ?? pago.ValueDate, OfficePublicId = pago.ReceiptBranchPublicId,
                MeansReference = pago.ReceiptMeansReference,
            },
        };
        var reasignaciones = await aplicacion.ReasignacionesAsync(credito, efecto.Reasignaciones, ct);
        var u = await emision.PagoReversadoAsync(new ReversoDePagoAEmitir(datos.Hecho(contable.Value), reverso, pago, r.IniciadoPor, r.Motivo.Trim(),
            EmisionDeCartera.Montos(c.Espejos, RestituidoPorElReverso), efecto.DeCausacion, reasignaciones, reverso.Amount, reabre, recaudador, pagador,
            actor.Kind == Domain.Enums.Integration.ActorKind.Person ? new UserRefV1 { CentralUserId = actor.CentralUserId, Name = actor.Name } : null), ct);

        var mensajeDelReverso = u.De(PagoReversadoV1.Type);
        var espejos = new List<LoanTransaction>();
        foreach (var original in c.Originales)
        {
            var espejo = RegistroDeCartera.Espejar(original, reverso, contable.Value, mensajeDelReverso);
            if (espejo.IsFailure) return Result.Failure<ReversoRegistrado>(espejo.Error);
            espejos.Add(espejo.Value);
        }
        registro.RegistrarTransacciones(espejos);
        registro.RegistrarTransacciones(RegistroDeCartera.DiferenciasPorFamilia(credito.Id, efecto.Diferencias, reverso, contable.Value, reverso.RegisteredAt,
            (_, clase) => clase == LoanTransactionKind.PaymentReallocation ? mensajeDelReverso : u.De(AjusteDeCausacionPorFechaValorV1.Type) ?? mensajeDelReverso));
        await db.SaveChangesAsync(ct);

        pago.ReversedByOperationId = reverso.Id;
        var anulados = await documentos.AnularAsync(credito.Id, pago.ValueDate, reverso, ct);
        var dia = efecto.Hasta;
        var ultimas = await PosicionesDeCartera.UltimasAsync(db, [credito.Id], dia, dia, ct);
        var ultima = ultimas.GetValueOrDefault((credito.Id, dia));
        registro.PosicionVersionada(control, efecto.Despues, credito, c.Cargado.Vigentes, ultima.Version, ultima.Huella, reloj.UtcNow, credito.Status);
        await db.SaveChangesAsync(ct);

        var comprobante = new ComprobanteDeReversoDto(reverso.PublicId, reverso.Number, pago.PublicId, pago.Number, credito.PublicId, credito.Number,
            contable.Value, reverso.RegisteredAt, reverso.Amount, r.Motivo.Trim(), Ajustes(efecto), await aplicacion.NumerosAsync(efecto.Reasignaciones, ct),
            ComprobantesDePago.Saldos(efecto.Despues), reabre, anulados);
        return Result.Success(new ReversoRegistrado(reverso, comprobante, u, false));
    }

    /// <summary>El comprobante de un reverso ya registrado (la respuesta <c>AlreadyApplied</c>).</summary>
    public async Task<ComprobanteDeReversoDto> ComprobanteAsync(LoanOperation reverso, LoanOperation pago, Loan credito, CancellationToken ct)
    {
        var ajustes = await db.LoanTransactions.AsNoTracking()
            .Where(t => t.OperationId == reverso.Id && t.ReversesTransactionId == null)
            .Select(t => new { t.InstallmentNumber, t.Concept, t.Amount }).ToListAsync(ct);
        var marca = await db.LoanControls.AsNoTracking().Where(x => x.LoanId == credito.Id).Select(x => x.LastAccruedDate).FirstAsync(ct);
        var fecha = marca > reverso.ValueDate ? marca : reverso.ValueDate;
        var cargado = await cargador.CargarAsync(credito.Id, null, fecha, ct);
        var saldos = cargado is { Listo: true }
            ? ComprobantesDePago.Saldos(MotorDeCartera.Reconstruir(cargado.Entrada!, fecha))
            : new SaldosDeObligacionDto(0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0);
        return new ComprobanteDeReversoDto(reverso.PublicId, reverso.Number, pago.PublicId, pago.Number, credito.PublicId, credito.Number,
            reverso.AccountingDate, reverso.RegisteredAt, reverso.Amount, reverso.Reason ?? string.Empty,
            ajustes.Select(a => new LineaDeDistribucionDto(a.InstallmentNumber, ComprobantesDePago.Concepto(a.Concept), a.Amount)).ToList(), [], saldos,
            false, []);
    }

    /// <summary>
    /// El cálculo del reverso: la entrada con lo conocido, la reconstrucción sin el pago desde su fecha valor hasta la marca de causación, los
    /// espejos de sus filas y las diferencias por familia. <paramref name="reverso"/> nulo en la vista previa (se simula uno sin registrar).
    /// </summary>
    private async Task<Result<Calculo>> CalcularAsync(LoanOperation pago, LoanOperation? reverso, CancellationToken ct)
    {
        var credito = await db.Loans.FirstAsync(l => l.Id == pago.LoanId, ct);
        var control = await db.LoanControls.FirstAsync(c => c.LoanId == pago.LoanId, ct);
        var marca = control.LastAccruedDate;
        var v = pago.ValueDate;
        var hasta = marca > v ? marca : v;
        var cargado = await cargador.CargarAsync(credito.Id, null, hasta, ct);
        if (cargado is null) return Result.Failure<Calculo>(ErroresDePagos.NoPagable(credito.Number, credito.Status.ToString()));
        if (!cargado.Listo) return Result.Failure<Calculo>(cargado.Error!);

        var deLaOperacion = await db.LoanTransactions.AsNoTracking().Where(t => t.LoanId == credito.Id && t.OperationId == pago.Id).ToListAsync(ct);
        var ids = deLaOperacion.Select(t => t.Id).ToList();
        var espejadas = await db.LoanTransactions.AsNoTracking()
            .Where(t => t.LoanId == credito.Id && t.ReversesTransactionId != null && ids.Contains(t.ReversesTransactionId.Value))
            .ToListAsync(ct);
        var filas = deLaOperacion.Concat(espejadas)
            .Select(t => new FilaDelLibro(t.Id, t.Kind, t.Concept, t.InstallmentNumber, t.Amount, t.ValueDate, t.OperationId, t.ProcessRunId, t.ReversesTransactionId))
            .ToList();
        IReadOnlyList<FilaDelLibro> aEspejar;
        try
        {
            aEspejar = deLaOperacion.Count == 0 ? [] : EspejosDelReverso.DelPago(filas, pago.Id, v).AEspejar;
        }
        catch (ReglaDeCarteraException ex)
        {
            return Result.Failure<Calculo>(ErroresDeGobierno.De(ex.Problema));
        }
        var originales = aEspejar.Select(f => deLaOperacion.First(t => t.Id == f.Id)).ToList();
        var espejos = aEspejar.Select(f => new MovimientoDeLibro(f.FechaValor, f.Clase, f.Concepto, f.Cuota, -f.Importe)).ToList();

        var simulado = reverso ?? new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.PaymentReversal, ValueDate = v, RegisteredAt = reloj.UtcNow, Sequence = control.Sequence + 1,
            ReversesOperationId = pago.Id,
        };
        var entrada = cargado.Entrada!;
        var sinElPago = CargadorDeReconstruccion.ConOperacion(entrada, simulado, CargadorDeReconstruccion.Id(pago.Id));
        var libro = marca >= v ? (await cargador.LibroAsync([credito.Id], marca, ct))[credito.Id] : [];
        var efecto = EfectoEnElLibro.Calcular(entrada, sinElPago, CargadorDeReconstruccion.IdEnLaEntrada(simulado), v, marca, libro, espejos);
        return Result.Success(new Calculo(credito, control, cargado, efecto, originales, espejos, marca));
    }

    private VistaDelReverso Vista(Calculo c, LoanOperation pago, IReadOnlyList<(Guid PublicId, string Kind, string Number)> afectados)
    {
        var porConcepto = c.Espejos.GroupBy(e => e.Concepto).OrderBy(g => (int)g.Key).ToDictionary(g => g.Key, g => g.Sum(e => e.Monto));
        var ajustes = Ajustes(c.Efecto);
        var huella = HuellaDeOperacion.Calcular("Lending.Payments.ReversalPreview", new
        {
            payment = pago.PublicId,
            mark = c.Marca,
            mirrors = c.Espejos.Select(e => new { e.Dia, kind = e.Clase.ToString(), concept = e.Concepto.ToString(), e.Cuota, e.Monto }),
            adjustments = ajustes,
            reallocations = c.Efecto.Reasignaciones.Select(r => new { r.OperacionId, after = ComprobantesDePago.Lineas(r.Despues) }),
            plan = c.Efecto.Despues.Plan?.Cuotas.Select(q => new { q.Numero, q.Vencimiento, q.Capital, q.Interes }),
        });
        var numeros = c.Efecto.Reasignaciones.Select(r => long.TryParse(r.OperacionId, out var n) ? n : 0L).ToList();
        var operaciones = db.LoanOperations.AsNoTracking().Where(o => numeros.Contains(o.Id)).Select(o => new { o.Id, o.PublicId, o.Number }).ToList();
        var redistribuidos = c.Efecto.Reasignaciones.Select(r =>
        {
            var op = operaciones.FirstOrDefault(o => CargadorDeReconstruccion.Id(o.Id) == r.OperacionId);
            return new PagoRedistribuido(op?.PublicId ?? Guid.Empty, op?.Number ?? string.Empty, ComprobantesDePago.Lineas(r.Antes), ComprobantesDePago.Lineas(r.Despues));
        }).ToList();
        return new VistaDelReverso(huella, porConcepto, redistribuidos, afectados,
            c.Credito.Status == LoanStatus.PaidOff && c.Efecto.Despues.ValorParaCancelar > 0m,
            pago.CollectorClass is nameof(ClaseDeRecaudador.Receipts) or nameof(ClaseDeRecaudador.Test),
            ajustes, ComprobantesDePago.Saldos(c.Efecto.Despues), ComprobantesDePago.Plan(c.Efecto.Despues.Plan, c.Efecto.Hasta));
    }

    private static IReadOnlyList<LineaDeDistribucionDto> Ajustes(EfectoEnElLibro e) =>
        e.Diferencias.GroupBy(d => (d.Cuota, d.Concepto)).Select(g => new LineaDeDistribucionDto(g.Key.Cuota, ComprobantesDePago.Concepto(g.Key.Concepto),
            g.Sum(d => d.Diferencia))).Where(l => l.Amount != 0m).OrderBy(l => l.InstallmentNumber ?? 0).ThenBy(l => (int)l.Concept).ToList();

    /// <summary>
    /// Lo que el reverso restituye por concepto, en positivo como dice el contrato (<c>PagoReversadoV1.Amounts</c>, mensajes.md §10.8): el espejo de
    /// lo que el pago bajó de un activo (capital, intereses, cargos) sube y es positivo; el de lo que el pago dejó como anticipo o saldo a favor
    /// baja ese pasivo con el deudor y su espejo es negativo, así que se le cambia el signo. Hasta el 2026-10-13 viajaba negativo y Contabilidad
    /// acreditaba el anticipo otra vez en lugar de debitarlo (lo destapó la conciliación de la e2e de T0632). (nuevo)
    /// </summary>
    public static decimal RestituidoPorElReverso(MovimientoDeLibro espejo) =>
        espejo.Concepto is LoanConcept.Advance or LoanConcept.CreditBalance ? -espejo.Monto : espejo.Monto;
}
