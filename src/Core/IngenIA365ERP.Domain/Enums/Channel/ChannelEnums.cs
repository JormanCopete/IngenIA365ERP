namespace IngenIA365ERP.Domain.Enums.Channel;

// Feature 021 (data-model.md §A): enumeraciones del módulo «Canal digital». Se guardan como entero.

/// <summary>Qué personas quedan habilitadas para el canal (FR-010).</summary>
public enum EnablementPolicy
{
    /// <summary>Todo asociado activo queda habilitado y se inhabilita al retirarse.</summary>
    AllActiveAssociates = 1,

    /// <summary>Sólo las personas que habilite un funcionario, una a una o por carga masiva.</summary>
    Manual = 2,

    /// <summary>Las dos anteriores.</summary>
    Both = 3,
}

/// <summary>Estado de la habilitación de una persona para el canal.</summary>
public enum EnablementStatus { Enabled = 1, Disabled = 2 }

/// <summary>De dónde salió una habilitación.</summary>
public enum EnablementSource { Policy = 1, Manual = 2, Bulk = 3 }

/// <summary>Tipos de concepto que la cooperativa publica en el canal (FR-002).</summary>
public enum ConceptKind
{
    Loan = 1,
    Contribution = 2,
    ContractualSaving = 3,
    ProgrammedSaving = 4,

    /// <summary>Facturas pendientes de cuentas por cobrar comerciales (entrega C2b).</summary>
    CommercialReceivable = 5,
}

/// <summary>Textos legales que la cooperativa publica (FR-007).</summary>
public enum LegalTextKind { Terms = 1, DataPolicy = 2, Faq = 3, GatewayFaq = 4 }

/// <summary>Qué hacer con el valor que excede lo exigible al aplicarse (FR-013, aclaración 3).</summary>
public enum SurplusPolicy
{
    /// <summary>Anticipo a las próximas cuotas si la obligación sigue viva; si no, saldo a favor del asociado.</summary>
    AdvanceElseCreditBalance = 1,

    /// <summary>Siempre saldo a favor del asociado.</summary>
    AlwaysCreditBalance = 2,
}

/// <summary>Pasarelas de pago soportadas.</summary>
public enum PaymentGateway { Placetopay = 1 }

/// <summary>Estado de las credenciales de un destino; las credenciales mismas viven sólo en el canal (research R10).</summary>
public enum CredentialsStatus { NotSet = 0, Active = 1, Failed = 2 }

/// <summary>Resultado de aplicar un aviso de pago (FR-046).</summary>
public enum NoticeOutcome
{
    Applied = 1,

    /// <summary>Aplicado con excedente a saldo a favor o con una parte que no se pudo aplicar.</summary>
    InReview = 2,
}

/// <summary>Campo de contacto que el asociado cambió en el canal (FR-024).</summary>
public enum ContactField { Email = 1, Mobile = 2 }

/// <summary>Opción de prepago por defecto de un crédito publicado (FR-032a).</summary>
public enum ChannelPrepaymentChoice { ReduceTerm = 1, ReduceInstallment = 2 }
