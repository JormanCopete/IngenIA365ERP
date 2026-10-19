using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Deposits.Ledger;

/// <summary>El sello que el registro da a una operación de cuenta: su <c>Sequence</c> y su <c>RegisteredAt</c> monótonos por cuenta. (nuevo)</summary>
public sealed record SelloDeCuenta(int Sequence, DateTime RegisteredAt);

/// <summary>
/// La causación de una cuenta y un día que entra en bloque (feature 014, T030): la fila de control (que avanza de d − 1 a d) y las filas
/// <c>InterestAccrued</c> de ese día valor. (nuevo)
/// </summary>
public sealed record CausacionDeCuentaAEscribir(MemberAccountControl Control, DateOnly Dia, IReadOnlyList<MemberAccountTransaction> Filas);

/// <summary>El resumen de lo que entró en bloque (el evento resumen de la tanda, data-model §0.8): cuentas, días, filas y total con signo por concepto.</summary>
public sealed record ResumenDeCausacionDeCuentas(int Cuentas, int Dias, int Filas, IReadOnlyDictionary<MemberAccountConcept, decimal> PorConcepto)
{
    public static ResumenDeCausacionDeCuentas Vacio { get; } = new(0, 0, 0, new Dictionary<MemberAccountConcept, decimal>());

    public decimal Total => PorConcepto.Values.Sum();
}

/// <summary>Las restricciones del día que la posición desglosa (FR-057): retenido, embargado, en canje, mínimo y lo que queda disponible. (nuevo)</summary>
public sealed record DisponibleDelDia(decimal Retenido, decimal Embargado, decimal EnCanje, decimal Minimo, decimal Disponible)
{
    public static DisponibleDelDia SinRestricciones(decimal saldo) => new(0m, 0m, 0m, 0m, saldo);
}

/// <summary>Los errores del libro de cuentas que producen las primitivas del registro. (nuevo)</summary>
public static class ErroresDelLibroDeCuentas
{
    /// <summary>data-model §0.3: una fila que no es de causación ni de ajuste de causación lleva a lo sumo 2 decimales.</summary>
    public const string ImporteDemasiadoPrecisoCode = "Lending.Savings.AmountTooPrecise";

    /// <summary>contrato-de-cuentas §3.11: un reverso no se reversa.</summary>
    public const string ReversoDeReversoCode = "Lending.Savings.ReversalOfReversal";

    public static Error ReversoDeReverso(long transaccionId) => new(ReversoDeReversoCode,
        $"La fila {transaccionId} ya es el espejo de un reverso: un reverso no se reversa; para rehacer, registre una operación nueva.");
}

/// <summary>
/// El único escritor del libro de cuentas y de lo que lo acompaña (feature 014, T030; data-model §0.7, §4; research R03): agrega
/// <c>LND_MemberAccountOperations</c>, <c>LND_MemberAccountTransactions</c> y <c>LND_MemberAccountDailyPositions</c>, crea la fila de
/// <c>LND_MemberAccountControls</c> y mueve sus marcas de agua (causación, liquidación, secuencia, último registro, última posición, actividad del
/// titular). K8 y K9 le suman las liquidaciones, la retención acumulada, el GMF del mes, las condiciones del CDAT, su plan y las cuotas exigibles. Son
/// <b>primitivas sin lógica financiera</b>: los importes y las filas los calculan los motores de captaciones (<c>MotorDeCaptaciones</c>, y los únicos
/// que calculan retención y GMF, <c>RetencionSobreRendimientos</c> y <c>GravamenMovimientosFinancieros</c>). Nadie más las escribe
/// (<c>NadieEscribeElLibroDeCuentasFueraDelRegistro</c>). Molde: <c>RegistroDeCartera</c> de la 013.
///
/// <para>
/// Reglas que hace cumplir: <c>Sequence</c> y <c>RegisteredAt</c> monótonos por cuenta bajo el cerrojo (<c>max(ahora, LastRegisteredAt + 1 tick)</c>);
/// toda fila con importe distinto de cero y exactamente un origen (operación o corrida); a lo sumo 6 decimales, y 2 salvo en la causación y su ajuste
/// (<c>Lending.Savings.AmountTooPrecise</c>, §0.3); la retención y el GMF <b>en pareja</b> dentro de la operación (lo retenido sale del capital o del
/// interés por pagar y entra a su memoria por el mismo valor, §0.2); el espejo de un reverso con <c>ReversesTransactionId</c>, signo contrario y
/// nunca de otro espejo (<c>Lending.Savings.ReversalOfReversal</c>); la causación avanza la marca un día a la vez (<c>d − 1 → d</c>); la posición se
/// versiona, nunca se sobrescribe. No guarda: el <c>SaveChanges</c> lo hace la operación que lo llama, una vez, con todo junto. (nuevo)
/// </para>
/// </summary>
public sealed class RegistroDeCuentas(IApplicationDbContext db)
{
    /// <summary>
    /// La fila de control de la cuenta que nace (en el mismo guardado que la cuenta, data-model §4.4): marca en la fecha valor de la apertura o en el
    /// corte, secuencia en cero.
    /// </summary>
    public MemberAccountControl IniciarControl(MemberAccount cuenta, DateOnly marcaInicial, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        var control = new MemberAccountControl
        {
            MemberAccount = cuenta.Id == 0 ? cuenta : null,
            MemberAccountId = cuenta.Id,
            LastAccruedDate = marcaInicial,
            Sequence = 0,
            LastRegisteredAt = Utc(ahoraUtc),
        };
        db.MemberAccountControls.Add(control);
        return control;
    }

