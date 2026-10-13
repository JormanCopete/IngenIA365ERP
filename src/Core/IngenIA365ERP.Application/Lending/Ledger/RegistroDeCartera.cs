using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Application.Lending.Ledger;

/// <summary>
/// La causación de un crédito y un día que entra en bloque (feature 013, US4, T0494): la fila de control (que avanza de d − 1 a d) y las
/// filas <c>Accrual</c> de ese día valor, ya armadas con <see cref="RegistroDeCartera.FilaDeCausacion"/>. (nuevo)
/// </summary>
public sealed record CausacionAEscribir(LoanControl Control, DateOnly Dia, IReadOnlyList<LoanTransaction> Filas);

/// <summary>
/// El resumen de lo que entró en bloque (T67: la causación no lleva diff por fila sino un evento resumen por tanda): créditos, días, filas y
/// el total con signo por concepto. (nuevo)
/// </summary>
public sealed record ResumenDeCausacion(int Creditos, int Dias, int Filas, IReadOnlyDictionary<LoanConcept, decimal> PorConcepto)
{
    public static ResumenDeCausacion Vacio { get; } = new(0, 0, 0, new Dictionary<LoanConcept, decimal>());

    public decimal Total => PorConcepto.Values.Sum();
}

/// <summary>El sello que el registro da a una operación: su <c>Sequence</c> y su <c>RegisteredAt</c> monótonos por crédito. (nuevo)</summary>
public sealed record SelloDeCartera(int Sequence, DateTime RegisteredAt);

/// <summary>Los errores del libro de Cartera que producen las primitivas del registro. (nuevo)</summary>
public static class ErroresDelLibroDeCartera
{
    public const string ReversoDeReversoCode = "Lending.Reversal.OfReversal";

    public static Error ReversoDeReverso(long transaccionId) => new(ReversoDeReversoCode,
        $"La fila {transaccionId} ya es el espejo de un reverso: un reverso no se reversa; para rehacer, registre una operación nueva.");
}

/// <summary>
/// El único escritor del libro de Cartera y de lo que lo acompaña (feature 013, M1, C45, T0208; data-model §0.11, §5, §6): agrega
/// <c>LND_LoanOperations</c>, <c>LND_LoanTransactions</c>, <c>LND_LoanTermsVersions</c>, <c>LND_RepaymentScheduleVersions</c> con sus
/// <c>LND_RepaymentInstallments</c> y <c>LND_LoanDailyPositions</c>, y avanza la marca de agua de <c>LND_LoanControls</c>. Son
/// <b>primitivas sin lógica financiera</b>: los importes, las filas y las versiones los calculan los motores de US4 y US5, que suman
/// <c>Conciliar</c> y la escritura en bloque del proceso. Nadie más las escribe (<c>NadieEscribeElLibroDeCarteraFueraDelRegistro</c>).
///
/// <para>
/// Reglas que hace cumplir: <c>Sequence</c> y <c>RegisteredAt</c> monótonos por crédito bajo el cerrojo
/// (<c>max(ahora, LastRegisteredAt + 1 tick)</c>, T34); toda fila del libro con importe distinto de cero y exactamente un origen
/// (operación o corrida); el espejo de un reverso con <c>ReversesTransactionId</c>, signo contrario y nunca de otro espejo
/// (<c>Lending.Reversal.OfReversal</c>); la causación avanza la marca de un día a la vez (<c>d − 1 → d</c>, T6). No guarda: el
/// <c>SaveChanges</c> lo hace la operación que lo llama, una vez, con todo junto. (nuevo)
/// </para>
/// </summary>
public sealed class RegistroDeCartera(IApplicationDbContext db)
{
    /// <summary>
    /// La fila de control del crédito que nace (en el mismo guardado que el crédito, data-model §5.2): marca en la fecha valor del
    /// desembolso o en el corte, secuencia en cero.
    /// </summary>
    public LoanControl IniciarControl(Loan credito, DateOnly marcaInicial, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(credito);
        var control = new LoanControl
        {
            Loan = credito,
            LoanId = credito.Id,
            LastAccruedDate = marcaInicial,
            Sequence = 0,
            LastRegisteredAt = Utc(ahoraUtc),
        };
        db.LoanControls.Add(control);
        return control;
    }

