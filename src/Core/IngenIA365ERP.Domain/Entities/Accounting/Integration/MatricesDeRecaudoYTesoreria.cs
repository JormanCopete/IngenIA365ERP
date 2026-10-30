using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Domain.Entities.Accounting.Integration;

// Feature 015, US8 (T0256; E-04; R-22; FR-143; data-model §17.1, §17.2; contracts/contabilidad.md §5, §6): las matrices contables de Recaudo y Tesorería, sus
// recibos de contabilización y sus tipos de comprobante. Molde: ACC_LendingPostingRules, ACC_LendingPostings y ACC_LendingVoucherMappings de la 013. Ninguna tiene
// llave a tablas RCD_ ni TES_: las dimensiones van por código y los documentos por su PublicId (RecaudoNoConoceContabilidad, TesoreriaNoConoceContabilidad).

/// <summary>
/// Lo común de una regla de la matriz de Recaudo o de Tesorería: a qué cuenta va un <see cref="Role"/> de un hecho (<see cref="FactType"/>, o <c>*</c>) para una
/// combinación de dimensiones <b>por código</b>, con vigencia. Una dimensión nula vale para cualquiera; fijada, se exige por igualdad. Gana la más específica por
/// <see cref="SpecificityWeight"/> (contabilidad.md §6): caja 256, punto 128, medio 64, concepto 64, clase de orden 64, módulo, naturaleza, clase de recaudador y
/// origen de la orden 32 cada uno, cuenta propia 16, causa 8, oficina 4, hecho 2 (sólo en los roles de resultado) y clase de medio 1. Dos candidatas con el mismo
/// peso son un error de configuración que la completitud muestra. El centro de costo <b>se asigna</b> y no pesa (como C77 de la 013). Nada se borra: una versión
/// nueva cierra la anterior la víspera (<see cref="CerrarVigencia"/>). <see cref="DimensionKey"/> y <see cref="SpecificityWeight"/> los calcula
/// <see cref="Recalcular"/>; nadie los asigna a mano. (nuevo)
/// </summary>
public abstract class ReglaContableDeModulo : AuditableEntity
{
    public const int PesoCaja = 256;
    public const int PesoPunto = 128;
    public const int PesoMedio = 64;
    public const int PesoConcepto = 64;
    public const int PesoClaseDeOrden = 64;
    public const int PesoModulo = 32;
    public const int PesoNaturaleza = 32;
    public const int PesoClaseDeRecaudador = 32;
    public const int PesoOrigenDeLaOrden = 32;
    public const int PesoCuentaPropia = 16;
    public const int PesoCausa = 8;
    public const int PesoOficina = 4;
    public const int PesoHecho = 2;
    public const int PesoClaseDeMedio = 1;

    /// <summary>Lo que va donde la regla no fija valor.</summary>
    public const string Cualquiera = "*";

    /// <summary>El tipo del hecho (el <c>Type</c> del mensaje), o <c>*</c>.</summary>
    public string FactType { get; set; } = Cualquiera;

    /// <summary>Uno de los roles de contabilidad.md §5.</summary>
    public string Role { get; set; } = string.Empty;

    public string? BranchCode { get; set; }

    public string? CollectionPointCode { get; set; }

    public string? CashBoxCode { get; set; }

    public string? MeansCode { get; set; }

    /// <summary><c>ClaseDeMedioDeRecaudo</c> como número.</summary>
    public int? MeansClass { get; set; }

    public string? MiscConceptCode { get; set; }

    public string? TargetModule { get; set; }

    public string? TargetNature { get; set; }

    public string? CollectorClass { get; set; }

    public string? OwnBankAccountCode { get; set; }

    public string? OrderOrigin { get; set; }

    public string? OrderKind { get; set; }

    public string? Cause { get; set; }

    public int AccountId { get; set; }

    /// <summary>Se asigna a la línea si la cuenta lo maneja; no se compara ni pesa.</summary>
    public int? CostCenterId { get; set; }

    public DateOnly ValidFrom { get; set; }