    /// <summary>El siguiente sello de la cuenta: <c>Sequence + 1</c> y <c>max(ahora, LastRegisteredAt + 1 tick)</c>. Avanza el control.</summary>
    public static SelloDeCuenta Sellar(MemberAccountControl control, DateTime ahoraUtc)
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
    /// Registra la operación que arma <paramref name="crear"/> con el sello de la cuenta principal. La operación debe llevar exactamente ese
    /// <c>Sequence</c> y ese <c>RegisteredAt</c>; un reverso nombra la operación que reversa.
    /// </summary>
    public MemberAccountOperation RegistrarOperacion(MemberAccountControl control, DateTime ahoraUtc, Func<SelloDeCuenta, MemberAccountOperation> crear)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(crear);
        var sello = Sellar(control, ahoraUtc);
        var operacion = crear(sello);
        if (operacion.Sequence != sello.Sequence || operacion.RegisteredAt != sello.RegisteredAt)
            throw new InvalidOperationException("La operación debe llevar el Sequence y el RegisteredAt del sello del registro.");
        if (operacion.MemberAccountId != control.MemberAccountId && operacion.MemberAccount is null)
            throw new InvalidOperationException("La operación es de otra cuenta que la del control.");
        if (operacion.Kind == MemberAccountOperationKind.Reversal && operacion.ReversesOperationId is null)
            throw new InvalidOperationException("Un reverso nombra la operación que reversa (ReversesOperationId).");
        db.MemberAccountOperations.Add(operacion);
        return operacion;
    }

    /// <summary>
    /// Agrega filas del libro. Cada una con importe distinto de cero, exactamente un origen (la operación, por Id o por navegación, o la corrida) y la
    /// precisión de su clase; la retención y el GMF de una misma operación van en pareja. Un espejo debe salir de <see cref="Espejar"/>.
    /// </summary>
    public void RegistrarTransacciones(IEnumerable<MemberAccountTransaction> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);
        var lista = filas.ToList();
        foreach (var fila in lista) Validar(fila);
        ValidarParejas(lista);
        foreach (var fila in lista) db.MemberAccountTransactions.Add(fila);
    }

    /// <summary>
    /// El espejo de una fila del libro en la operación de reverso (FR-002): misma cuenta, clase, concepto, cuota, período y fecha valor, importe con
    /// signo contrario, <c>ReversesTransactionId</c> = la original y la fecha contable del período abierto. Un espejo no se espeja. No lo agrega: va en
    /// <see cref="RegistrarTransacciones"/> con las demás.
    /// </summary>
    public static Result<MemberAccountTransaction> Espejar(MemberAccountTransaction original, MemberAccountOperation reverso, DateOnly fechaContable,
        Guid? mensaje = null)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(reverso);
        if (original.ReversesTransactionId is not null)
            return Result.Failure<MemberAccountTransaction>(ErroresDelLibroDeCuentas.ReversoDeReverso(original.Id));

        return Result.Success(new MemberAccountTransaction
        {
            MemberAccountId = original.MemberAccountId,
            Operation = reverso,
            OperationId = reverso.Id == 0 ? null : reverso.Id,
            Kind = original.Kind,
            Concept = original.Concept,
            InstallmentId = original.InstallmentId,
            SchedulePeriodNumber = original.SchedulePeriodNumber,
            Amount = -original.Amount,
            ValueDate = original.ValueDate,
            AccountingDate = fechaContable,
            RegisteredAt = reverso.RegisteredAt,
            ReversesTransactionId = original.Id,
            MessagePublicId = mensaje,
        });
    }

    /// <summary>
    /// <b>Reversar</b> (FR-002): los espejos de <paramref name="originales"/> —la operación y sus dependientes: GMF, cargos, retención y el ajuste de
    /// causación por fecha valor— en la operación de reverso, y los agrega. Falla sin agregar nada si alguna ya es un espejo.
    /// </summary>
    public Result<IReadOnlyList<MemberAccountTransaction>> Reversar(IEnumerable<MemberAccountTransaction> originales, MemberAccountOperation reverso,
        DateOnly fechaContable, Guid? mensaje = null)
    {
        ArgumentNullException.ThrowIfNull(originales);
        var espejos = new List<MemberAccountTransaction>();
        foreach (var original in originales)
        {
            var espejo = Espejar(original, reverso, fechaContable, mensaje);
            if (espejo.IsFailure) return Result.Failure<IReadOnlyList<MemberAccountTransaction>>(espejo.Error);
            espejos.Add(espejo.Value);
        }
        RegistrarTransacciones(espejos);
        return Result.Success<IReadOnlyList<MemberAccountTransaction>>(espejos);
    }

    /// <summary>
    /// Anota en la operación original el reverso que la anuló (columna de escritura única, data-model §4.1). Una operación reversada no se vuelve a
    /// reversar, y un reverso no se reversa.
    /// </summary>
    public static void MarcarReversada(MemberAccountOperation original, MemberAccountOperation reverso)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(reverso);
        if (original.Kind == MemberAccountOperationKind.Reversal || original.ReversesOperationId is not null)
            throw new InvalidOperationException($"La operación {original.Number} es un reverso: un reverso no se reversa.");
        if (original.ReversedByOperationId is not null)
            throw new InvalidOperationException($"La operación {original.Number} ya está reversada.");
        if (reverso.Id == 0)
            throw new InvalidOperationException("El reverso se guarda antes de anotarlo en la original (su Id es la marca).");
        original.ReversedByOperationId = reverso.Id;
    }

    /// <summary>La liberación del canje de una operación (FR-050, C14): se escribe una vez.</summary>
    public static void LiberarCanje(MemberAccountOperation operacion, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        if (operacion.ClearingUntil is null)
            throw new InvalidOperationException($"La operación {operacion.Number} no tiene valor en canje.");
        if (operacion.ClearedAt is not null)
            throw new InvalidOperationException($"El canje de la operación {operacion.Number} ya se liberó.");
        operacion.ClearedAt = Utc(ahoraUtc);
    }

    /// <summary>
    /// Avanza la marca de agua a <paramref name="fecha"/>: causar el día d exige la marca en la víspera. Una fecha a la vez y en orden; una cuenta que
    /// falla no llama aquí y su marca no avanza.
    /// </summary>
    public static void AvanzarCausacion(MemberAccountControl control, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control.LastAccruedDate != fecha.AddDays(-1))
            throw new InvalidOperationException(
                $"La causación del {fecha:yyyy-MM-dd} exige la marca en la víspera; la cuenta {control.MemberAccountId} está causada hasta el {control.LastAccruedDate:yyyy-MM-dd}.");
        control.LastAccruedDate = fecha;
    }

    /// <summary>
    /// La causación de una tanda o de una puesta al día, <b>en bloque</b> (data-model §4.2): avanza la marca de cada cuenta un día a la vez (en el orden
    /// dado) y agrega todas las filas con un solo <c>AddRange</c>, sin diff por fila. Sólo filas <c>InterestAccrued</c> del día, de su cuenta y con un
    /// origen. Devuelve el resumen que va a la corrida.
    /// </summary>
    public ResumenDeCausacionDeCuentas CausarEnBloque(IReadOnlyList<CausacionDeCuentaAEscribir> causaciones)
    {
        ArgumentNullException.ThrowIfNull(causaciones);
        var filas = new List<MemberAccountTransaction>();
        foreach (var c in causaciones)
        {
            if (c.Filas.Any(f => f.Kind != MemberAccountTransactionKind.InterestAccrued || f.ValueDate != c.Dia || f.Amount == 0m
                                 || (f.MemberAccountId != c.Control.MemberAccountId && f.MemberAccount is null)))
                throw new InvalidOperationException($"La causación del {c.Dia:yyyy-MM-dd} de la cuenta {c.Control.MemberAccountId} trae filas que no son suyas.");
            foreach (var f in c.Filas) Validar(f);
            AvanzarCausacion(c.Control, c.Dia);
            filas.AddRange(c.Filas);
        }
        if (filas.Count > 0) db.MemberAccountTransactions.AddRange(filas);
        return new ResumenDeCausacionDeCuentas(
            causaciones.Select(c => c.Control.MemberAccountId).Distinct().Count(),
            causaciones.Count,
            filas.Count,
            filas.GroupBy(f => f.Concept).OrderBy(g => (int)g.Key).ToDictionary(g => g.Key, g => g.Sum(f => f.Amount)));
    }

    /// <summary>
    /// Marca el fin del último período liquidado (FR-061): liquidar exige la causación hasta el fin del período y no retrocede. La idempotencia por
    /// período la da <c>LND_InterestSettlements</c> (K8).
    /// </summary>
    public static void MarcarLiquidacion(MemberAccountControl control, DateOnly finDelPeriodo)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control.LastAccruedDate < finDelPeriodo)
            throw new InvalidOperationException(
                $"Liquidar hasta el {finDelPeriodo:yyyy-MM-dd} exige la causación hasta ese día; la cuenta {control.MemberAccountId} está causada hasta el {control.LastAccruedDate:yyyy-MM-dd}.");
        if (control.LastSettledThrough is { } previo && finDelPeriodo <= previo)
            throw new InvalidOperationException($"La cuenta {control.MemberAccountId} ya está liquidada hasta el {previo:yyyy-MM-dd}.");
        control.LastSettledThrough = finDelPeriodo;
    }

    /// <summary>El último movimiento del titular (FR-058, FR-084): mide la inactividad; nunca retrocede.</summary>
    public static void MarcarActividadDelTitular(MemberAccountControl control, DateOnly dia)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control.LastHolderActivityDate is null || dia > control.LastHolderActivityDate) control.LastHolderActivityDate = dia;
    }

    /// <summary>
    /// Las clases de fila que <b>no</b> cuentan como movimiento del titular (FR-058): la causación, el abono de intereses, el cargo y la
    /// reclasificación. Las demás reactivan una cuenta inactiva.
    /// </summary>
    public static IReadOnlySet<MemberAccountTransactionKind> NoSonDelTitular { get; } = new HashSet<MemberAccountTransactionKind>
    {
        MemberAccountTransactionKind.InterestAccrued, MemberAccountTransactionKind.AccrualAdjustment, MemberAccountTransactionKind.InterestCredited,
        MemberAccountTransactionKind.ChargeApplied, MemberAccountTransactionKind.Reclassification,
    };

    /// <summary>¿Cuenta la fila como movimiento del titular? (FR-058).</summary>
    public static bool EsMovimientoDelTitular(MemberAccountTransactionKind clase) => !NoSonDelTitular.Contains(clase);

    /// <summary>Agrega una posición del día (versión nueva, nunca sobrescribe) y lleva la última fecha proyectada del control.</summary>
    public void RegistrarPosicion(MemberAccountControl control, MemberAccountDailyPosition posicion)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(posicion);
        if (posicion.MemberAccountId != control.MemberAccountId && posicion.MemberAccount is null)
            throw new InvalidOperationException("La posición es de otra cuenta que la del control.");
        db.MemberAccountDailyPositions.Add(posicion);
        if (control.LastPositionDate is null || posicion.PositionDate > control.LastPositionDate)
            control.LastPositionDate = posicion.PositionDate;
    }

    /// <summary>
    /// La posición del día de una cuenta armada desde su reconstrucción (data-model §4.3): saldos por concepto, el disponible desglosado, la memoria de
    /// lo retenido y la huella como <c>InputsSha256</c>. La versión la decide quien llama (<see cref="PosicionVersionada"/>).
    /// </summary>
    public static MemberAccountDailyPosition Posicion(SaldosDeCuenta estado, MemberAccount cuenta, int version, DateTime registradoEn,
        DisponibleDelDia? disponible = null, IssueTermBand? bandaDePlazo = null, decimal? tasaDelDia = null, int? diasAlVencimiento = null,
        int cuotasVencidas = 0, decimal valorVencido = 0m, decimal retencionDelAnio = 0m, decimal gmfDelMes = 0m, bool exentaDeGmf = false)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(cuenta);
        var saldo = cuenta.Nature == LineNature.Contribution ? estado.SaldoDeAportes : estado.SaldoDeCaptacion;
        var d = disponible ?? DisponibleDelDia.SinRestricciones(saldo);
        return new MemberAccountDailyPosition
        {
            MemberAccountId = cuenta.Id,
            MemberAccount = cuenta.Id == 0 ? cuenta : null,
            PositionDate = estado.Fecha,
            Version = version,
            RegisteredAt = Utc(registradoEn),
            Status = cuenta.Status,
            BranchId = cuenta.BranchId,
            CreditLineId = cuenta.CreditLineId,
            Nature = cuenta.Nature,
            DepositClass = cuenta.DepositClass,
            IssueTermBand = bandaDePlazo,
            Principal = estado.Saldo(MemberAccountConcept.Principal),
            ContributionMinimum = estado.Saldo(MemberAccountConcept.ContributionMinimum),
            ContributionOrdinary = estado.Saldo(MemberAccountConcept.ContributionOrdinary),
            ContributionExtraordinary = estado.Saldo(MemberAccountConcept.ContributionExtraordinary),
            ContributionRevaluation = estado.Saldo(MemberAccountConcept.ContributionRevaluation),
            Bonus = estado.Saldo(MemberAccountConcept.Bonus),
            ChargeReceivable = estado.Saldo(MemberAccountConcept.ChargeReceivable),
            InterestPayable = estado.Saldo(MemberAccountConcept.InterestPayable),
            HeldAmount = d.Retenido,
            GarnishedAmount = d.Embargado,
            ClearingAmount = d.EnCanje,
            MinimumBalance = d.Minimo,
            AvailableBalance = d.Disponible,
            WithholdingYearToDate = retencionDelAnio,
            GmfMonthToDate = gmfDelMes,
            AppliedAnnualRate = tasaDelDia,
            DaysToMaturity = diasAlVencimiento,
            InstallmentsInArrears = cuotasVencidas,
            ArrearsAmount = valorVencido,
            IsInactive = cuenta.Status == MemberAccountStatus.Inactive,
            IsGarnished = cuenta.IsGarnished,
            IsGmfExempt = exentaDeGmf,
            IsAccrualSuspended = cuenta.AccrualSuspendedSince is { } desde && desde <= estado.Fecha,
            InputsSha256 = estado.Huella,
        };
    }

    /// <summary>
    /// La posición de un día con su versión (data-model §4.3): la primera es la 1; si el día ya tiene versiones, una nueva sólo si la huella cambió
    /// (<paramref name="ultimaHuella"/>), con <c>Version</c> = la última + 1. Nunca sobrescribe. La agrega y devuelve; nula si no cambió.
    /// </summary>
    public MemberAccountDailyPosition? PosicionVersionada(MemberAccountControl control, MemberAccountDailyPosition nueva, int ultimaVersion, string? ultimaHuella)
    {
        ArgumentNullException.ThrowIfNull(nueva);
        if (ultimaVersion > 0 && string.Equals(ultimaHuella, nueva.InputsSha256, StringComparison.Ordinal)) return null;
        if (nueva.Version != ultimaVersion + 1)
            throw new InvalidOperationException($"La posición nueva del {nueva.PositionDate:yyyy-MM-dd} va en la versión {ultimaVersion + 1}, no en la {nueva.Version}.");
        RegistrarPosicion(control, nueva);
        return nueva;
    }

    // ------------------------------------------------------------------------------------------- cuotas exigibles (US3) --

    /// <summary>
    /// US3 (T081, T082; data-model §3.5): agrega cuotas exigibles. Cada una con valor mayor que cero, a lo sumo 2 decimales y exactamente un origen (la
    /// corrida o la operación). La idempotencia por período la da el único de la tabla; quien genera no vuelve a pedir un período que ya tiene.
    /// </summary>
    public void RegistrarCuotas(IEnumerable<AccountInstallment> cuotas)
    {
        ArgumentNullException.ThrowIfNull(cuotas);
        var lista = cuotas.ToList();
        foreach (var c in lista)
        {
            if (c.Amount <= 0m)
                throw new InvalidOperationException("Una cuota exigible lleva valor mayor que cero (CK_LND_AccountInstallments_Amount).");
            if (Decimales(c.Amount) > MemberAccountTransaction.DecimalesDeOperacion)
                throw new InvalidOperationException($"Una cuota exigible lleva a lo sumo {MemberAccountTransaction.DecimalesDeOperacion} decimales; {c.Amount} tiene más.");
            if ((c.GeneratedByProcessRunId is not null) == (c.GeneratedByOperationId is not null))
                throw new InvalidOperationException("Una cuota exigible tiene exactamente un origen: la corrida o la operación (CK_LND_AccountInstallments_Origin).");
            if (c.MemberAccountId == 0 && c.MemberAccount is null)
                throw new InvalidOperationException("Una cuota exigible nombra su cuenta.");
        }
        if (lista.Count > 0) db.AccountInstallments.AddRange(lista);
    }

    /// <summary>La marca del último período generado de la cuenta (<c>LastInstallmentPeriod</c>, <c>AAAA-MM-s</c>): sólo avanza.</summary>
    public static void MarcarCuotaGenerada(MemberAccountControl control, string periodo)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentException.ThrowIfNullOrWhiteSpace(periodo);
        if (control.LastInstallmentPeriod is null || string.CompareOrdinal(periodo, control.LastInstallmentPeriod) > 0)
            control.LastInstallmentPeriod = periodo;
    }

    /// <summary>
    /// US3 (T083; data-model §3.7): lo que Nómina o una pagaduría informó que no descontó de una cuota, con su motivo. La cuota sigue exigible. No
    /// guarda.
    /// </summary>
    public void RegistrarNoDescontado(AccountInstallmentNonDeduction noDescontado)
    {
        ArgumentNullException.ThrowIfNull(noDescontado);
        if (noDescontado.RequestedAmount <= 0m || noDescontado.DeductedAmount < 0m || noDescontado.DeductedAmount > noDescontado.RequestedAmount)
            throw new InvalidOperationException("Un no descontado pide más que cero y descuenta entre cero y lo pedido.");
        db.AccountInstallmentNonDeductions.Add(noDescontado);
    }

    // ------------------------------------------------------------------------------------------- GMF del mes (US4) --

    /// <summary>
    /// US4 (T110, T111; data-model §4.7): el uso del tope exento y el GMF de una disposición, por persona y mes, tal como lo calculó
    /// <c>GravamenMovimientosFinancieros</c>, en la misma operación que la disposición. Sin valor ni exención no hay fila (con tarifa cero o «no aplica» no se
    /// escribe nada, FR-022). No guarda.
    /// </summary>
    public GmfMonthlyUsage? RegistrarUsoDeGmf(int persona, MemberAccountOperation operacion, int cuenta, DateOnly fecha, string hecho, decimal dispuesto,
        decimal exento, decimal baseGravada, decimal gmf, GmfExemptionPolicy politica, decimal tarifa)
    {
        ArgumentNullException.ThrowIfNull(operacion);
        if (gmf == 0m && exento == 0m) return null;
        if (gmf < 0m || exento < 0m || baseGravada < 0m)
            throw new InvalidOperationException("El uso del GMF de una disposición no es negativo: el reverso es otra fila con ReversesUsageId.");
        var uso = new GmfMonthlyUsage
        {
            PersonId = persona,
            Year = (short)fecha.Year,
            Month = (byte)fecha.Month,
            MemberAccountId = cuenta,
            Operation = operacion,
            OperationId = operacion.Id,
            TaxableEventCode = hecho.Length <= GmfMonthlyUsage.LargoDelHecho ? hecho : hecho[..GmfMonthlyUsage.LargoDelHecho],
            DisposedAmount = dispuesto,
            ExemptAmount = exento,
            TaxableBase = baseGravada,
            GmfAmount = gmf,
            Policy = politica,
            Rate = tarifa,
            Origin = GmfMonthlyUsage.OrigenOperacion,
        };
        db.GmfMonthlyUsages.Add(uso);
        return uso;
    }

    /// <summary>
    /// US4 (T111; FR-002): el reverso de una disposición devuelve al tope del mes lo que usó: una fila espejo por cada fila de la original, con los valores en
    /// negativo y <c>ReversesUsageId</c>, en el mes de la original. No guarda.
    /// </summary>
    public void ReversarUsosDeGmf(IEnumerable<GmfMonthlyUsage> originales, MemberAccountOperation reverso)
    {
        ArgumentNullException.ThrowIfNull(originales);
        ArgumentNullException.ThrowIfNull(reverso);
        foreach (var o in originales)
        {
            if (o.ReversesUsageId is not null) continue;
            db.GmfMonthlyUsages.Add(new GmfMonthlyUsage
            {
                PersonId = o.PersonId,
                Year = o.Year,
                Month = o.Month,
                MemberAccountId = o.MemberAccountId,
                Operation = reverso,
                OperationId = reverso.Id == 0 ? 0 : reverso.Id,
                TaxableEventCode = o.TaxableEventCode,
                DisposedAmount = -o.DisposedAmount,
                ExemptAmount = -o.ExemptAmount,
                TaxableBase = -o.TaxableBase,
                GmfAmount = -o.GmfAmount,
                Policy = o.Policy,
                Rate = o.Rate,
                ReversesUsageId = o.Id,
                Origin = o.Origin,
            });
        }
    }

    // ------------------------------------------------------------------------------------------------- migración (US9) --

    /// <summary>
    /// US9 (T155; data-model §7.1): lo exento del GMF ya usado en el mes del corte por una persona migrada, en la operación de saldo inicial de su cuenta
    /// exenta, con <c>Origin = Migration</c>: el tope del mes de la activación arranca con lo que la persona ya usó. Sin valor no hay fila. No guarda.
    /// </summary>
    public GmfMonthlyUsage? RegistrarUsoDeGmfMigrado(int persona, MemberAccountOperation saldoInicial, MemberAccount cuenta, DateOnly corte, decimal exentoUsado,
        GmfExemptionPolicy politica)
    {
        ArgumentNullException.ThrowIfNull(saldoInicial);
        ArgumentNullException.ThrowIfNull(cuenta);
        if (exentoUsado == 0m) return null;
        if (exentoUsado < 0m) throw new InvalidOperationException("Lo exento usado del mes no es negativo.");
        if (saldoInicial.Kind != MemberAccountOperationKind.OpeningBalance)
            throw new InvalidOperationException("El uso del GMF migrado va en la operación de saldo inicial de la cuenta.");
        var uso = new GmfMonthlyUsage
        {
            PersonId = persona,
            Year = (short)corte.Year,
            Month = (byte)corte.Month,
            MemberAccountId = cuenta.Id,
            Operation = saldoInicial,
            OperationId = saldoInicial.Id,
            TaxableEventCode = "Migration",
            DisposedAmount = exentoUsado,
            ExemptAmount = exentoUsado,
            TaxableBase = 0m,
            GmfAmount = 0m,
            Policy = politica,
            Rate = 0m,
            Origin = GmfMonthlyUsage.OrigenMigracion,
        };
        db.GmfMonthlyUsages.Add(uso);
        return uso;
    }

    /// <summary>
    /// US9 (T155; data-model §7.1): los rendimientos, el componente inflacionario y la retención del año del corte anteriores al corte, por titular y cuenta,
    /// para el certificado tributario, en la operación de saldo inicial de la cuenta y con <c>Origin = Migration</c>. Se cargan como información, sin hechos.
    /// No guarda.
    /// </summary>
    public WithholdingAccumulation RegistrarAcumuladoMigrado(int persona, MemberAccountOperation saldoInicial, MemberAccount cuenta, short anio, decimal rendimientos,
        decimal componenteInflacionario, decimal baseDeRetencion, decimal retencion)
    {
        ArgumentNullException.ThrowIfNull(saldoInicial);
        ArgumentNullException.ThrowIfNull(cuenta);
        if (rendimientos < 0m || componenteInflacionario < 0m || baseDeRetencion < 0m || retencion < 0m)
            throw new InvalidOperationException("El acumulado tributario migrado no es negativo.");
        if (saldoInicial.Kind != MemberAccountOperationKind.OpeningBalance)
            throw new InvalidOperationException("El acumulado tributario migrado va en la operación de saldo inicial de la cuenta.");
        var acumulado = new WithholdingAccumulation
        {
            PersonId = persona,
            TaxYear = anio,
            MemberAccountId = cuenta.Id,
            DepositClass = cuenta.DepositClass,
            Operation = saldoInicial,
            OperationId = saldoInicial.Id,
            Share = 1m,
            GrossInterest = rendimientos,
            InflationaryComponentAmount = componenteInflacionario,
            WithholdingBase = baseDeRetencion,
            WithholdingAmount = retencion,
            Origin = WithholdingAccumulation.OrigenMigracion,
        };
        db.WithholdingAccumulations.Add(acumulado);
        return acumulado;
    }

    // ---------------------------------------------------------------------------------------- liquidación de intereses (US5) --

    /// <summary>
    /// US5 (T130, T132; data-model §4.5, §4.6; FR-061, FR-063): la liquidación de un período con sus acumulados de retención por titular, en la misma
    /// operación que sus filas, y la marca del último período liquidado. Hace cumplir lo que el hecho promete: lo causado a seis decimales, el bruto, la
    /// retención y el neto al centavo, la diferencia de redondeo = bruto − causado, el neto = bruto − retención y los acumulados que suman lo de la
    /// liquidación. Lo calculan los motores (<c>InteresDeCaptacion</c>, <c>RetencionSobreRendimientos</c>); aquí sólo se escribe. No guarda.
    /// </summary>
    public void RegistrarLiquidacion(MemberAccountControl control, InterestSettlement liquidacion, IReadOnlyList<WithholdingAccumulation> acumulados)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(liquidacion);
        ArgumentNullException.ThrowIfNull(acumulados);
        if (Decimales(liquidacion.AccruedInterest) > MemberAccountTransaction.DecimalesDeCausacion
            || Decimales(liquidacion.GrossInterest) > MemberAccountTransaction.DecimalesDeOperacion
            || Decimales(liquidacion.WithholdingAmount) > MemberAccountTransaction.DecimalesDeOperacion)
            throw new InvalidOperationException("Una liquidación lleva lo causado a seis decimales y el bruto y la retención al centavo.");
        if (liquidacion.RoundingDifference != liquidacion.GrossInterest - liquidacion.AccruedInterest
            || liquidacion.NetInterest != liquidacion.GrossInterest - liquidacion.WithholdingAmount
            || liquidacion.WithholdingAmount < 0m || liquidacion.WithholdingAmount > liquidacion.GrossInterest)
            throw new InvalidOperationException("La liquidación no cuadra: redondeo = bruto − causado y neto = bruto − retención, con la retención entre cero y el bruto.");
        if (acumulados.Count > 0 && (acumulados.Sum(a => a.GrossInterest) != liquidacion.GrossInterest
                                     || acumulados.Sum(a => a.WithholdingAmount) != liquidacion.WithholdingAmount))
            throw new InvalidOperationException("Los acumulados de retención de los titulares suman lo de la liquidación.");
        if (liquidacion.MemberAccountId != control.MemberAccountId)
            throw new InvalidOperationException("La liquidación es de otra cuenta que la del control.");
        MarcarLiquidacion(control, liquidacion.PeriodTo);
        db.InterestSettlements.Add(liquidacion);
        if (acumulados.Count > 0) db.WithholdingAccumulations.AddRange(acumulados);
    }

    /// <summary>Lo que el registro exige de cada fila: importe, un origen y la precisión de su clase.</summary>
    private static void Validar(MemberAccountTransaction fila)
    {
        if (fila.Amount == 0m)
            throw new InvalidOperationException("Una fila del libro de cuentas lleva importe distinto de cero (CK_LND_MemberAccountTransactions_Amount).");
        var deOperacion = fila.OperationId is not null || fila.Operation is not null;
        if (deOperacion == (fila.ProcessRunId is not null))
            throw new InvalidOperationException("Una fila del libro tiene exactamente un origen: la operación o la corrida (CK_LND_MemberAccountTransactions_Origin).");
        var permitidos = MemberAccountTransaction.ClasesDeCausacion.Contains(fila.Kind)
            ? MemberAccountTransaction.DecimalesDeCausacion
            : MemberAccountTransaction.DecimalesDeOperacion;
        if (Decimales(fila.Amount) > permitidos)
            throw new ReglaDeCarteraException(new ProblemaDeCartera(ErroresDelLibroDeCuentas.ImporteDemasiadoPrecisoCode,
                $"Una fila {fila.Kind} lleva a lo sumo {permitidos} decimales; {fila.Amount} tiene más.",
                new Dictionary<string, object?> { ["kind"] = fila.Kind.ToString(), ["amount"] = fila.Amount, ["maxDecimals"] = permitidos }));
    }

    /// <summary>
    /// La retención y el GMF van en pareja dentro de cada operación (§0.2): lo que entra a la memoria (<c>Withholding</c>, <c>Gmf</c>) sale del capital o
    /// del interés por pagar por el mismo valor. Los espejos de un reverso cumplen igual, con el signo contrario.
    /// </summary>
    private static void ValidarParejas(IReadOnlyList<MemberAccountTransaction> filas)
    {
        foreach (var (clase, memoria) in new[] { (MemberAccountTransactionKind.WithholdingApplied, MemberAccountConcept.Withholding),
                     (MemberAccountTransactionKind.GmfCharged, MemberAccountConcept.Gmf) })
        {
            foreach (var grupo in filas.Where(f => f.Kind == clase).GroupBy(Origen))
            {
                var aMemoria = grupo.Where(f => f.Concept == memoria).Sum(f => f.Amount);
                var delTitular = grupo.Where(f => f.Concept is MemberAccountConcept.Principal or MemberAccountConcept.InterestPayable).Sum(f => f.Amount);
                var otros = grupo.Any(f => f.Concept is not (MemberAccountConcept.Principal or MemberAccountConcept.InterestPayable) && f.Concept != memoria);
                if (aMemoria == 0m || aMemoria + delTitular != 0m || otros)
                    throw new InvalidOperationException(
                        $"Las filas {clase} de una operación van en pareja: lo que entra a {memoria} sale del capital o del interés por pagar por el mismo valor (data-model §0.2).");
            }
        }
    }

    /// <summary>El origen de una fila para agruparla: la operación (por navegación o por Id) o la corrida.</summary>
    private static object Origen(MemberAccountTransaction f) =>
        f.Operation is { } o ? o : f.OperationId is { } id ? $"op:{id}" : $"run:{f.ProcessRunId}";

    /// <summary>Los decimales significativos de <paramref name="valor"/>, sin los ceros a la derecha (0,10 tiene uno).</summary>
    public static int Decimales(decimal valor)
    {
        var escala = (decimal.GetBits(valor)[3] >> 16) & 0xFF;
        while (escala > 0 && valor == Math.Round(valor, escala - 1)) escala--;
        return escala;
    }

    private static DateTime Utc(DateTime instante) => instante.Kind switch
    {
        DateTimeKind.Utc => instante,
        DateTimeKind.Local => instante.ToUniversalTime(),
        _ => DateTime.SpecifyKind(instante, DateTimeKind.Utc),
    };
}
