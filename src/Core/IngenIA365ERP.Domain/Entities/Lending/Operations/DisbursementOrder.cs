using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_DisbursementOrders</c> (feature 013; data-model §7.4; FR-045, FR-047, FR-048, D27): orden de giro de un tramo, con
/// beneficiario y cuenta sellados (I-1). Nunca en especie; el neto es el bruto menos lo recogido y es positivo. El estado sólo lo
/// mueve el <c>ResultadoDelGiro</c> consumido (§7.5). No es hecho. US3 (T0433) le suma su comportamiento: la emisión con el neto
/// positivo, la anulación pedida y las transiciones de interfaces.md §4.3.
/// </summary>
public class DisbursementOrder : AuditableEntity
{
    public int LoanId { get; set; }

    /// <summary>El tramo (<c>LND_LoanOperations</c>, bigint); una orden por tramo.</summary>
    public long DisbursementOperationId { get; set; }

    /// <summary>Numerador <c>DisbursementOrder</c>; único.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Nunca <c>InKind</c> (<c>CK_LND_DisbursementOrders_Form</c>).</summary>
    public DisbursementForm Form { get; set; }

    /// <summary>Deudor o tercero.</summary>
    public int BeneficiaryPersonId { get; set; }

    /// <summary>Copia tal como se emitió (I-1).</summary>
    public string BeneficiaryTaxIdType { get; set; } = string.Empty;

    public string BeneficiaryTaxId { get; set; } = string.Empty;

    public string BeneficiaryName { get; set; } = string.Empty;

    /// <summary>Obligatorio en <c>Transfer</c>; propuesto desde la persona y corregible al desembolsar (I10).</summary>
    public int? DestinationBankId { get; set; }

    public byte? DestinationAccountType { get; set; }

    public string? DestinationAccountNumber { get; set; }

    public decimal GrossAmount { get; set; }

    public decimal PayoffAmount { get; set; }

    /// <summary><c>GrossAmount − PayoffAmount</c>, mayor que cero (<c>CK_LND_DisbursementOrders_Net</c>).</summary>
    public decimal NetAmount { get; set; }

    public DateOnly ScheduledDate { get; set; }

    public DisbursementOrderStatus Status { get; set; } = DisbursementOrderStatus.Issued;

    public DateTime IssuedAt { get; set; }

    public int IssuedByUserId { get; set; }

    public DateTime? StatusChangedAt { get; set; }

    /// <summary>Emisión + <c>Giros.DiasSinResultado</c> hábiles; pasado, alerta <c>Cartera.GiroSinResultado</c>.</summary>
    public DateOnly ResultDueOn { get; set; }

    /// <summary>Anulación pedida, a la espera de Tesorería: el estado no cambia hasta su respuesta.</summary>
    public DateTime? VoidRequestedAt { get; set; }

    public int? VoidRequestedByUserId { get; set; }

    public string? VoidReason { get; set; }

    /// <summary>Nuevo giro enlazado a la orden anterior.</summary>
    public int? PreviousOrderId { get; set; }

    /// <summary>Se decide al desembolsar y no cambia: la paga Cartera por archivo plano (C10). Sólo en <c>Transfer</c>.</summary>
    public bool PaidByFile { get; set; }

    /// <summary>Archivo no anulado que la lleva; la referencia vive sólo aquí (C10).</summary>
    public int? CurrentDisbursementFileId { get; set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0433) --

    public const string CodigoNoAnulable = "Lending.DisbursementOrder.NotVoidable";
    public const string CodigoEnArchivo = "Lending.DisbursementOrder.InFile";
    public const string CodigoResultadoInvalido = "Lending.DisbursementOrder.InvalidResult";

    /// <summary>Hay una anulación pedida a Tesorería que todavía no tiene respuesta (el estado no cambia hasta entonces).</summary>
    public bool AnulacionPedida => VoidRequestedAt is not null;