    /// <summary>Nula = abierta.</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>Quién decidió y por qué (la fuente de la contadora).</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>La clave posicional, con <c>*</c> donde la regla no fija valor.</summary>
    public string DimensionKey { get; set; } = string.Empty;

    public int SpecificityWeight { get; set; }

    /// <summary>¿Rige en esa fecha contable?</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || fecha <= ValidTo.Value);

    /// <summary>Normaliza los códigos y recalcula la clave y el peso. Lo llama quien crea la regla.</summary>
    public void Recalcular(bool esRolDeResultado)
    {
        FactType = Texto(FactType) ?? Cualquiera;
        Role = Role.Trim();
        BranchCode = Mayusculas(BranchCode);
        CollectionPointCode = Mayusculas(CollectionPointCode);
        CashBoxCode = Mayusculas(CashBoxCode);
        MeansCode = Mayusculas(MeansCode);
        MiscConceptCode = Mayusculas(MiscConceptCode);
        TargetModule = Mayusculas(TargetModule);
        TargetNature = Texto(TargetNature);
        CollectorClass = Texto(CollectorClass);
        OwnBankAccountCode = Mayusculas(OwnBankAccountCode);
        OrderOrigin = Texto(OrderOrigin);
        OrderKind = Texto(OrderKind);
        Cause = Texto(Cause);
        Notes = Notes?.Trim() ?? string.Empty;
        DimensionKey = string.Join('|', FactType, Role, BranchCode ?? Cualquiera, CollectionPointCode ?? Cualquiera, CashBoxCode ?? Cualquiera,
            MeansCode ?? Cualquiera, MeansClass?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? Cualquiera, MiscConceptCode ?? Cualquiera,
            TargetModule ?? Cualquiera, TargetNature ?? Cualquiera, CollectorClass ?? Cualquiera, OwnBankAccountCode ?? Cualquiera, OrderOrigin ?? Cualquiera,
            OrderKind ?? Cualquiera, Cause ?? Cualquiera);
        var peso = 0;
        if (CashBoxCode is not null) peso += PesoCaja;
        if (CollectionPointCode is not null) peso += PesoPunto;
        if (MeansCode is not null) peso += PesoMedio;
        if (MiscConceptCode is not null) peso += PesoConcepto;
        if (OrderKind is not null) peso += PesoClaseDeOrden;
        if (TargetModule is not null) peso += PesoModulo;
        if (TargetNature is not null) peso += PesoNaturaleza;
        if (CollectorClass is not null) peso += PesoClaseDeRecaudador;
        if (OrderOrigin is not null) peso += PesoOrigenDeLaOrden;
        if (OwnBankAccountCode is not null) peso += PesoCuentaPropia;
        if (Cause is not null) peso += PesoCausa;
        if (BranchCode is not null) peso += PesoOficina;
        if (esRolDeResultado && FactType != Cualquiera) peso += PesoHecho;
        if (MeansClass is not null) peso += PesoClaseDeMedio;
        SpecificityWeight = peso;
    }

    /// <summary>Cierra la vigencia el día <paramref name="hasta"/> (inclusive); la víspera de <see cref="ValidFrom"/> la deja sin días vigentes.</summary>
    public void CerrarVigencia(DateOnly hasta)
    {
        if (hasta < ValidFrom.AddDays(-1))
            throw new ArgumentOutOfRangeException(nameof(hasta), hasta, "La vigencia no puede cerrarse antes de la víspera de su inicio.");
        ValidTo = hasta;
    }

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) || valor.Trim() == Cualquiera ? null : valor.Trim();

    private static string? Mayusculas(string? valor) => Texto(valor)?.ToUpperInvariant();
}

/// <summary><c>ACC_ReceiptsPostingRules</c>: la matriz de Recaudo (<c>REC</c>). (nuevo)</summary>
public class ReceiptsPostingRule : ReglaContableDeModulo;

/// <summary><c>ACC_TreasuryPostingRules</c>: la matriz de Tesorería (<c>TES</c>). (nuevo)</summary>
public class TreasuryPostingRule : ReglaContableDeModulo;

