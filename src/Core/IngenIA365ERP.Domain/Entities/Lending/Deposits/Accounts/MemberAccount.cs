using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>
/// <c>LND_MemberAccounts</c> (feature 014, K7; data-model §3.1; FR-001, FR-019, FR-030 a FR-039, FR-056, FR-084, R02): la cuenta del asociado
/// —ahorro a la vista, contractual, permanente, CDAT o aportes—. Una sola entidad para captación y aportes (R02): la naturaleza la copia de su
/// línea y la clase de captación es nula en aportes. <b>No guarda saldos</b> (FR-001): el saldo de un concepto es la suma de sus filas en
/// <c>LND_MemberAccountTransactions</c>. Documento con estado: sólo cambian las columnas que dice cada transición; nunca se borra, se cancela
/// (I-3). Nace siempre con su <c>LND_MemberAccountControls</c> (data-model §4.4). El número lo asigna <c>NumeradorDeCartera</c> al final del
/// orden de cerrojos; un borrador de apertura no consume número (§0.4).
/// </summary>
public class MemberAccount : AuditableEntity
{
    /// <summary>Una cuenta cerrada no recibe operaciones (data-model §3.1).</summary>
    public const string CodigoCerrada = "Lending.Savings.AccountClosed";

    /// <summary>La naturaleza de la cuenta no es la de su línea, o es colocación.</summary>
    public const string CodigoNaturalezaDistinta = "Lending.CreditLine.NatureMismatch";

    /// <summary>Largo del número de cuenta (<c>nvarchar(20)</c>, §0.4).</summary>
    public const int LargoDelNumero = 20;

    /// <summary>Consecutivo por línea (§0.4); único sin filtro. Vacío mientras la cuenta es un borrador sin numerar.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Línea <c>Deposit</c> o <c>Contribution</c>.</summary>
    public int CreditLineId { get; set; }

    /// <summary>Copia de la naturaleza de la línea; nunca <c>Placement</c> (<c>CK_LND_MemberAccounts_Nature</c>).</summary>
    public LineNature Nature { get; set; }

    /// <summary>Obligatoria en <c>Deposit</c>, nula en <c>Contribution</c> (<c>CK_LND_MemberAccounts_Class</c>).</summary>
    public DepositClass? DepositClass { get; set; }

    /// <summary>Titular; inmutable. Un menor es el titular (FR-033).</summary>
    public int HolderPersonId { get; set; }

    /// <summary>Oficina de la cuenta: alcance y dimensión contable.</summary>
    public int BranchId { get; set; }

    public HandlingMode HandlingMode { get; set; } = HandlingMode.Indistinct;

    public MemberAccountStatus Status { get; set; } = MemberAccountStatus.Opening;

    public DateTime StatusChangedAt { get; set; }

    public MemberAccountOrigin Origin { get; set; } = MemberAccountOrigin.Opening;

    public DateOnly OpenedOn { get; set; }

    /// <summary>Nulo en la migración (lo hace el proceso).</summary>
    public int? OpenedByUserId { get; set; }

    /// <summary>Día en que pasó a <c>Active</c>.</summary>
    public DateOnly? ActivatedOn { get; set; }

    /// <summary>Con <c>Status = Inactive</c>.</summary>
    public DateOnly? InactiveSince { get; set; }

    /// <summary>CDAT o contractual vencido sin prórroga.</summary>
    public DateOnly? MaturedOn { get; set; }

    public DateTime? ClosingRequestedAt { get; set; }

    public DateOnly? ClosedOn { get; set; }

    public int? ClosedByUserId { get; set; }

    public string? CloseReason { get; set; }

    /// <summary>Marca de dimensión contable y de pantalla; la escribe sólo el registro de embargos en la transacción de la retención (§5.2).</summary>
    public bool IsGarnished { get; set; }

    /// <summary>Mayoría de edad del titular (FR-033, R22) o reingreso; se limpia al actualizar el conocimiento y la autorización de datos.</summary>
    public bool RequiresDataUpdate { get; set; }

    public DateOnly? DataUpdateRequiredSince { get; set; }

    /// <summary>Sólo en el contractual.</summary>
    public ContractualStatus? ContractualStatus { get; set; }

    /// <summary>Negativo técnico por un reverso (FR-056): bloquea retiros y alerta.</summary>
    public DateOnly? TechnicalNegativeSince { get; set; }

    /// <summary>FR-065: la causación suspendida por embargo total o fallecimiento.</summary>
    public DateOnly? AccrualSuspendedSince { get; set; }

    /// <summary><c>TotalGarnishment</c> o <c>Deceased</c>.</summary>
    public string? AccrualSuspensionReason { get; set; }

    /// <summary>Número de origen en la migración (búsqueda; no identifica).</summary>
    public string? SourceAccountNumber { get; set; }

    public int? MigrationLoadId { get; set; }

    /// <summary>
    /// US6 (T170; research E135): las condiciones que se pidieron al constituir un CDAT mientras espera el fondeo (o la decisión de su tasa negociada), en
    /// JSON. Se sellan en <c>LND_MemberAccountTermsVersions</c> al fondear y entonces se limpia: no es un hecho, es la propuesta de la apertura.
    /// </summary>
    public string? PendingTermsJson { get; set; }

