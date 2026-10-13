using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// Un pago tal como se emite (feature 013, US5, T0545; mensajes.md §7.2, §10.7, §10.9, §10.11, §10.12, §10.14): el hecho del crédito, la operación,
/// quién recaudó y con qué clave, el pagador, lo recibido y el excedente, las filas propias del pago (del libro teórico), las diferencias de
/// causación por una fecha valor anterior a lo causado, la redistribución de los pagos posteriores, el plan nuevo, si canceló, la firmeza, la
/// simulación y la validación previa. (nuevo)
/// </summary>
public sealed record PagoAEmitir(
    HechoDeCredito Hecho,
    LoanOperation Operacion,
    Recaudador Recaudador,
    string Clave,
    ClaseDeMedioDeRecaudo ClaseDeMedio,
    Guid PagadorPublicId,
    ReferenciaDeRecibo Recibo,
    PaymentModality Modalidad,
    decimal Recibido,
    decimal Excedente,
    IReadOnlyList<MovimientoDeLibro> Filas,
    IReadOnlyList<DiferenciaDeLibro> AjustesDeCausacion,
    IReadOnlyList<ReallocationV1> Reasignaciones,
    PrepaymentChoice? Eleccion,
    LoanPlanSummaryV1? PlanNuevo,
    bool Cancela,
    DateOnly? Firmeza,
    string? SimulationId,
    PrevalidationOutcome ValidacionPrevia,
    Guid? ReclamacionPublicId);

/// <summary>El reverso de un pago tal como se emite (mensajes.md §10.8, §11.5; C08, C13). (nuevo)</summary>
public sealed record ReversoDePagoAEmitir(
    HechoDeCredito Hecho,
    LoanOperation Reverso,
    LoanOperation Pago,
    string IniciadoPor,
    string Motivo,
    IReadOnlyList<ConceptAmountV1> Valores,
    IReadOnlyList<DiferenciaDeLibro> AjustesDeCausacion,
    IReadOnlyList<ReallocationV1> Reasignaciones,
    decimal Reversado,
    bool Reabierto,
    CollectorRefV1 Recaudador,
    Guid PagadorPublicId,
    UserRefV1? ReversadoPor);

/// <summary>Una resolución de saldo a favor o de anticipo tal como se emite (mensajes.md §10.13). (nuevo)</summary>
public sealed record ResolucionAEmitir(
    HechoDeCredito Hecho,
    LoanOperation Operacion,
    LoanConcept Concepto,
    decimal Monto,
    FormaDeResolucion Forma,
    CollectorRefV1 Recaudador,
    OriginalFactRefV1? PagoDestino,
    LoanRefV1? CreditoDestino);

/// <summary>Los mensajes de una unidad emitida, por tipo. (nuevo)</summary>
public sealed record UnidadEmitida(IReadOnlyList<IntegrationMessage> Mensajes)
{
    public static UnidadEmitida Vacia { get; } = new([]);

    public Guid? De(string tipo) => Mensajes.FirstOrDefault(m => m.Type == tipo)?.PublicId;
}

public sealed partial class EmisionDeCartera
{
    /// <summary>La clase del origen de un pago (mensajes.md §3).</summary>
    public const string ClasePago = "PaymentApplication";

    /// <summary>La clase del origen de un reverso de pago.</summary>
    public const string ClaseReversoDePago = "PaymentReversal";

    /// <summary>La clase del origen de una resolución de saldo a favor o de anticipo.</summary>
    public const string ClaseResolucion = "CreditBalanceResolution";

    /// <summary>Los conceptos del suspenso: lo que su cobro va en <c>CobroDeInteresEnSuspenso</c>.</summary>
    private static readonly HashSet<LoanConcept> DeSuspenso = [LoanConcept.CurrentInterestInSuspense, LoanConcept.DefaultInterestInSuspense];

    /// <summary>El recaudador de un hecho (<c>CollectorRefV1</c>).</summary>
    public static CollectorRefV1 Recaudador(Recaudador r, string clave, ClaseDeMedioDeRecaudo medio, Guid pagador, ReferenciaDeRecibo recibo) => new()
    {
        CollectorCode = r.Code,
        CollectorClass = r.Class.ToString(),
        IdempotencyKey = clave,
        MeansClass = medio.ToString(),
        PayerPersonPublicId = pagador,
        Receipt = new ReceiptRefV1
        {
            Number = recibo.Number, Date = recibo.Date, OfficePublicId = recibo.OfficePublicId, MeansReference = recibo.MeansReference,
            DocumentKind = recibo.DocumentKind,
        },
    };

