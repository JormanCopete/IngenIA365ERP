namespace IngenIA365ERP.Domain.Enums.Audit;

/// <summary>
/// Por qué se ancló la cadena de auditoría en ese punto (feature 012, T38; data-model §23, §26). Se
/// guarda como <c>int</c> y un valor nunca se renumera.
/// </summary>
public enum AuditAnchorKind
{
    /// <summary>Al activar la cadena de la cooperativa: <c>Seq</c> 0 y el hash inicial.</summary>
    Genesis = 1,

    /// <summary>Cada 1.000 eventos.</summary>
    EveryN = 2,

    /// <summary>Una vez al día, sobre la cabeza de la cadena.</summary>
    Daily = 3,
}
