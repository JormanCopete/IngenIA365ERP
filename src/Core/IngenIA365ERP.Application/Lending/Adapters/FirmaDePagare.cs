namespace IngenIA365ERP.Application.Lending.Adapters;

/// <summary>En qué va la firma de un firmante ante el depósito (feature 013, US3, T0436; §3.2). (nuevo)</summary>
public enum EstadoDeFirmaDePagare
{
    /// <summary>El depósito le pidió firmar y todavía no lo ha hecho.</summary>
    Pendiente = 1,

    /// <summary>Firmó.</summary>
    Firmada = 2,

    /// <summary>Rechazó la firma (o el depósito la rechazó): el pagaré no se perfecciona.</summary>
    Rechazada = 3,
}

/// <summary>
/// La firma de un firmante tal como la informa el depósito (feature 013, US3, T0436; §3.2): de quién es, en qué va y cuándo cambió. (nuevo)
/// </summary>
public sealed record FirmaDePagare(Guid PersonaPublicId, string NumeroDeDocumento, EstadoDeFirmaDePagare Estado, DateTimeOffset? En);
