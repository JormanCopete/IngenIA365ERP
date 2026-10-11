using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Schedules;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Ledger;

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
    /// se espeja (<c>Lending.Reversal.OfReversal</c>). No lo agrega: va en <see cref="RegistrarTransacciones"/> con las demás.
    /// </summary>
    public static Result<LoanTransaction> Espejar(LoanTransaction original, LoanOperation reverso, DateOnly fechaContable)
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
            MessagePublicId = null,
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
