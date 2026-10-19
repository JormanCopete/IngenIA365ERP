using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Restrictions;

/// <summary>
/// <c>LND_Garnishments</c> (feature 014, US11, T187; data-model §5.2; FR-055, FR-005, D20, R23; research §20): un embargo sobre los depósitos de una persona
/// —oficio, autoridad, demandante, valor y si es por alimentos—. Documento con estado: el embargo <b>rige desde que se registra</b> (CGP art. 593 num. 10: se
/// entiende consumado con la recepción del oficio; no espera una aprobación); quien lo registra no lo levanta (FR-005, expediente <c>Garnishment</c>).
/// Lo que retiene cada cuenta está en sus <c>AccountHold</c> (<c>Garnishment</c>), que recalcula <c>RetencionDeEmbargos</c>; lo consignado es la suma de
/// sus operaciones <c>GarnishmentConsignment</c> no reversadas (no se guarda aquí). Transiciones: <c>Active → Released</c> (desembargo total) y
/// <c>Rejected</c> al nacer si el oficio sólo recae sobre aportes (Ley 79 de 1988 art. 49), con su respuesta al juzgado. (nuevo)
/// </summary>
public class Garnishment : AuditableEntity
{
    public const string CodigoNoEncontrado = "Lending.Garnishment.NotFound";
    public const string CodigoYaLevantado = "Lending.Garnishment.AlreadyReleased";
    public const int LargoDelNombre = 200;
    public const int LargoDelCodigo = 30;
    public const int LargoDelRadicado = 40;
    public const int LargoDelDocumento = 20;
    public const int LargoDeLaCuentaBancaria = 25;
    public const int LargoDeLasNotas = 500;

    /// <summary>El embargado.</summary>
    public int PersonId { get; set; }

    public GarnishmentAuthority Authority { get; set; } = GarnishmentAuthority.Court;

    /// <summary>El juzgado o la entidad como persona del maestro: es el tercero de los embargos por consignar.</summary>
    public int CourtPersonId { get; set; }

    public string CourtName { get; set; } = string.Empty;

    public string? CourtCode { get; set; }

    /// <summary>Radicado del proceso.</summary>
    public string ProcessNumber { get; set; } = string.Empty;

    /// <summary>Número del oficio (la referencia que cita la respuesta).</summary>
    public string OrderNumber { get; set; } = string.Empty;

    public DateOnly OrderDate { get; set; }

    /// <summary>Recibido en la cooperativa: desde ese día rige y corre el plazo de respuesta.</summary>
    public DateOnly ReceivedOn { get; set; }

    public string ClaimantName { get; set; } = string.Empty;

    public string? ClaimantTaxId { get; set; }

    public int? ClaimantPersonId { get; set; }

    /// <summary>El valor de la medida; nulo = sin límite (CGP art. 593 num. 10: el juez lo fija con el crédito y las costas más un 50 %).</summary>
    public decimal? Amount { get; set; }

    /// <summary>Por alimentos: el límite inembargable no aplica (CGP art. 594 num. 2).</summary>
    public bool IsChildSupport { get; set; }

    public GarnishmentAccountsScope AccountsScope { get; set; } = GarnishmentAccountsScope.AllSavings;

    /// <summary>Con <c>Specific</c>: los Id de las cuentas que nombra el oficio, en JSON.</summary>
    public string? SpecificAccountIdsJson { get; set; }

    /// <summary>¿El oficio nombra también los aportes? (se responde que no se embargan).</summary>
    public bool IncludesContributions { get; set; }

    /// <summary>Cuenta de depósitos judiciales a la que se consigna.</summary>
    public int? DestinationBankId { get; set; }

    public string? DestinationBankAccountNumber { get; set; }

    public Guid OrderAttachmentPublicId { get; set; }

    public GarnishmentStatus Status { get; set; } = GarnishmentStatus.Active;

    /// <summary>Plazo informativo de la respuesta (<c>Embargos.DiasHabilesParaResponder</c>; avisa, no bloquea).</summary>
    public DateOnly? ResponseDueOn { get; set; }

    /// <summary>Nulo en la migración (lo registró el sistema anterior).</summary>
    public int? RegisteredByUserId { get; set; }

    public DateTime RegisteredAt { get; set; }

    public string? Notes { get; set; }

    public string? ReleaseOrderNumber { get; set; }

    public DateOnly? ReleaseOrderDate { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public int? ReleasedByUserId { get; set; }

    public Guid? ReleaseAttachmentPublicId { get; set; }

    public string? ReleaseReason { get; set; }

    // ------------------------------------------------------------------------------------------------ comportamiento --

    /// <summary>¿Retiene? (el embargo activo).</summary>
    public bool Vigente => Status == GarnishmentStatus.Active && !IsDeleted;

    /// <summary>El desembargo total: libera lo retenido (lo hace <c>RetencionDeEmbargos</c>) y conserva la historia.</summary>
    public void Levantar(string oficio, DateOnly fechaDelOficio, Guid? soporte, string motivo, int usuario, DateTime instante)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oficio);
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (Status != GarnishmentStatus.Active) throw new InvalidOperationException($"El embargo está «{Status}»: sólo se levanta uno activo.");
        Status = GarnishmentStatus.Released;
        ReleaseOrderNumber = Recortar(oficio, LargoDelRadicado);
        ReleaseOrderDate = fechaDelOficio;
        ReleaseAttachmentPublicId = soporte;
        ReleaseReason = Recortar(motivo, LargoDeLasNotas);
        ReleasedAt = instante;
        ReleasedByUserId = usuario;
    }

    /// <summary>El desembargo parcial: baja el valor de la medida y el embargo sigue activo.</summary>
    public void Reducir(decimal valor)
    {
        if (Status != GarnishmentStatus.Active) throw new InvalidOperationException($"El embargo está «{Status}»: sólo se reduce uno activo.");
        if (Amount is not { } actual || valor <= 0m || valor >= actual)
            throw new ArgumentOutOfRangeException(nameof(valor), valor, "Un desembargo parcial baja el valor de una medida con valor, sin agotarlo.");
        Amount = actual - valor;
    }

    private static string Recortar(string texto, int largo)
    {
        var t = texto.Trim();
        return t.Length <= largo ? t : t[..largo];
    }
}

/// <summary>
/// <c>LND_GarnishmentResponses</c> (feature 014, US11, T187; data-model §5.2.1): una respuesta enviada al juzgado —acuse, informe de saldos, aportes
/// inembargables, aviso de consignación, acuse del desembargo— con el texto que se envió (la plantilla de la cooperativa ya llenada) y el soporte firmado.
/// El documento emitido de la Cartera (<c>IssuedDocumentId</c>) lo pone US13; hasta entonces nulo (research E158). Hecho de sólo inserción. (nuevo)
/// </summary>
public class GarnishmentResponse : AuditableEntity, IHechoInmutable
{
    public const int LargoDelTexto = 4000;

    public int GarnishmentId { get; init; }

    public GarnishmentResponseKind Kind { get; init; }

    public int? IssuedDocumentId { get; init; }

    public Guid? AttachmentPublicId { get; init; }

    public DateOnly SentOn { get; init; }

    public decimal? RetainedAmount { get; init; }

    public decimal? ConsignedAmount { get; init; }

    public string? Text { get; init; }

    public DateTime RespondedAt { get; init; }

    public int RespondedByUserId { get; init; }
}