    /// <summary>
    /// <c>PagoAplicado</c> y sus acompañantes en la unidad de la operación (T0545; C01, C03; mensajes.md §7.2): <c>AnticipoRecibido</c>,
    /// <c>CobroDeInteresEnSuspenso</c>, <c>RecuperacionDeCastigada</c>, <c>SaldoAFavorReconocido</c> (<c>cause = NoPrepaymentPreference</c>) y
    /// <c>AjusteDeCausacionPorFechaValor</c> (<c>cause = ValueDatedOperation</c>) cuando corresponden, todos con el modo sellado del principal; origen
    /// la operación (<c>PaymentApplication</c>), clave <c>Confirmation</c>, tercero el pagador, <c>dimensions.collectorClass</c> = la clase del
    /// recaudador. <c>amounts</c> del principal lleva sólo lo aplicado a la deuda (y el anticipo que usó una cancelación, D7). Ningún contenido
    /// lleva cuentas. Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> PagoAplicadoAsync(PagoAEmitir p, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(p);
        var h = p.Hecho;
        var op = p.Operacion;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var dimensiones = Dimensiones(h, categoria) with { CollectorClass = p.Recaudador.Class.ToString() };
        var registrado = new DateTimeOffset(DateTime.SpecifyKind(op.RegisteredAt, DateTimeKind.Utc));

