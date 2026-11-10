namespace IngenIA365ERP.Domain.Enums.Connectors;

// Feature 020 (R15, R18; data-model §3): los enums de los conectores externos. Se guardan como smallint o texto; un valor nunca se renumera.

/// <summary>Cómo se autentica un conector ante el sistema externo.</summary>
public enum ConnectorAuthKind : short
{
    None = 0,

    /// <summary>Un inicio de sesión que devuelve un token, que se manda en una cabecera.</summary>
    LoginToken = 1,

    /// <summary>Una llave fija en una cabecera.</summary>
    HeaderKey = 2,

    /// <summary>Usuario y clave básicos (RFC 7617).</summary>
    Basic = 3,
}

/// <summary>Qué significa una respuesta del sistema externo (FR-037).</summary>
public enum ConnectorOutcome : short
{
    Done = 1,
    DoneWithWarning = 2,
    Retry = 3,
    ReAuth = 4,
    ManualReview = 5,
}

/// <summary>Estado de un envío en el tablero (FR-049).</summary>
public enum ConnectorRequestStatus : short
{
    Pending = 1,
    Sent = 2,
    SentWithWarning = 3,
    Retrying = 4,
    Review = 5,
    OnHold = 6,
    PaymentVoided = 7,
    ResolvedManually = 8,
}

/// <summary>Qué llamada hizo un intento.</summary>
public enum ConnectorCallKind : short
{
    Login = 1,
    Operation = 2,
}
