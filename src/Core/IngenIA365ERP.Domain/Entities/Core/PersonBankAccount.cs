using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// <c>COR_PersonBankAccounts</c> (feature 015, E-06, T0028; R-21; FR-210; data-model §16.1): una cuenta bancaria de una persona a la que la
/// cooperativa le gira (devoluciones, desembolsos, pagos). Es una pieza de <b>plataforma</b>, no de Tesorería: la usan Tesorería para validar
/// el destino de una orden y Recaudo para las devoluciones, por <c>ICuentasBancariasDePersona</c>.
///
/// <para>
/// <b>Doble control</b>: una persona la pide con la certificación bancaria (titularidad) y <b>otra</b> la aprueba; desde
/// <see cref="PayableFrom"/> (aprobación + <c>CuentasDestino.DiasDeEspera</c>) se le puede girar. Un cambio es una <b>fila nueva</b> que
/// reemplaza a la anterior (<see cref="ReplacesId"/>), y la anterior queda <see cref="PersonBankAccountState.Retired"/>: la historia de a qué
/// cuenta se giró no se reescribe. Las cuentas que la migración copió de los campos sueltos de proveedor de la persona nacen
/// <see cref="PersonBankAccountOrigin.Imported"/>, activas y sin certificación (Q4). Lectura y escritura son sensibles y se auditan
/// (<c>Core.PersonBankAccount.Read</c>, <c>.Changed</c>). Sólo la escriben los comandos de <c>Application/Core/PersonBankAccounts</c>.
/// </para>
/// </summary>
public class PersonBankAccount : AuditableEntity
{
    /// <summary>El titular: la persona a la que se le gira.</summary>
    public int PersonId { get; set; }

    public Person? Person { get; set; }

    /// <summary>El banco (<c>COR_Banks</c>); su <c>TransferCode</c> es el código ACH.</summary>
    public int BankId { get; set; }

    public Bank? Bank { get; set; }

    /// <summary>El tipo de cuenta tal como lo pide el archivo del banco (ahorros, corriente…), en texto corto.</summary>
    public string AccountType { get; set; } = string.Empty;

    /// <summary>El número de la cuenta, sin espacios ni guiones.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>El nombre del titular según la certificación.</summary>
    public string HolderName { get; set; } = string.Empty;

    /// <summary>El documento del titular según la certificación.</summary>
    public string HolderTaxId { get; set; } = string.Empty;

    /// <summary>La certificación bancaria (<c>COR_Attachments</c>, tipo <c>PersonBankCertificate</c>); nula sólo en las importadas.</summary>
    public Guid? CertificateAttachmentPublicId { get; set; }

    public PersonBankAccountOrigin Origin { get; set; } = PersonBankAccountOrigin.Registered;

    /// <summary>Mutable: <c>PendingApproval → Active | Rejected</c> y <c>Active → Retired</c>.</summary>
    public PersonBankAccountState Status { get; set; } = PersonBankAccountState.PendingApproval;

    /// <summary>El <c>SEC_Users.Id</c> de quien la pidió (nulo en las importadas): la segregación compara éste, nunca el del token.</summary>
    public int? RequestedByUserId { get; set; }

    public string RequestedByName { get; set; } = string.Empty;

    /// <summary>El <c>SEC_Users.Id</c> de quien la aprobó o la rechazó: siempre otra persona que la que la pidió.</summary>
    public int? DecidedByUserId { get; set; }

    public string? DecidedByName { get; set; }

    /// <summary>Instante UTC de la aprobación o del rechazo.</summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>Desde qué fecha (de Colombia) se le puede girar: la de la aprobación más la espera vigente; sin espera, la misma.</summary>
    public DateOnly? PayableFrom { get; set; }

    /// <summary>El motivo del rechazo o del retiro.</summary>
    public string? Reason { get; set; }

    /// <summary>Instante UTC del retiro (por un cambio o a pedido).</summary>
    public DateTime? RetiredAt { get; set; }

    /// <summary>La cuenta que esta fila reemplaza: al aprobarse ésta, aquélla queda retirada.</summary>
    public int? ReplacesId { get; set; }

    public PersonBankAccount? Replaces { get; set; }

    /// <summary>¿Se le puede girar el día <paramref name="hoy"/>? Activa y ya pasó la espera.</summary>
    public bool PagableEl(DateOnly hoy) => Status == PersonBankAccountState.Active && PayableFrom is { } desde && desde <= hoy;
}