/// <summary>
/// Lo común del tipo de comprobante de un hecho de Recaudo o de Tesorería (molde <c>ACC_LendingVoucherMappings</c>): una fila por hecho, sin vigencia (un cambio vale
/// para lo que se contabilice después y queda en la auditoría). El tipo es <c>Usage = Module</c> del módulo y activo. Lo sembrado lleva <see cref="IsSeeded"/> y queda
/// «pendiente de validar» hasta que la contadora lo confirma. (nuevo)
/// </summary>
public abstract class MapeoDeComprobanteDeModulo : AuditableEntity
{
    public string FactType { get; set; } = string.Empty;

    public int VoucherTypeId { get; set; }

    public VoucherType? VoucherType { get; set; }

    /// <summary>Cruce de las líneas cuya cuenta lo exige (el número va en la línea del hecho).</summary>
    public int? CrossDocumentTypeId { get; set; }

    public CrossDocumentType? CrossDocumentType { get; set; }

    public bool IsSeeded { get; set; }

    /// <summary>Conformidad de la contadora; nula = «pendiente de validar».</summary>
    public DateTime? ValidatedAt { get; set; }

    public string? ValidatedBy { get; set; }
}

/// <summary><c>ACC_ReceiptsVoucherMappings</c>. (nuevo)</summary>
public class ReceiptsVoucherMapping : MapeoDeComprobanteDeModulo;

/// <summary><c>ACC_TreasuryVoucherMappings</c>. (nuevo)</summary>
public class TreasuryVoucherMapping : MapeoDeComprobanteDeModulo;

/// <summary>
/// Lo común del recibo de un hecho de Recaudo o de Tesorería procesado por Contabilidad (molde <c>ACC_LendingPostings</c>): uno por hecho —único
/// <see cref="MessagePublicId"/>: la colisión es «ya procesado»— y escrito en el mismo guardado que el comprobante. Es el vínculo comprobante ↔ hechos (en un
/// resumido, varios recibos nombran el mismo comprobante). Es un hecho: no se edita. Sin comprobante en los de valor cero o cuyas puntas resolvieron lo mismo. (nuevo)
/// </summary>
public abstract class RecepcionContableDeModulo : AuditableEntityLong, IHechoInmutable
{
    public const string SinComprobanteValorCero = "ZeroValue";
    public const string SinComprobanteMismasCuentas = "SameAccounts";
    public const string SinComprobanteInformativo = "Informational";

    public Guid MessagePublicId { get; init; }

    public string MessageType { get; init; } = string.Empty;

    public short MessageVersion { get; init; } = 1;

    public IntegrationMessageKind MessageKind { get; init; }

    /// <summary><c>REC</c> o <c>TES</c>.</summary>
    public string SourceModule { get; init; } = string.Empty;

    /// <summary>El documento de Recaudo o Tesorería que originó el hecho.</summary>
    public Guid SourcePublicId { get; init; }

    public string? SourceDocumentClass { get; init; }

    public string? SourceDocumentTypeCode { get; init; }

    public string? SourceDocumentNumber { get; init; }

    /// <summary>El hecho original de un correctivo (anulación, reverso, devolución).</summary>
    public Guid? RelatedMessagePublicId { get; init; }

    public DateOnly OperationDate { get; init; }

    public long? AccountingDocumentId { get; init; }

    public AccountingDocument? AccountingDocument { get; init; }

    public string? NoVoucherReason { get; init; }

    public Guid? BatchPublicId { get; init; }

    public ActorKind OriginActorKind { get; init; } = ActorKind.Person;

    public Guid? OriginUserCentralId { get; init; }

    public string OriginUserName { get; init; } = string.Empty;

    public ActorKind ActorKind { get; init; }

    public int? ActorUserId { get; init; }

    public string ActorName { get; init; } = string.Empty;

    public DateTime ProcessedAt { get; init; }
}

/// <summary><c>ACC_ReceiptsPostings</c>. (nuevo)</summary>
public class ReceiptsPosting : RecepcionContableDeModulo;

/// <summary><c>ACC_TreasuryPostings</c>. (nuevo)</summary>
public class TreasuryPosting : RecepcionContableDeModulo;