    /// <summary>El siguiente sello del crédito: <c>Sequence + 1</c> y <c>max(ahora, LastRegisteredAt + 1 tick)</c>. Avanza el control.</summary>
    public static SelloDeCartera Sellar(LoanControl control, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(control);
        var ahora = Utc(ahoraUtc);
        var siguiente = control.LastRegisteredAt.AddTicks(1);
        var registradoEn = ahora > siguiente ? ahora : DateTime.SpecifyKind(siguiente, DateTimeKind.Utc);
        control.Sequence += 1;
        control.LastRegisteredAt = registradoEn;
        return new(control.Sequence, registradoEn);
    }

    /// <summary>
    /// Registra la operación que arma <paramref name="crear"/> con el sello del crédito. La operación debe llevar exactamente ese
    /// <c>Sequence</c> y ese <c>RegisteredAt</c>: el registro no admite otro.
    /// </summary>
    public LoanOperation RegistrarOperacion(LoanControl control, DateTime ahoraUtc, Func<SelloDeCartera, LoanOperation> crear)
    {
        ArgumentNullException.ThrowIfNull(crear);
        var sello = Sellar(control, ahoraUtc);
        var operacion = crear(sello);
        if (operacion.Sequence != sello.Sequence || operacion.RegisteredAt != sello.RegisteredAt)
            throw new InvalidOperationException("La operación debe llevar el Sequence y el RegisteredAt del sello del registro.");
        if (operacion.LoanId != control.LoanId && operacion.Loan is null)
            throw new InvalidOperationException("La operación es de otro crédito que el del control.");
        db.LoanOperations.Add(operacion);
        return operacion;
    }

