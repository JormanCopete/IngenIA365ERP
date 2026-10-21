using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;

/// <summary>
/// <c>LND_MemberAccountControls</c> (feature 014, K7; data-model §4.4; FR-031, FR-061, R08): una fila por cuenta, <b>sin saldos</b>: cerrojo
/// (nivel «cuenta» del orden fijo de <c>CerrojoDeCartera</c>) y marcas de agua. Nace en el mismo guardado que la cuenta. Causar el día d exige
/// <see cref="LastAccruedDate"/> = d − 1; liquidar un período exige la marca en su fin o después. Las marcas las mueve sólo
/// <c>RegistroDeCuentas</c>. No es hecho; sin diferencias de auditoría (§0.8).
/// </summary>
[SinDiffDeAuditoria]
public class MemberAccountControl : AuditableEntity
{
    /// <summary>Único sin filtro.</summary>
    public int MemberAccountId { get; set; }

    /// <summary>Navegación para nacer en el mismo guardado que la cuenta.</summary>
    public Accounts.MemberAccount? MemberAccount { get; set; }

    /// <summary>Última fecha causada; al nacer, la fecha valor de la apertura o el corte.</summary>
    public DateOnly LastAccruedDate { get; set; }

    /// <summary>Fin del último período liquidado (FR-061).</summary>
    public DateOnly? LastSettledThrough { get; set; }

    /// <summary>Última secuencia asignada a una operación de la cuenta.</summary>
    public int Sequence { get; set; }

    /// <summary>Para el <c>RegisteredAt</c> monótono por cuenta.</summary>
    public DateTime LastRegisteredAt { get; set; }

    /// <summary>Última posición diaria escrita.</summary>
    public DateOnly? LastPositionDate { get; set; }

    /// <summary>Último movimiento del titular (FR-058, FR-084): lo que mide la inactividad.</summary>
    public DateOnly? LastHolderActivityDate { get; set; }

    /// <summary><c>AAAA-MM-s</c> de la última cuota generada.</summary>
    public string? LastInstallmentPeriod { get; set; }
}
