using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;

/// <summary>
/// <c>LND_MemberAccountOperations</c> (feature 014, K7; data-model §4.1; FR-001, FR-002, FR-009, FR-050 a FR-059, FR-086 a FR-092, R03): el hecho
/// primario —lo que decidió una persona, un recaudador o el proceso— y <b>el comprobante de la operación</b>. Toca una cuenta principal y, en
/// traslados, una contraparte; sus filas del libro llevan cada una su cuenta. Hecho de sólo inserción (<see cref="IHechoInmutable"/>,
/// <c>bigint</c>) que escribe <c>RegistroDeCuentas</c> bajo el cerrojo; sólo <see cref="ClearedAt"/> y <see cref="ReversedByOperationId"/> se
/// escriben una vez.
///
/// <para>Las columnas que apuntan a tablas de entregas posteriores nacen con ellas (data-model §8.2): <c>GarnishmentId</c> y
/// <c>AccountChargeId</c> (K8), <c>AssemblyDistributionId</c> y <c>DeceasedHolderCaseId</c> (K10). <see cref="MembershipWithdrawalId"/> es de K7
/// pero su tabla nace con US10 (T096), que le pone la FK. La orden de giro no se nombra desde aquí: la orden nombra su operación
/// (<c>LND_AccountPayoutOrders.OperationId</c>, único), porque un hecho no se reescribe para enlazarla (research E16).</para>
/// </summary>
public class MemberAccountOperation : AuditableEntityLong, IHechoInmutable
{
    /// <summary>Cuenta principal.</summary>
    public int MemberAccountId { get; init; }

    /// <summary>Navegación para que <c>RegistroDeCuentas</c> enlace en el mismo guardado una operación con su cuenta todavía sin Id.</summary>
    public Accounts.MemberAccount? MemberAccount { get; init; }

    /// <summary>Traslado, cruce de aportes a ahorro, destino de intereses.</summary>
    public int? CounterpartMemberAccountId { get; init; }

    public MemberAccountOperationKind Kind { get; init; }

    /// <summary>Comprobante; numerador <c>MemberAccountOperation</c> con ámbito = la clase. Único.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Por cuenta principal, bajo el cerrojo; único con <see cref="MemberAccountId"/>.</summary>
    public int Sequence { get; init; }

    /// <summary>Fecha valor; hacia atrás hasta <c>Captaciones.DiasMaximosFechaValor</c> (D11).</summary>
    public DateOnly ValueDate { get; init; }

    public DateOnly AccountingDate { get; init; }

    public DateTime RegisteredAt { get; init; }

    /// <summary>Valor de la operación; 0 en las que no mueven dinero.</summary>
    public decimal Amount { get; init; }

    // ---------------------------------------------------------------------------------------------- quién actúa --

    public ActorKind ActorKind { get; init; }

    public int? ActorUserId { get; init; }

    public Guid? OriginUserCentralId { get; init; }

    // ------------------------------------------------------------------------------------------------ recaudo --

    /// <summary><c>ClaseDeRecaudador</c> del puerto, como texto.</summary>
    public string? CollectorClass { get; init; }

    /// <summary>Código del participante (<c>NOM</c>, <c>TES</c>, <c>TST</c>; <c>CAR</c> en las operaciones internas).</summary>
    public string? CollectorCode { get; init; }

    /// <summary>Clave de negocio (FR-009); única con <see cref="CollectorCode"/> si no es nula (SC-005).</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary><c>HuellaDeOperacion</c> de la solicitud.</summary>
    public string? RequestSha256 { get; init; }

    /// <summary>Versión adoptada de <c>IContratoDeCuentas</c>.</summary>
    public short? ContractVersion { get; init; }

    /// <summary><c>ClaseDeMedioDeRecaudo</c> como texto.</summary>
    public string? PaymentMeansClass { get; init; }

    public string? ReceiptNumber { get; init; }

    public DateOnly? ReceiptDate { get; init; }

    public Guid? ReceiptBranchPublicId { get; init; }

    public string? ReceiptMeansReference { get; init; }

    /// <summary>Medio con días de disponibilidad: el valor queda en canje hasta esta fecha (FR-050, C14).</summary>
    public DateOnly? ClearingUntil { get; init; }

    /// <summary>La liberación del canje; se escribe una vez.</summary>
    [EscrituraUnica]
    public DateTime? ClearedAt { get; set; }

    /// <summary>Declaración de origen de fondos sobre el umbral de efectivo (FR-059).</summary>
    public Guid? CashOriginDeclarationAttachmentPublicId { get; init; }

    // ------------------------------------------------------------------------------------------------ decisión --

    /// <summary>Aprobador presente con segundo factor: retiro sobre la atribución (FR-051), redención (FR-071).</summary>
    public int? AuthorizedByUserId { get; init; }

    public int? AuthorizationDecisionId { get; init; }

    /// <summary>Firmantes que autorizaron (manejo conjunto, límites, representante), JSON con sus Id de persona.</summary>
    public string? SignedByPersonIdsJson { get; init; }

    // ------------------------------------------------------------------------------------------------- enlaces --

    /// <summary>Cruce interno o débito automático: el pago aplicado al crédito (<c>LND_LoanOperations</c>).</summary>
    public long? RelatedLoanOperationId { get; init; }

    /// <summary>K7 (US10, T096): el retiro del asociado que la originó; la FK nace con su tabla.</summary>
    public int? MembershipWithdrawalId { get; init; }

    /// <summary>K8 (US11, T189): el embargo que consigna (<c>GarnishmentConsignment</c>) o cuya marca reclasifica (<c>Reclassification</c>).</summary>
    public int? GarnishmentId { get; init; }

    /// <summary>K10 (US12, T202): la decisión de asamblea que la originó (revalorización, excedentes o amortización).</summary>
    public int? AssemblyDistributionId { get; init; }

    /// <summary>Resultado del giro, factura del cargo.</summary>
    public int? InboundMessageId { get; init; }

    /// <summary>
    /// Feature 014 (T091, D36): el resultado de pagaduría «descontado pendiente de giro» que esta consignación gira (<c>ExternalPaymasterDeduction</c> con
    /// <c>DeductionFileLinePublicId</c>); mientras la consignación no se reverse, ese resultado ya no está pendiente.
    /// </summary>
    public int? PayrollDeductionAccountResultId { get; init; }

    /// <summary>Carga de migración (<c>OpeningBalance</c>).</summary>
    public int? MigrationLoadId { get; init; }

    /// <summary>La operación que ésta reversa; única si no es nula.</summary>
    public long? ReversesOperationId { get; init; }

    /// <summary>El reverso que la anuló; se escribe una vez.</summary>
    [EscrituraUnica]
    public long? ReversedByOperationId { get; set; }

    /// <summary>Obligatorio en reversos, cargos manuales y retiros excepcionales.</summary>
    public string? Reason { get; init; }

    /// <summary>JSON canónico de las entradas: valor, fecha valor, medio, firmas, simulación.</summary>
    public string Payload { get; init; } = "{}";

    public string InputsSha256 { get; init; } = string.Empty;

    public string RulesVersion { get; init; } = string.Empty;

    /// <summary>Sello de la validación previa contable (D23) como texto.</summary>
    public string? PrevalidationOutcome { get; init; }
}