        var aplicados = p.Filas
            .Where(f => (f.Clase == LoanTransactionKind.PaymentAllocation && !DeSuspenso.Contains(f.Concepto))
                        || (f.Clase == LoanTransactionKind.AdvanceApplied && f.Concepto == LoanConcept.Advance))
            .ToList();
        var principal = new PagoAplicadoV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = op.ValueDate,
            RegisteredAt = registrado,
            Collector = Recaudador(p.Recaudador, p.Clave, p.ClaseDeMedio, p.PagadorPublicId, p.Recibo),
            Modality = p.Modalidad.ToString(),
            AmountReceived = p.Recibido,
            Surplus = p.Excedente,
            Dimensions = dimensiones,
            Amounts = Montos(aplicados, f => -f.Monto, f => f.Clase == LoanTransactionKind.AdvanceApplied ? "AdvanceUsed" : null),
            Reallocations = p.Reasignaciones,
            PrepaymentChoice = p.Eleccion?.ToString(),
            NewPlan = p.PlanNuevo,
            PaidOff = p.Cancela,
            FinalityUntil = p.Firmeza,
            SimulationId = p.SimulationId,
            InsuranceClaimPublicId = p.ReclamacionPublicId,
            RulesVersion = h.Condiciones.RulesVersion,
        };
        List<object> contenidos = [principal];

        var anticipo = p.Filas.Where(f => f.Clase == LoanTransactionKind.AdvanceReceived).ToList();
        if (anticipo.Count > 0)
            contenidos.Add(new AnticipoRecibidoV1
            {
                Loan = h.Referencia, DebtorPersonPublicId = h.DeudorPublicId, ValueDate = op.ValueDate, RegisteredAt = registrado, Dimensions = dimensiones,
                Amounts = Montos(anticipo, f => f.Monto), RulesVersion = h.Condiciones.RulesVersion,
            });
        var suspenso = p.Filas.Where(f => f.Clase == LoanTransactionKind.PaymentAllocation && DeSuspenso.Contains(f.Concepto)).ToList();
        if (suspenso.Count > 0)
            contenidos.Add(new CobroDeInteresEnSuspensoV1
            {
                Loan = h.Referencia, DebtorPersonPublicId = h.DeudorPublicId, ValueDate = op.ValueDate, RegisteredAt = registrado, Dimensions = dimensiones,
                Amounts = Montos(suspenso, f => -f.Monto), RulesVersion = h.Condiciones.RulesVersion,
            });
        var recuperado = p.Filas.Where(f => f.Clase == LoanTransactionKind.Recovery).ToList();
        if (recuperado.Count > 0)
            contenidos.Add(new RecuperacionDeCastigadaV1
            {
                Loan = h.Referencia, DebtorPersonPublicId = h.DeudorPublicId, ValueDate = op.ValueDate, RegisteredAt = registrado,
                Dimensions = dimensiones with { WrittenOff = true }, Amounts = Montos(recuperado, f => -f.Monto), RulesVersion = h.Condiciones.RulesVersion,
            });
        var saldo = p.Filas.Where(f => f.Clase == LoanTransactionKind.CreditBalanceRecognized).ToList();
        if (saldo.Count > 0)
            contenidos.Add(new SaldoAFavorReconocidoV1
            {
                Loan = h.Referencia, DebtorPersonPublicId = h.DeudorPublicId, ValueDate = op.ValueDate, RegisteredAt = registrado, Dimensions = dimensiones,
                Amounts = Montos(saldo, f => f.Monto), RulesVersion = h.Condiciones.RulesVersion, Cause = "NoPrepaymentPreference",
            });
        if (p.AjustesDeCausacion.Count > 0)
            contenidos.Add(Ajuste(h, dimensiones, op, ClasePago, p.AjustesDeCausacion, null));

        var modo = await modoDePaso.ResolverAsync(PagoAplicadoV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, ClasePago, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                PersonPublicId: p.PagadorPublicId),
            ClavesDeEvento.Confirmacion, contenidos, modo.Value, ValidacionPrevia: p.ValidacionPrevia), ct);
        return Result.Success(new UnidadEmitida(mensajes));
    }

    /// <summary>
    /// <c>PagoReversado</c> (mensajes.md §10.8): origen el reverso (<c>PaymentReversal</c>), clave <c>Confirmation</c>, <c>related</c> = el pago,
    /// <b>hereda el modo</b> del <c>PagoAplicado</c> (C08), tercero el pagador, con su acompañante <c>AjusteDeCausacionPorFechaValor</c> si la
    /// reconstrucción deja diferencias de causación (C03). Si el reverso lo inició Cartera (<c>initiatedBy = Lending</c>) y el recaudador es de
    /// clase <c>Receipts</c> o <c>Test</c>, en el mismo guardado <c>PagoReversadoPorCartera</c> al destino <c>Receipts</c> (§11.5, C13). Sin guardar.
    /// </summary>
    public async Task<UnidadEmitida> PagoReversadoAsync(ReversoDePagoAEmitir r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var h = r.Hecho;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var dimensiones = Dimensiones(h, categoria) with { CollectorClass = r.Recaudador.CollectorClass };
        var original = await HechoOriginalAsync(r.Pago.PublicId, PagoAplicadoV1.Type, r.Pago.Number, ct);
        var principal = new PagoReversadoV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = r.Pago.ValueDate,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(r.Reverso.RegisteredAt, DateTimeKind.Utc)),
            Dimensions = dimensiones,
            Amounts = r.Valores,
            Original = original,
            RulesVersion = h.Condiciones.RulesVersion,
            InitiatedBy = r.IniciadoPor,
            Reason = r.Motivo,
            Collector = r.Recaudador,
            ReversedAmount = r.Reversado,
            Reallocations = r.Reasignaciones,
            Reopened = r.Reabierto,
        };
        List<object> contenidos = [principal];
        if (r.AjustesDeCausacion.Count > 0) contenidos.Add(Ajuste(h, dimensiones, r.Reverso, ClaseReversoDePago, r.AjustesDeCausacion, original));

        var relacionado = new DocumentoRelacionado(r.Pago.PublicId, ClasePago, r.Pago.Number);
        var origen = new OrigenDeEmision(MessageOriginKind.Document, r.Reverso.PublicId, ClaseReversoDePago, Prefijo(r.Reverso.Number), r.Reverso.Number,
            h.FechaContable, h.SucursalPublicId, PersonPublicId: r.PagadorPublicId);
        ModoDeEntrega modo = original is null
            ? (await modoDePaso.ResolverAsync(PagoReversadoV1.Type, h.FechaContable, ct)).Value ?? new ModoDeEntrega.Sellado(DeliveryMode.NotPosted)
            : new ModoDeEntrega.Heredado(r.Pago.PublicId);
        var emitidos = new List<IntegrationMessage>();
        emitidos.AddRange(await emisor.EmitirAsync(new SolicitudDeEmision(origen, ClavesDeEvento.Confirmacion, contenidos, modo, Relacionado: relacionado,
            UsuarioDeOrigen: r.ReversadoPor), ct));

        if (r.IniciadoPor == IniciadoPorCartera && r.Recaudador.CollectorClass is nameof(ClaseDeRecaudador.Receipts) or nameof(ClaseDeRecaudador.Test))
        {
            var aviso = new PagoReversadoPorCarteraV1
            {
                Loan = h.Referencia,
                Payment = original ?? new OriginalFactRefV1 { Type = PagoAplicadoV1.Type, OriginPublicId = r.Pago.PublicId, Number = r.Pago.Number },
                ReversalOperationPublicId = r.Reverso.PublicId,
                ReversalNumber = r.Reverso.Number,
                Collector = r.Recaudador,
                ReversedAmount = r.Reversado,
                ValueDate = r.Pago.ValueDate,
                Reason = r.Motivo,
                ReversedBy = r.ReversadoPor ?? new UserRefV1(),
                SurplusAlreadyReturned = r.Pago.ExcessAmount ?? 0m,
                Reopened = r.Reabierto,
            };
            emitidos.AddRange(await emisor.EmitirAsync(new SolicitudDeEmision(origen, ClavesDeEvento.Confirmacion, [aviso],
                new ModoDeEntrega.Sellado(DeliveryMode.Always), Relacionado: relacionado, Destino: IntegrationDestinations.Receipts,
                UsuarioDeOrigen: r.ReversadoPor), ct));
        }
        return new UnidadEmitida(emitidos);
    }

    /// <summary>
    /// Lo que un pago emite <b>aparte</b> de su unidad (mensajes.md §7.2; C01): <c>CausacionReanudada</c> cuando el pago lo pone al día y la causación
    /// suspendida se reanuda (FR-065), con origen la operación, clave <c>Confirmation:{tipoDeHecho:N}</c> (el <c>PublicId</c> sembrado del tipo en
    /// <c>LND_FactTypes</c>), su propio modo y <c>causeFact</c> = el <c>PagoAplicado</c>. Sin la fila del tipo no emite. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage?> ReanudacionPorPagoAsync(HechoDeCredito h, LoanOperation op, string clase, int diasDeMora, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(h);
        ArgumentNullException.ThrowIfNull(op);
        var tipo = await db.LendingFactTypes.AsNoTracking().Where(f => f.Code == CausacionReanudadaV1.Type && !f.IsDeleted)
            .Select(f => (Guid?)f.PublicId).FirstOrDefaultAsync(ct);
        if (tipo is not { } tipoDeHecho) return null;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var contenido = new CausacionReanudadaV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            Dimensions = Dimensiones(h, categoria),
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(op.RegisteredAt, DateTimeKind.Utc)),
            EffectiveDate = op.ValueDate.AddDays(1),
            Cause = "ArrearsCured",
            DaysPastDue = diasDeMora,
            CauseFact = await HechoOriginalAsync(op.PublicId, clase == ClasePago ? PagoAplicadoV1.Type : SaldoAFavorResueltoV1.Type, op.Number, ct),
        };
        var modo = await modoDePaso.ResolverAsync(CausacionReanudadaV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return null;
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, op.PublicId, clase, Prefijo(op.Number), op.Number, h.FechaContable, h.SucursalPublicId,
                PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.ConfirmacionPor(tipoDeHecho), [contenido], modo.Value), ct);
        return mensajes[0];
    }

    /// <summary>Quién inició el reverso de un pago (<c>initiatedBy</c>, §10.8).</summary>
    public const string IniciadoPorElRecaudador = "Collector";

    /// <summary>El reverso lo inició un usuario de Cartera: también nace <c>PagoReversadoPorCartera</c>.</summary>
    public const string IniciadoPorCartera = "Lending";

    /// <summary>
    /// <c>SaldoAFavorResuelto</c> (mensajes.md §10.13): origen la resolución (<c>CreditBalanceResolution</c>), clave <c>Confirmation</c>, tercero el
    /// deudor; con la aplicación en la obligación destino o a capital (<c>targetPayment</c>, <c>targetLoan</c>). Sin guardar.
    /// </summary>
    public async Task<Result<UnidadEmitida>> SaldoAFavorResueltoAsync(ResolucionAEmitir r, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(r);
        var h = r.Hecho;
        var categoria = await new CategoriaContableDeCartera(db).VigenteAsync(h.Credito.Id, h.FechaContable, ct);
        var contenido = new SaldoAFavorResueltoV1
        {
            Loan = h.Referencia,
            DebtorPersonPublicId = h.DeudorPublicId,
            ValueDate = r.Operacion.ValueDate,
            RegisteredAt = new DateTimeOffset(DateTime.SpecifyKind(r.Operacion.RegisteredAt, DateTimeKind.Utc)),
            Dimensions = Dimensiones(h, categoria) with
            {
                CollectorClass = r.Forma == FormaDeResolucion.RefundThroughCollector ? r.Recaudador.CollectorClass : null,
            },
            Amounts = [new ConceptAmountV1 { Concept = r.Concepto.ToString(), Amount = r.Monto }],
            RulesVersion = h.Condiciones.RulesVersion,
            BalanceKind = r.Concepto.ToString(),
            Resolution = r.Forma.ToString(),
            Collector = r.Recaudador,
            TargetPayment = r.PagoDestino,
            TargetLoan = r.CreditoDestino,
        };
        var modo = await modoDePaso.ResolverAsync(SaldoAFavorResueltoV1.Type, h.FechaContable, ct);
        if (modo.IsFailure) return Result.Failure<UnidadEmitida>(modo.Error);
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, r.Operacion.PublicId, ClaseResolucion, Prefijo(r.Operacion.Number), r.Operacion.Number,
                h.FechaContable, h.SucursalPublicId, PersonPublicId: h.DeudorPublicId),
            ClavesDeEvento.Confirmacion, [contenido], modo.Value), ct);
        return Result.Success(new UnidadEmitida(mensajes));
    }

    /// <summary>Valores por concepto y cuota de unas filas, con el signo que pide el hecho.</summary>
    public static IReadOnlyList<ConceptAmountV1> Montos(IEnumerable<MovimientoDeLibro> filas, Func<MovimientoDeLibro, decimal> signo,
        Func<MovimientoDeLibro, string?>? motivo = null) =>
        filas.GroupBy(f => (f.Concepto, f.Cuota, Motivo: motivo?.Invoke(f)))
            .Select(g => new ConceptAmountV1 { Concept = g.Key.Concepto.ToString(), InstallmentNumber = g.Key.Cuota, Amount = g.Sum(signo), ReasonCode = g.Key.Motivo })
            .Where(m => m.Amount != 0m)
            .OrderBy(m => m.InstallmentNumber ?? 0).ThenBy(m => m.Concept, StringComparer.Ordinal)
            .ToList();

    /// <summary>El acompañante <c>AjusteDeCausacionPorFechaValor</c> de una operación (forma masiva de una línea, valores con signo).</summary>
    private static AjusteDeCausacionPorFechaValorV1 Ajuste(HechoDeCredito h, LoanFactDimensionsV1 dimensiones, LoanOperation op, string clase,
        IReadOnlyList<DiferenciaDeLibro> ajustes, OriginalFactRefV1? causa)
    {
        var montos = ajustes.GroupBy(d => (d.Concepto, d.Cuota)).OrderBy(g => g.Key.Cuota ?? 0).ThenBy(g => (int)g.Key.Concepto)
            .Select(g => new ConceptAmountV1 { Concept = g.Key.Concepto.ToString(), InstallmentNumber = g.Key.Cuota, Amount = g.Sum(d => d.Diferencia) })
            .Where(m => m.Amount != 0m).ToList();
        var linea = new LoanFactLineV1 { Loan = h.Referencia, DebtorPersonPublicId = h.DeudorPublicId, Dimensions = dimensiones, Amounts = montos, ValueDate = op.ValueDate };
        return new AjusteDeCausacionPorFechaValorV1
        {
            FactDate = op.ValueDate,
            BatchNumber = 1,
            PartNumber = 1,
            CatchUpOperation = new CatchUpOperationV1 { OperationPublicId = op.PublicId, DocumentClass = clase, Number = op.Number },
            Cause = "ValueDatedOperation",
            CauseFact = causa,
            Totals = [new LoanFactTotalV1 { Dimensions = dimensiones, Amounts = montos, LoanCount = 1 }],
            Lines = [linea],
            LineCount = 1,
            ControlTotal = montos.Sum(m => m.Amount),
        };
    }
}