    /// <summary>
    /// Agrega filas del libro. Cada una con importe distinto de cero y exactamente un origen (la operación, por Id o por
    /// navegación, o la corrida); un espejo debe salir de <see cref="Espejar"/>.
    /// </summary>
    public void RegistrarTransacciones(IEnumerable<LoanTransaction> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);
        foreach (var fila in filas)
        {
            if (fila.Amount == 0m)
                throw new InvalidOperationException("Una fila del libro lleva importe distinto de cero (CK_LND_LoanTransactions_Amount).");
            var deOperacion = fila.OperationId is not null || fila.Operation is not null;
            if (deOperacion == (fila.ProcessRunId is not null))
                throw new InvalidOperationException("Una fila del libro tiene exactamente un origen: la operación o la corrida (CK_LND_LoanTransactions_Origin).");
            db.LoanTransactions.Add(fila);
        }
    }

    /// <summary>
    /// El espejo de una fila del libro en la operación de reverso (FR-002): mismo crédito, clase, concepto, cuota y fecha valor,
    /// importe con signo contrario, <c>ReversesTransactionId</c> = la original y la fecha contable del período abierto. Un espejo no
    /// se espeja (<c>Lending.Reversal.OfReversal</c>). No lo agrega: va en <see cref="RegistrarTransacciones"/> con las demás. US5: el espejo de un
    /// pago lleva el mensaje de su <c>PagoReversado</c> (<paramref name="mensaje"/>).
    /// </summary>
    public static Result<LoanTransaction> Espejar(LoanTransaction original, LoanOperation reverso, DateOnly fechaContable, Guid? mensaje = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(reverso);
        if (original.ReversesTransactionId is not null)
            return Result.Failure<LoanTransaction>(ErroresDelLibroDeCartera.ReversoDeReverso(original.Id));

        return Result.Success(new LoanTransaction
        {
            LoanId = original.LoanId,
            Operation = reverso,
            OperationId = reverso.Id == 0 ? null : reverso.Id,
            Kind = original.Kind,
            Concept = original.Concept,
            InstallmentNumber = original.InstallmentNumber,
            Amount = -original.Amount,
            ValueDate = original.ValueDate,
            AccountingDate = fechaContable,
            RegisteredAt = reverso.RegisteredAt,
            ReversesTransactionId = original.Id,
            InControl = original.InControl,
            MessagePublicId = mensaje,
        });
    }

    /// <summary>Agrega una versión de condiciones del crédito (hecho; la arma el motor o el comando que cambia condiciones).</summary>
    public void RegistrarVersionDeCondiciones(LoanTermsVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        db.LoanTermsVersions.Add(version);
    }

    /// <summary>Agrega una versión del plan con sus cuotas; cada cuota debe ser de esa versión y los números no se repiten.</summary>
    public void RegistrarPlan(RepaymentScheduleVersion plan, IReadOnlyCollection<RepaymentInstallment> cuotas)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(cuotas);
        if (cuotas.Any(c => !ReferenceEquals(c.ScheduleVersion, plan) && (plan.Id == 0 || c.ScheduleVersionId != plan.Id)))
            throw new InvalidOperationException("Cada cuota debe pertenecer a la versión del plan que se registra.");
        if (cuotas.Select(c => c.InstallmentNumber).Distinct().Count() != cuotas.Count)
            throw new InvalidOperationException("Los números de cuota no se repiten dentro de una versión del plan.");
        db.RepaymentScheduleVersions.Add(plan);
        foreach (var cuota in cuotas) db.RepaymentInstallments.Add(cuota);
    }

    /// <summary>Agrega una posición del día (versión nueva, nunca sobrescribe) y lleva la última fecha proyectada del control.</summary>
    public void RegistrarPosicion(LoanControl control, LoanDailyPosition posicion)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(posicion);
        db.LoanDailyPositions.Add(posicion);
        if (control.LastPositionDate is null || posicion.PositionDate > control.LastPositionDate)
            control.LastPositionDate = posicion.PositionDate;
    }

    /// <summary>
    /// Avanza la marca de agua a <paramref name="fecha"/>: causar el día d exige la marca en la víspera (T6, FR-066). Una fecha a la vez y
    /// en orden; un crédito que falla no llama aquí y su marca no avanza.
    /// </summary>
    public static void AvanzarCausacion(LoanControl control, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control.LastAccruedDate != fecha.AddDays(-1))
            throw new InvalidOperationException(
                $"La causación del {fecha:yyyy-MM-dd} exige la marca en la víspera; el crédito {control.LoanId} está causado hasta el {control.LastAccruedDate:yyyy-MM-dd}.");
        control.LastAccruedDate = fecha;
    }

    // ------------------------------------------------------------------------------------------ US4 (T0494, C45) --

    /// <summary>
    /// Una fila <c>Accrual</c> del libro a partir de la del libro teórico (US4): la escribe la corrida (<paramref name="processRunId"/>) o la
    /// operación que pone al día (<paramref name="operacion"/>), con la fecha contable del día causado (T36) y el mensaje de su parte o de su
    /// crédito (T14).
    /// </summary>
    public static LoanTransaction FilaDeCausacion(int loanId, MovimientoDeLibro fila, int? processRunId, LoanOperation? operacion, DateOnly fechaContable,
        DateTime registradoEn, Guid? mensaje)
    {
        ArgumentNullException.ThrowIfNull(fila);
        if (fila.Clase != LoanTransactionKind.Accrual)
            throw new InvalidOperationException("La causación sólo escribe filas Accrual.");
        return new LoanTransaction
        {
            LoanId = loanId,
            ProcessRunId = operacion is null ? processRunId : null,
            Operation = operacion,
            OperationId = operacion is { Id: not 0 } op ? op.Id : null,
            Kind = LoanTransactionKind.Accrual,
            Concept = fila.Concepto,
            InstallmentNumber = fila.Cuota,
            Amount = fila.Monto,
            ValueDate = fila.Dia,
            AccountingDate = fechaContable,
            RegisteredAt = Utc(registradoEn),
            MessagePublicId = mensaje,
        };
    }

    /// <summary>
    /// La causación de una tanda o de una puesta al día, <b>en bloque</b> (T67, data-model §5.1): avanza la marca de cada crédito un día a
    /// la vez (<c>d − 1 → d</c>, en el orden dado) y agrega todas las filas con un solo <c>AddRange</c>; quien guarda la tanda suelta del
    /// seguimiento lo escrito al terminar (sin <c>ChangeTracker</c> entre tandas). Devuelve el resumen que va a la corrida (el evento resumen
    /// de T67: la causación no lleva diff por fila).
    /// </summary>
    public ResumenDeCausacion CausarEnBloque(IReadOnlyList<CausacionAEscribir> causaciones)
    {
        ArgumentNullException.ThrowIfNull(causaciones);
        var filas = new List<LoanTransaction>();
        foreach (var c in causaciones)
        {
            if (c.Filas.Any(f => f.Kind != LoanTransactionKind.Accrual || f.ValueDate != c.Dia || f.Amount == 0m || f.LoanId != c.Control.LoanId))
                throw new InvalidOperationException($"La causación del {c.Dia:yyyy-MM-dd} del crédito {c.Control.LoanId} trae filas que no son suyas.");
            if (c.Filas.Any(f => (f.OperationId is not null || f.Operation is not null) == (f.ProcessRunId is not null)))
                throw new InvalidOperationException("Una fila del libro tiene exactamente un origen: la operación o la corrida (CK_LND_LoanTransactions_Origin).");
            AvanzarCausacion(c.Control, c.Dia);
            filas.AddRange(c.Filas);
        }
        if (filas.Count > 0) db.LoanTransactions.AddRange(filas);
        return new ResumenDeCausacion(
            causaciones.Select(c => c.Control.LoanId).Distinct().Count(),
            causaciones.Count,
            filas.Count,
            filas.GroupBy(f => f.Concept).OrderBy(g => (int)g.Key).ToDictionary(g => g.Key, g => g.Sum(f => f.Amount)));
    }

    /// <summary>
    /// <b>Conciliar</b> (T33, data-model §5.1): registra cada diferencia del libro contra la reconstrucción por (día valor, concepto, cuota)
    /// como una fila nueva —nunca corrige una existente— con la fecha contable del período abierto y enlazada a su operación o a su corrida:
    /// <c>SuspenseReclassification</c> si es la mitad de un traslado entre un concepto y su suspenso del mismo día y cuota,
    /// <c>PaymentReallocation</c> si es capital, y <c>AccrualAdjustment</c> en lo demás. Agrega y devuelve las filas.
    /// </summary>
    public IReadOnlyList<LoanTransaction> Conciliar(int loanId, IReadOnlyList<DiferenciaDeLibro> diferencias, LoanOperation? operacion, int? processRunId,
        DateOnly fechaContable, DateTime registradoEn, Guid? mensaje)
    {
        ArgumentNullException.ThrowIfNull(diferencias);
        var filas = Diferencias(loanId, diferencias, operacion, processRunId, fechaContable, registradoEn, mensaje);
        RegistrarTransacciones(filas);
        return filas;
    }

    /// <summary>Las filas que <see cref="Conciliar"/> registraría, sin agregarlas (las agrega quien las junta con otras, p. ej. un reverso).</summary>
    public static IReadOnlyList<LoanTransaction> Diferencias(int loanId, IReadOnlyList<DiferenciaDeLibro> diferencias, LoanOperation? operacion,
        int? processRunId, DateOnly fechaContable, DateTime registradoEn, Guid? mensaje) =>
        diferencias.Where(d => d.Diferencia != 0m).Select(d => new LoanTransaction
        {
            LoanId = loanId,
            Operation = operacion,
            OperationId = operacion is { Id: not 0 } op ? op.Id : null,
            ProcessRunId = operacion is null ? processRunId : null,
            Kind = ClaseDeDiferencia(d, diferencias),
            Concept = d.Concepto,
            InstallmentNumber = d.Cuota,
            Amount = d.Diferencia,
            ValueDate = d.Dia,
            AccountingDate = fechaContable,
            RegisteredAt = Utc(registradoEn),
            MessagePublicId = mensaje,
        }).ToList();

    /// <summary>La clase de la fila que registra una diferencia de la conciliación.</summary>
    public static LoanTransactionKind ClaseDeDiferencia(DiferenciaDeLibro d, IReadOnlyList<DiferenciaDeLibro> todas)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentNullException.ThrowIfNull(todas);
        if (d.Concepto == LoanConcept.Principal) return LoanTransactionKind.PaymentReallocation;
        LoanConcept? pareja = d.Concepto switch
        {
            LoanConcept.CurrentInterest => LoanConcept.CurrentInterestInSuspense,
            LoanConcept.CurrentInterestInSuspense => LoanConcept.CurrentInterest,
            LoanConcept.DefaultInterest => LoanConcept.DefaultInterestInSuspense,
            LoanConcept.DefaultInterestInSuspense => LoanConcept.DefaultInterest,
            _ => null,
        };
        var traslado = pareja is { } p && todas.Any(o => o.Dia == d.Dia && o.Cuota == d.Cuota && o.Concepto == p && o.Diferencia == -d.Diferencia);
        return traslado ? LoanTransactionKind.SuspenseReclassification : LoanTransactionKind.AccrualAdjustment;
    }

    // ---------------------------------------------------------------------------------------- US5 (bloque 9B, C45) --

    /// <summary>Las clases de fila de los hechos de un pago, una resolución o un anticipo al vencer (data-model §5.1, §7.8–§7.10).</summary>
    public static IReadOnlySet<LoanTransactionKind> ClasesDeHecho { get; } = new HashSet<LoanTransactionKind>
    {
        LoanTransactionKind.PaymentAllocation, LoanTransactionKind.Recovery, LoanTransactionKind.AdvanceReceived, LoanTransactionKind.AdvanceApplied,
        LoanTransactionKind.CreditBalanceRecognized, LoanTransactionKind.CreditBalanceSettled, LoanTransactionKind.PaymentReallocation,
        // US6 (T0591): las filas de un ajuste de Comercial (nota crédito o débito, devolución, anulación).
        LoanTransactionKind.CommercialAdjustment,
    };

    /// <summary>
    /// Una fila de un hecho a partir de la del libro teórico (US5): la de un pago, una resolución o un anticipo aplicado al vencer, que escribe la
    /// operación (<paramref name="operacion"/>) o la corrida (<paramref name="processRunId"/>), con su fecha contable y el mensaje del hecho que la
    /// lleva. Sólo las clases de <see cref="ClasesDeHecho"/>; la causación va por <see cref="FilaDeCausacion"/>.
    /// </summary>
    public static LoanTransaction FilaDeHecho(int loanId, MovimientoDeLibro fila, LoanOperation? operacion, int? processRunId, DateOnly fechaContable,
        DateTime registradoEn, Guid? mensaje)
    {
        ArgumentNullException.ThrowIfNull(fila);
        if (!ClasesDeHecho.Contains(fila.Clase))
            throw new InvalidOperationException($"La fila {fila.Clase} no es de un hecho de pago, resolución o anticipo.");
        return new LoanTransaction
        {
            LoanId = loanId,
            ProcessRunId = operacion is null ? processRunId : null,
            Operation = operacion,
            OperationId = operacion is { Id: not 0 } op ? op.Id : null,
            Kind = fila.Clase,
            Concept = fila.Concepto,
            InstallmentNumber = fila.Cuota,
            Amount = fila.Monto,
            ValueDate = fila.Dia,
            AccountingDate = fechaContable,
            RegisteredAt = Utc(registradoEn),
            MessagePublicId = mensaje,
        };
    }

    /// <summary>
    /// Las filas de las diferencias de una conciliación <b>por familia</b> (US5, T33; <see cref="LibroTeorico.DiferenciasPorFamilia"/>): cada
    /// diferencia por (día valor, concepto, cuota) es una fila nueva enlazada a <paramref name="operacion"/>, con la clase de
    /// <see cref="Domain.Lending.Reversos.EspejosDelReverso.ClaseDeLaDiferencia"/> —<c>PaymentReallocation</c> la de los hechos (un pago posterior
    /// redistribuido) o el capital, <c>SuspenseReclassification</c> la mitad de un traslado al suspenso, <c>AccrualAdjustment</c> lo demás— y el
    /// mensaje que la lleva. No las agrega: van en <see cref="RegistrarTransacciones"/> con las demás de la operación.
    /// </summary>
    public static IReadOnlyList<LoanTransaction> DiferenciasPorFamilia(int loanId, IReadOnlyList<DiferenciaDeLibro> diferencias, LoanOperation operacion,
        DateOnly fechaContable, DateTime registradoEn, Func<DiferenciaDeLibro, LoanTransactionKind, Guid?> mensaje)
    {
        ArgumentNullException.ThrowIfNull(diferencias);
        ArgumentNullException.ThrowIfNull(operacion);
        ArgumentNullException.ThrowIfNull(mensaje);
        return diferencias.Where(d => d.Diferencia != 0m).Select(d =>
        {
            var clase = Domain.Lending.Reversos.EspejosDelReverso.ClaseDeLaDiferencia(d, diferencias);
            return new LoanTransaction
            {
                LoanId = loanId,
                Operation = operacion,
                OperationId = operacion.Id == 0 ? null : operacion.Id,
                Kind = clase,
                Concept = d.Concepto,
                InstallmentNumber = d.Cuota,
                Amount = d.Diferencia,
                ValueDate = d.Dia,
                AccountingDate = fechaContable,
                RegisteredAt = Utc(registradoEn),
                MessagePublicId = mensaje(d, clase),
            };
        }).ToList();
    }

    /// <summary>
    /// La versión del plan que regenera un pago (un abono extraordinario con elección, FR-075) o su reverso (la redistribución que lo cambia): el
    /// plan del motor con sus cuotas, la versión de condiciones vigente, la operación y la fecha exigible de cada cuota por el art. 829. La agrega.
    /// </summary>
    public RepaymentScheduleVersion RegistrarPlanRegenerado(int loanId, int numero, LoanTermsVersion condiciones, LoanOperation operacion,
        Domain.Lending.Schedules.PlanDePagos plan, IReadOnlyList<DateOnly> festivos)
    {
        ArgumentNullException.ThrowIfNull(condiciones);
        ArgumentNullException.ThrowIfNull(operacion);
        ArgumentNullException.ThrowIfNull(plan);
        var version = new RepaymentScheduleVersion
        {
            LoanId = loanId,
            VersionNumber = numero,
            TermsVersionId = condiciones.Id,
            TermsVersion = condiciones.Id == 0 ? condiciones : null,
            Operation = operacion,
            OperationId = operacion.Id,
            EffectiveValueDate = operacion.ValueDate,
            RegisteredAt = operacion.RegisteredAt,
            PrincipalBase = plan.TotalCapital,
            FirstRegeneratedInstallment = plan.PrimeraCuotaRegenerada,
            InstallmentCount = plan.Cuotas.Count,
            RulesVersion = plan.RulesVersion,
            InputsSha256 = operacion.InputsSha256,
        };
        var articulo829 = Domain.Lending.Schedules.FechasDeVencimiento.CalendarioDelArticulo829(festivos);
        var cuotas = plan.Cuotas.Select(c => new RepaymentInstallment
        {
            ScheduleVersion = version,
            InstallmentNumber = c.Numero,
            PeriodStart = c.Desde,
            DueDate = c.Vencimiento,
            ExigibleDate = Domain.Lending.Schedules.FechasDeVencimiento.FechaExigible(c.Vencimiento, articulo829),
            OpeningPrincipal = c.SaldoInicial,
            Principal = c.Capital,
            Interest = c.Interes,
            GraceInterest = c.InteresDeGracia,
            Insurance = c.Seguro,
            OtherCharges = 0m,
            Total = c.Total,
            ClosingPrincipal = c.SaldoFinal,
            IsExtraordinary = c.Extraordinaria != 0m,
            IsGraceInstallment = c.SoloInteres,
        }).ToList();
        RegistrarPlan(version, cuotas);
        return version;
    }

    /// <summary>
    /// <b>Reversar</b> (T33, C49, C101): los espejos de <paramref name="originales"/> en la operación de reverso, por <see cref="Espejar"/>, y los
    /// agrega. Falla sin agregar nada si alguna ya es un espejo (<c>Lending.Reversal.OfReversal</c>).
    /// </summary>
    public Result<IReadOnlyList<LoanTransaction>> Reversar(IEnumerable<LoanTransaction> originales, LoanOperation reverso, DateOnly fechaContable)
    {
        ArgumentNullException.ThrowIfNull(originales);
        var espejos = new List<LoanTransaction>();
        foreach (var original in originales)
        {
            var espejo = Espejar(original, reverso, fechaContable);
            if (espejo.IsFailure) return Result.Failure<IReadOnlyList<LoanTransaction>>(espejo.Error);
            espejos.Add(espejo.Value);
        }
        RegistrarTransacciones(espejos);
        return Result.Success<IReadOnlyList<LoanTransaction>>(espejos);
    }

    /// <summary>
    /// La posición del día de un crédito armada desde su reconstrucción (data-model §6.1): saldos por concepto, capital vencido, altura de mora,
    /// suspensión, usura topada, tasa del día y la huella como <c>InputsSha256</c>. La versión la decide quien llama
    /// (<see cref="PosicionVersionada"/>). US9 (T0701): con la calificación informativa del día si el ciclo la calculó.
    /// </summary>
    public static LoanDailyPosition Posicion(EstadoDelCredito estado, Loan credito, LoanTermsVersion condiciones, int version, DateTime registradoEn,
        LoanStatus? estadoDelCredito = null, Risk.CalificacionDiaria? calificacion = null)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(credito);
        ArgumentNullException.ThrowIfNull(condiciones);
        return new LoanDailyPosition
        {
            LoanId = credito.Id,
            Loan = credito.Id == 0 ? credito : null,
            PositionDate = estado.Fecha,
            Version = version,
            RegisteredAt = Utc(registradoEn),
            Status = estadoDelCredito ?? credito.Status,
            BranchId = credito.BranchId,
            CreditLineId = credito.CreditLineId,
            ModalityCode = condiciones.ModalityCode,
            SubsegmentCode = condiciones.SubsegmentCode,
            Principal = estado.Saldo(LoanConcept.Principal),
            PrincipalPastDue = estado.CapitalVencido,
            CurrentInterest = estado.Saldo(LoanConcept.CurrentInterest),
            DefaultInterest = estado.Saldo(LoanConcept.DefaultInterest),
            CurrentInterestInSuspense = estado.Saldo(LoanConcept.CurrentInterestInSuspense),
            DefaultInterestInSuspense = estado.Saldo(LoanConcept.DefaultInterestInSuspense),
            Insurance = estado.Saldo(LoanConcept.Insurance),
            CollectionFee = estado.Saldo(LoanConcept.CollectionFee),
            OtherCharge = estado.Saldo(LoanConcept.OtherCharge),
            Advance = estado.Anticipo,
            CreditBalance = estado.SaldoAFavor,
            PayoffAmount = estado.ValorParaCancelar,
            DaysPastDue = estado.AlturaDeMora.Dias,
            OldestUnpaidInstallment = estado.AlturaDeMora.CuotaMasAntigua,
            InstallmentsPastDue = estado.AlturaDeMora.CuotasVencidas,
            IsSuspended = estado.Suspendida,
            UsuryCapped = estado.UsuraTopada,
            AppliedAnnualRate = estado.TasaDelDia?.CorrienteEA,
            InputsSha256 = estado.Huella,
            // US9 (T0701): la copia de la calificación informativa del día (la calcula el ciclo con el mismo motor del cierre).
            DailyRating = calificacion?.Categoria,
            DailyRatingSubcategory = calificacion?.Subcategoria,
        };
    }

    /// <summary>
    /// La posición de un día con su versión (data-model §6.1): la primera es la 1; si el día ya tiene versiones, una nueva sólo si la huella
    /// cambió (<paramref name="ultimaHuella"/>), con <c>Version</c> = la última + 1. Nunca sobrescribe. La agrega y devuelve; nula si no cambió.
    /// </summary>
    public LoanDailyPosition? PosicionVersionada(LoanControl control, EstadoDelCredito estado, Loan credito, LoanTermsVersion condiciones,
        int ultimaVersion, string? ultimaHuella, DateTime registradoEn, LoanStatus? estadoDelCredito = null, Risk.CalificacionDiaria? calificacion = null)
    {
        ArgumentNullException.ThrowIfNull(estado);
        if (ultimaVersion > 0 && string.Equals(ultimaHuella, estado.Huella, StringComparison.Ordinal)) return null;
        var posicion = Posicion(estado, credito, condiciones, ultimaVersion + 1, registradoEn, estadoDelCredito, calificacion);
        RegistrarPosicion(control, posicion);
        return posicion;
    }

    /// <summary>Las transiciones del crédito de data-model §8.1 (feature 013, US3, T0444, T0447, T0448).</summary>
    private static readonly IReadOnlyDictionary<LoanStatus, LoanStatus[]> TransicionesDelCredito = new Dictionary<LoanStatus, LoanStatus[]>
    {
        [LoanStatus.Active] = [LoanStatus.PaidOff, LoanStatus.PendingDisbursement, LoanStatus.Voided, LoanStatus.WrittenOff, LoanStatus.Novated],
        [LoanStatus.PaidOff] = [LoanStatus.Active],
        [LoanStatus.PendingDisbursement] = [LoanStatus.Active, LoanStatus.Voided],
        [LoanStatus.WrittenOff] = [LoanStatus.PaidOff],
    };

    /// <summary>¿Puede el crédito pasar de <paramref name="desde"/> a <paramref name="hacia"/>? (data-model §8.1).</summary>
    public static bool PuedePasar(LoanStatus desde, LoanStatus hacia) =>
        TransicionesDelCredito.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    /// <summary>
    /// Mueve el estado del crédito en la transacción de la operación que lo causa (data-model §8.1: todas las transiciones las hace el
    /// registro): <c>PendingDisbursement</c> tras el reverso del único tramo vivo, <c>Active</c> con un giro nuevo, <c>Voided</c> con fecha y
    /// motivo. Una transición fuera del diagrama es un defecto (excepción).
    /// </summary>
    public static void CambiarEstado(Loan credito, LoanStatus hacia, DateOnly fecha, string? motivo = null)
    {
        ArgumentNullException.ThrowIfNull(credito);
        if (!PuedePasar(credito.Status, hacia))
            throw new InvalidOperationException($"El crédito {credito.Number} está en «{credito.Status}» y no puede pasar a «{hacia}» (data-model §8.1).");
        credito.Status = hacia;
        switch (hacia)
        {
            case LoanStatus.Voided:
                ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
                credito.VoidedOn = fecha;
                credito.VoidReason = motivo.Trim();
                break;
            case LoanStatus.PaidOff:
                credito.PaidOffOn = fecha;
                break;
            case LoanStatus.Active:
                credito.PaidOffOn = null;
                break;
        }
    }

    private static DateTime Utc(DateTime instante) => instante.Kind switch
    {
        DateTimeKind.Utc => instante,
        DateTimeKind.Local => instante.ToUniversalTime(),
        _ => DateTime.SpecifyKind(instante, DateTimeKind.Utc),
    };
}
