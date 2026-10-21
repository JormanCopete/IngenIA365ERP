using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;

/// <summary>
/// <c>LND_MemberAccountTransactions</c> (feature 014, K7; data-model §4.2; FR-001, FR-002, FR-012, FR-058, FR-062, Principio XI): el libro de
/// cuentas, una fila por concepto con tres fechas. <b>Todo concepto suma por su signo</b> y el saldo de un concepto es Σ de sus filas (§0.2):
/// en los conceptos del titular, <c>+</c> es lo que la Cartera le debe; en <c>ChargeReceivable</c>, lo que el titular le debe a la Cartera; en
/// <c>Withholding</c> y <c>Gmf</c>, la memoria de lo retenido (no es saldo del titular). Hecho de sólo inserción (<see cref="IHechoInmutable"/>,
/// <c>bigint</c>) con un solo escritor, <c>RegistroDeCuentas</c>; corregir es un espejo (<see cref="ReversesTransactionId"/>) o un
/// <c>AccrualAdjustment</c> enlazado. Exactamente un origen: la operación o la corrida (<c>CK_LND_MemberAccountTransactions_Origin</c>).
/// <see cref="Amount"/> lleva hasta 6 decimales (alias <c>MontoCausado</c>, R05): la causación diaria los guarda; las demás filas, a lo sumo 2.
///
/// <para><c>TermsVersionId</c> (K9) nace con <c>LND_MemberAccountTermsVersions</c>; <see cref="InstallmentId"/> es de K7 y su FK nace con
/// <c>LND_AccountInstallments</c> (US3, T081).</para>
/// </summary>
public class MemberAccountTransaction : AuditableEntityLong, IHechoInmutable
{
    /// <summary>Decimales máximos de una fila del libro de cuentas (alias <c>MontoCausado</c>).</summary>
    public const int DecimalesDeCausacion = 6;

    /// <summary>Decimales máximos de una fila que no es de causación ni de ajuste de causación (§0.3).</summary>
    public const int DecimalesDeOperacion = 2;

    public int MemberAccountId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCuentas</c> enlace en el mismo guardado una fila con su cuenta todavía sin Id.</summary>
    public Accounts.MemberAccount? MemberAccount { get; init; }

    /// <summary>La operación que la escribió; excluyente con <see cref="ProcessRunId"/>.</summary>
    public long? OperationId { get; init; }

    /// <summary>Navegación para enlazar en el mismo guardado una fila con su operación todavía sin Id.</summary>
    public MemberAccountOperation? Operation { get; init; }

    /// <summary>La corrida que la escribió (causación del ciclo); excluyente con <see cref="OperationId"/>.</summary>
    public int? ProcessRunId { get; init; }

    public MemberAccountTransactionKind Kind { get; init; }

    public MemberAccountConcept Concept { get; init; }

    /// <summary>El pago de una cuota exigible (<c>LND_AccountInstallments</c>, US3).</summary>
    public long? InstallmentId { get; init; }

    /// <summary>Período del plan del CDAT al que pertenece la causación o el pago (K9).</summary>
    public int? SchedulePeriodNumber { get; init; }

    /// <summary>US6 (T169; research E16): la versión de condiciones del CDAT (o del contractual) a la que pertenece la causación o el pago.</summary>
    public int? TermsVersionId { get; init; }

    /// <summary>Con signo y distinto de cero (<c>CK_LND_MemberAccountTransactions_Amount</c>).</summary>
    public decimal Amount { get; init; }

    public DateOnly ValueDate { get; init; }

    public DateOnly AccountingDate { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>Fila espejo de un reverso (FR-002); única: un espejo por fila, y un espejo no se espeja.</summary>
    public long? ReversesTransactionId { get; init; }

    /// <summary>El hecho que la llevó (bandeja, sin FK); nulo sólo en <c>OpeningBalance</c>.</summary>
    public Guid? MessagePublicId { get; init; }

    // ------------------------------------------------------------------------------------------- familias del signo --

    /// <summary>Los conceptos del titular (§0.2): <c>+</c> = la Cartera le debe.</summary>
    public static IReadOnlySet<MemberAccountConcept> ConceptosDelTitular { get; } = new HashSet<MemberAccountConcept>
    {
        MemberAccountConcept.Principal, MemberAccountConcept.InterestPayable, MemberAccountConcept.ContributionMinimum,
        MemberAccountConcept.ContributionOrdinary, MemberAccountConcept.ContributionExtraordinary, MemberAccountConcept.ContributionRevaluation,
        MemberAccountConcept.Bonus,
    };

    /// <summary>Las clases de aporte (§0.2): el saldo de una cuenta de aportes es su suma.</summary>
    public static IReadOnlySet<MemberAccountConcept> ConceptosDeAportes { get; } = new HashSet<MemberAccountConcept>
    {
        MemberAccountConcept.ContributionMinimum, MemberAccountConcept.ContributionOrdinary, MemberAccountConcept.ContributionExtraordinary,
        MemberAccountConcept.ContributionRevaluation,
    };

    /// <summary>Memoria de lo retenido (§0.2): no forma parte del saldo del titular ni del disponible.</summary>
    public static IReadOnlySet<MemberAccountConcept> ConceptosDeMemoria { get; } = new HashSet<MemberAccountConcept>
    {
        MemberAccountConcept.Withholding, MemberAccountConcept.Gmf,
    };

    /// <summary>Las clases de fila que admiten hasta 6 decimales: la causación y su ajuste (§0.3).</summary>
    public static IReadOnlySet<MemberAccountTransactionKind> ClasesDeCausacion { get; } = new HashSet<MemberAccountTransactionKind>
    {
        MemberAccountTransactionKind.InterestAccrued, MemberAccountTransactionKind.AccrualAdjustment, MemberAccountTransactionKind.SettledPeriodAdjustment,
    };
}
