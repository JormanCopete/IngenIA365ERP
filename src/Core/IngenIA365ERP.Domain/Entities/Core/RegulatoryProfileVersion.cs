using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// <c>COR_RegulatoryProfileVersions</c> (feature 013, FR-017, FR-018, FR-108; T50, R15; data-model parte 3 §4.5): el perfil regulatorio
/// de la cooperativa con vigencia y soporte —tipo de organización del documento <c>TiposDeOrganizacion</c>, actividad financiera,
/// nivel de supervisión, categoría de fondo de empleados, grupo NIIF y la inscripción como operador de libranza—. Es plataforma: lo
/// consumen Cartera (selector de modelo de deterioro, mayorías de vinculados, operador de libranza, activación) y, más adelante, otros
/// módulos.
///
/// <para><b>Hecho</b> (H): se inserta y no se modifica; corregir es una fila nueva con <see cref="SupersedesPublicId"/>. La lectura es
/// bitemporal con la regla única (<see cref="VigenciaBitemporal"/>): R es <see cref="AuditableEntity.CreatedAt"/> y el <c>Id</c> desempata
/// dos versiones con el mismo <see cref="ValidFrom"/>. La escribe sólo <c>AddRegulatoryProfileVersionCommand</c>; la lee
/// <c>IPerfilRegulatorio</c>.</para>
/// </summary>
public class RegulatoryProfileVersion : AuditableEntity, IHechoInmutable, IVersionBitemporal
{
    /// <summary>Código del documento <c>TiposDeOrganizacion</c> vigente en <see cref="ValidFrom"/> (no es enum: la taxonomía cambia).</summary>
    public string OrganizationTypeCode { get; init; } = string.Empty;

    /// <summary>Sección o servicio de ahorro y crédito autorizado.</summary>
    public bool FinancialActivityAuthorized { get; init; }

    /// <summary>Nivel de supervisión: 1, 2 o 3.</summary>
    public byte SupervisionLevel { get; init; }

    /// <summary>Categoría de los fondos de empleados, del documento <c>TiposDeOrganizacion</c>; nula si no es fondo de empleados.</summary>
    public string? EmployeeFundCategory { get; init; }

    /// <summary>Grupo NIIF: 1, 2 o 3. La pantalla propone el de <c>AccountingSetup.NiifGroup</c>; si difieren, alerta.</summary>
    public byte? NiifGroup { get; init; }

    /// <summary>Inscrita como operador de libranza (FR-108).</summary>
    public bool LibranzaOperatorRegistered { get; init; }

    /// <summary>Número de la inscripción; obligatorio si <see cref="LibranzaOperatorRegistered"/>.</summary>
    public string? LibranzaOperatorRegistrationNumber { get; init; }

    public DateOnly? LibranzaOperatorRegistrationDate { get; init; }

    // ------------------------------------------------------------------------- feature 014, K7 (data-model §1.8; FR-018) --

    /// <summary>Feature 014 (FR-018): la cooperativa puede captar ahorro (lo exige la activación de captaciones); defecto falso.</summary>
    public bool MayTakeDeposits { get; init; }

    /// <summary>Feature 014: sólo capta de sus asociados (FR-030).</summary>
    public bool DepositsFromAssociatesOnly { get; init; }

    /// <summary>Feature 014 (FR-025, R25): categoría del fondo de liquidez según <c>FondoDeLiquidez.PorcentajesPorCategoria</c>; nula = no aplica.</summary>
    public string? LiquidityFundCategory { get; init; }

    /// <summary>Feature 014: inscrita en el seguro de depósitos (Fogacoop).</summary>
    public bool DepositInsuranceEnrolled { get; init; }

    /// <summary>Feature 014 (R07): agente retenedor del GMF.</summary>
    public bool IsGmfWithholdingAgent { get; init; }

    public DateOnly ValidFrom { get; init; }

    /// <summary>Resolución, certificado de existencia o acta.</summary>
    public string Source { get; init; } = string.Empty;

    public Guid? SupportAttachmentPublicId { get; init; }

    public string Reason { get; init; } = string.Empty;

    /// <summary>La versión que esta corrige; nula si no es corrección.</summary>
    public Guid? SupersedesPublicId { get; init; }

    public int RegisteredByUserId { get; init; }

    public string RegisteredByName { get; init; } = string.Empty;

    DateOnly? IVersionBitemporal.ValidTo => null;
    long IVersionBitemporal.Sequence => Id;
    DateTime IVersionBitemporal.ConocidaEn => CreatedAt;
}