    /// <summary>
    /// La orden de un tramo (data-model §7.4): el neto es el bruto menos lo recogido y es positivo (<c>CK_LND_DisbursementOrders_Net</c>),
    /// nunca en especie (<c>CK_LND_DisbursementOrders_Form</c>) y <paramref name="pagadaPorArchivo"/> sólo en transferencia
    /// (<c>CK_LND_DisbursementOrders_PaidByFile</c>). El número lo pone el numerador después.
    /// </summary>
    public static DisbursementOrder Emitir(int loanId, long operacionId, DisbursementForm forma, int beneficiarioId, string tipoDeDocumento,
        string documento, string nombre, int? bancoId, byte? tipoDeCuenta, string? cuenta, decimal bruto, decimal recogido, DateOnly fechaProgramada,
        DateOnly resultadoHasta, bool pagadaPorArchivo, DateTime instante, int usuarioId, int? ordenAnterior = null)
    {
        if (forma == DisbursementForm.InKind)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoResultadoInvalido, "Un desembolso en especie no emite orden de giro.", "form", forma.ToString()));
        if (bruto - recogido <= 0m)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoResultadoInvalido,
                "El neto a girar (bruto menos lo recogido) debe ser mayor que cero.", "netAmount", bruto - recogido));
        return new DisbursementOrder
        {
            LoanId = loanId,
            DisbursementOperationId = operacionId,
            Form = forma,
            BeneficiaryPersonId = beneficiarioId,
            BeneficiaryTaxIdType = tipoDeDocumento,
            BeneficiaryTaxId = documento,
            BeneficiaryName = nombre,
            DestinationBankId = bancoId,
            DestinationAccountType = tipoDeCuenta,
            DestinationAccountNumber = cuenta,
            GrossAmount = bruto,
            PayoffAmount = recogido,
            NetAmount = bruto - recogido,
            ScheduledDate = fechaProgramada,
            Status = DisbursementOrderStatus.Issued,
            IssuedAt = instante,
            IssuedByUserId = usuarioId,
            ResultDueOn = resultadoHasta,
            PaidByFile = pagadaPorArchivo && forma == DisbursementForm.Transfer,
            PreviousOrderId = ordenAnterior,
        };
    }

    /// <summary>Nulo si se puede pedir la anulación: <c>Issued</c>, sin anulación pedida y fuera de un archivo vivo (§7.4).</summary>
    public ProblemaDeCartera? PuedePedirAnulacion(Guid? archivoVivo)
    {
        if (Status != DisbursementOrderStatus.Issued || AnulacionPedida)
            return ProblemaDeCartera.Con(CodigoNoAnulable,
                AnulacionPedida
                    ? "La anulación de esta orden ya se pidió a Tesorería y espera su respuesta."
                    : "Sólo se anula una orden emitida que Tesorería todavía no ha girado.",
                "status", Status.ToString());
        if (CurrentDisbursementFileId is not null)
            return ProblemaDeCartera.Con(CodigoEnArchivo,
                "La orden va en un archivo de desembolsos vivo: anule primero el archivo y después la orden.", "filePublicId", archivoVivo);
        return null;
    }

    /// <summary>Anota la anulación pedida; el estado sigue <c>Issued</c> hasta que Tesorería responda (C12).</summary>
    public void PedirAnulacion(int usuarioId, DateTime instante, string motivo, Guid? archivoVivo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        ReglaDeCarteraException.SiHay(PuedePedirAnulacion(archivoVivo));
        VoidRequestedAt = instante;
        VoidRequestedByUserId = usuarioId;
        VoidReason = motivo.Trim();
    }

    /// <summary>
    /// ¿Cabe el resultado <paramref name="hacia"/> sobre la orden? (interfaces.md §4.3, el contrato dueño del giro, C13): desde <c>Issued</c>,
    /// girada o rechazada; anulada sólo con la anulación pedida; cheque no cobrado sólo después de girada y en cheque. Un cheque no cobrado
    /// que llega antes que su «girada» no cabe: se reprocesa cuando ésta llegue.
    /// </summary>
    public bool AdmiteResultado(DisbursementOrderStatus hacia) => (Status, hacia) switch
    {
        (DisbursementOrderStatus.Issued, DisbursementOrderStatus.Disbursed) => true,
        (DisbursementOrderStatus.Issued, DisbursementOrderStatus.Rejected) => true,
        (DisbursementOrderStatus.Issued, DisbursementOrderStatus.Voided) => AnulacionPedida,
        (DisbursementOrderStatus.Disbursed, DisbursementOrderStatus.CheckNotCashed) => Form == DisbursementForm.Check,
        _ => false,
    };

    /// <summary>
    /// ¿Puede ir a un archivo de desembolsos? (§7.4, §7.6; T78): emitida, para pagarse por archivo (sólo transferencia), sin anulación pedida y sin
    /// otro archivo vivo.
    /// </summary>
    public bool ElegibleParaArchivo =>
        Status == DisbursementOrderStatus.Issued && PaidByFile && Form == DisbursementForm.Transfer && !AnulacionPedida && CurrentDisbursementFileId is null;

    /// <summary>La orden queda en el archivo <paramref name="archivoId"/> (feature 013, US3, T0454): a lo sumo un archivo vivo.</summary>
    public void AsignarArchivo(int archivoId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(archivoId, 1);
        if (!ElegibleParaArchivo)
            throw new InvalidOperationException($"La orden {Number} no puede ir a un archivo: está en «{Status}» o ya va en otro archivo vivo.");
        CurrentDisbursementFileId = archivoId;
    }

    /// <summary>Al anular el archivo <paramref name="archivoId"/>, la orden queda libre para otro; sigue para pagarse por archivo (T78, C10).</summary>
    public void LiberarArchivo(int archivoId)
    {
        if (CurrentDisbursementFileId == archivoId) CurrentDisbursementFileId = null;
    }

    /// <summary>Los resultados que reversan el desembolso desde su origen (§7.11).</summary>
    public static bool Reversa(DisbursementOrderStatus resultado) =>
        resultado is DisbursementOrderStatus.Rejected or DisbursementOrderStatus.CheckNotCashed or DisbursementOrderStatus.Voided;

    /// <summary>Mueve el estado por un resultado admitido (<see cref="AdmiteResultado"/>).</summary>
    public void RegistrarResultado(DisbursementOrderStatus hacia, DateTime instante)
    {
        if (!AdmiteResultado(hacia))
            throw new ReglaDeCarteraException(new ProblemaDeCartera(CodigoResultadoInvalido, $"La orden en «{Status}» no admite el resultado «{hacia}».",
                new Dictionary<string, object?> { ["reason"] = "InvalidTransition", ["from"] = Status.ToString(), ["to"] = hacia.ToString() }));
        Status = hacia;
        StatusChangedAt = instante;
    }
}