    /// <summary>
    /// US6 (T173; api.md §5 <c>PUT /term-deposits/{id}/maturity-instruction</c>; research E136): la instrucción al vencimiento que el titular cambió hasta el fin
    /// de la gracia —prorrogar, renovar con condiciones o cancelar y pagar— con su destino, en JSON. Manda sobre la sellada en la versión de condiciones
    /// (que es hecho y no se edita) hasta que la versión siguiente la selle o el CDAT se cancele; nula = rige la sellada.
    /// </summary>
    public string? MaturityInstructionJson { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary>
    /// Una cuenta nueva en <c>Opening</c> (data-model §3.1): la naturaleza es la de su línea y no es colocación; la clase de captación es
    /// obligatoria en captación y nula en aportes. Sin número: lo pone el numerador al final del orden de cerrojos.
    /// </summary>
    public static MemberAccount Abrir(int creditLineId, LineNature naturaleza, DepositClass? clase, int titularId, int oficinaId, HandlingMode manejo,
        DateOnly abiertaEl, int? abiertaPor, DateTime instante, MemberAccountOrigin origen = MemberAccountOrigin.Opening, int? cargaId = null,
        string? numeroDeOrigen = null)
    {
        ReglaDeCarteraException.SiHay(ValidarNaturaleza(naturaleza, clase));
        ArgumentOutOfRangeException.ThrowIfLessThan(creditLineId, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(titularId, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(oficinaId, 1);
        if (origen == MemberAccountOrigin.Migration && cargaId is null)
            throw new ArgumentException("Una cuenta migrada nombra su carga.", nameof(cargaId));
        return new MemberAccount
        {
            CreditLineId = creditLineId,
            Nature = naturaleza,
            DepositClass = clase,
            HolderPersonId = titularId,
            BranchId = oficinaId,
            HandlingMode = manejo,
            Status = MemberAccountStatus.Opening,
            StatusChangedAt = instante,
            Origin = origen,
            OpenedOn = abiertaEl,
            OpenedByUserId = abiertaPor,
            MigrationLoadId = cargaId,
            SourceAccountNumber = string.IsNullOrWhiteSpace(numeroDeOrigen) ? null : numeroDeOrigen.Trim(),
            ContractualStatus = clase == Enums.Lending.DepositClass.Contractual ? Enums.Lending.ContractualStatus.Current : null,
        };
    }

    /// <summary>Nulo si la naturaleza y la clase son coherentes (§3.1: <c>CK_LND_MemberAccounts_Nature</c> y <c>_Class</c>).</summary>
    public static ProblemaDeCartera? ValidarNaturaleza(LineNature naturaleza, DepositClass? clase) => naturaleza switch
    {
        LineNature.Deposit when clase is null => ProblemaDeCartera.Con(CodigoNaturalezaDistinta,
            "Una cuenta de captación lleva su clase: a la vista, contractual, permanente o CDAT.", "depositClass", null),
        LineNature.Contribution when clase is not null => ProblemaDeCartera.Con(CodigoNaturalezaDistinta,
            "Una cuenta de aportes no lleva clase de captación.", "depositClass", clase.ToString()),
        LineNature.Deposit or LineNature.Contribution => null,
        _ => ProblemaDeCartera.Con(CodigoNaturalezaDistinta, "Una línea de colocación no abre cuentas: sólo las de captación y de aportes.",
            "nature", naturaleza.ToString()),
    };

    /// <summary>Las transiciones de data-model §3.1.</summary>
    private static readonly IReadOnlyDictionary<MemberAccountStatus, MemberAccountStatus[]> Transiciones = new Dictionary<MemberAccountStatus, MemberAccountStatus[]>
    {
        [MemberAccountStatus.Opening] = [MemberAccountStatus.Active, MemberAccountStatus.Closed],
        [MemberAccountStatus.Active] = [MemberAccountStatus.Inactive, MemberAccountStatus.Matured, MemberAccountStatus.Closing, MemberAccountStatus.Closed],
        [MemberAccountStatus.Inactive] = [MemberAccountStatus.Active, MemberAccountStatus.Closing, MemberAccountStatus.Closed],
        [MemberAccountStatus.Matured] = [MemberAccountStatus.Closing, MemberAccountStatus.Closed],
        [MemberAccountStatus.Closing] = [MemberAccountStatus.Closed, MemberAccountStatus.Active, MemberAccountStatus.Inactive, MemberAccountStatus.Matured],
    };

    /// <summary>¿Puede la cuenta pasar de <paramref name="desde"/> a <paramref name="hacia"/>? (§3.1; <c>Closed</c> es final).</summary>
    public static bool PuedePasar(MemberAccountStatus desde, MemberAccountStatus hacia) =>
        Transiciones.TryGetValue(desde, out var destinos) && destinos.Contains(hacia);

    /// <summary>Nulo si la cuenta recibe operaciones: toda menos la cerrada (<c>Lending.Savings.AccountClosed</c>).</summary>
    public ProblemaDeCartera? AdmiteOperaciones() => Status == MemberAccountStatus.Closed
        ? ProblemaDeCartera.Con(CodigoCerrada, $"La cuenta {Number} está cancelada: no recibe operaciones.", "status", Status.ToString())
        : null;

    /// <summary>¿Está viva? (toda menos la cerrada; la de aportes viva es única por persona).</summary>
    public bool Viva => Status != MemberAccountStatus.Closed;

    /// <summary><c>Opening → Active</c>: la apertura completa (FR-006, FR-032).</summary>
    public void Activar(DateOnly dia, DateTime instante)
    {
        Pasar(MemberAccountStatus.Active, instante);
        ActivatedOn = dia;
        InactiveSince = null;
    }

    /// <summary><c>Active → Inactive</c>: el paso diario de inactividad (FR-084).</summary>
    public void Inactivar(DateOnly desde, DateTime instante)
    {
        Pasar(MemberAccountStatus.Inactive, instante);
        InactiveSince = desde;
    }

    /// <summary><c>Inactive → Active</c>: un movimiento del titular la reactiva (FR-058, FR-084).</summary>
    public void Reactivar(DateTime instante)
    {
        if (Status != MemberAccountStatus.Inactive)
            throw new InvalidOperationException($"La cuenta {Number} está en «{Status}»: sólo se reactiva una inactiva.");
        Pasar(MemberAccountStatus.Active, instante);
        InactiveSince = null;
    }

    /// <summary><c>Active → Matured</c>: CDAT tras la gracia sin prórroga, o contractual al vencer sin instrucción.</summary>
    public void Vencer(DateOnly dia, DateTime instante)
    {
        if (DepositClass is not (Enums.Lending.DepositClass.TermDeposit or Enums.Lending.DepositClass.Contractual))
            throw new InvalidOperationException($"La cuenta {Number} no vence: sólo vencen el CDAT y el contractual.");
        Pasar(MemberAccountStatus.Matured, instante);
        MaturedOn = dia;
    }

    /// <summary><c>* → Closing</c>: hay una orden de giro en curso para el saldo (§3.9).</summary>
    public void PedirCierre(DateTime instante)
    {
        Pasar(MemberAccountStatus.Closing, instante);
        ClosingRequestedAt = instante;
    }

    /// <summary>
    /// <c>Closing → {anterior}</c>: la orden de giro rechazada o anulada devuelve la cuenta a su estado anterior y reintegra el saldo (§3.9, FR-091).
    /// </summary>
    public void RevertirCierre(MemberAccountStatus anterior, DateTime instante)
    {
        if (Status != MemberAccountStatus.Closing)
            throw new InvalidOperationException($"La cuenta {Number} está en «{Status}»: sólo se revierte el cierre de una cuenta en cierre.");
        if (anterior is not (MemberAccountStatus.Active or MemberAccountStatus.Inactive or MemberAccountStatus.Matured))
            throw new ArgumentOutOfRangeException(nameof(anterior), anterior, "Una cuenta en cierre vuelve a activa, inactiva o vencida.");
        Pasar(anterior, instante);
        ClosingRequestedAt = null;
    }

    /// <summary><c>* → Closed</c>: cancelación, retiro, entrega, no reclamados, o el desistimiento de una apertura (final).</summary>
    public void Cerrar(DateOnly dia, int? usuario, string motivo, DateTime instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        Pasar(MemberAccountStatus.Closed, instante);
        ClosedOn = dia;
        ClosedByUserId = usuario;
        var texto = motivo.Trim();
        CloseReason = texto.Length <= 300 ? texto : texto[..300];
    }

    /// <summary>
    /// Decisión del dueño del 2026-10-18 (D44): la única salida de <c>Closed</c>. El reverso tardío de un pago del cruce al retiro, con la cuenta ya
    /// cancelada, la reabre <b>en cierre</b> (<c>Closed → Closing</c>): el débito del cruce vuelve como saldo a favor del exasociado y espera su orden de
    /// giro. Guarda en el motivo de cierre que se reabrió.
    /// </summary>
    public void ReabrirPorReversoTardio(DateTime instante, string motivo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (Status != MemberAccountStatus.Closed)
            throw new InvalidOperationException($"La cuenta {Number} está en «{Status}»: sólo se reabre una cancelada.");
        Status = MemberAccountStatus.Closing;
        StatusChangedAt = instante;
        ClosingRequestedAt = instante;
        ClosedOn = null;
        ClosedByUserId = null;
        var texto = $"Reabierta: {motivo.Trim()}";
        CloseReason = texto.Length <= 300 ? texto : texto[..300];
    }

    private void Pasar(MemberAccountStatus hacia, DateTime instante)
    {
        if (!PuedePasar(Status, hacia))
            throw new InvalidOperationException($"La cuenta {Number} está en «{Status}» y no puede pasar a «{hacia}» (data-model §3.1).");
        Status = hacia;
        StatusChangedAt = instante;
    }
}
