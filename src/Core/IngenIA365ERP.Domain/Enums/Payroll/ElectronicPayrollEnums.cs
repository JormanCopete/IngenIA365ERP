namespace IngenIA365ERP.Domain.Enums.Payroll;

// Feature 010, US6 (data-model.md §2.9). Las entidades nacen en la entrega N2 para que la
// migración par de N2+N3 sea una sola (D-12); la lógica llega con N3.

/// <summary>Cómo transmite la cooperativa: con su propio software habilitado ante la DIAN o por un proveedor tecnológico.</summary>
public enum ElectronicPayrollMode
{
    OwnSoftware = 1,
    TechnologyProvider = 2,
}

/// <summary>Ambiente DIAN; los mismos códigos que el atributo <c>Ambiente</c> del XML.</summary>
public enum DianEnvironment
{
    Production = 1,
    Testing = 2,
}

/// <summary>Estado del set de pruebas de habilitación.</summary>
public enum TestSetStatus
{
    NotStarted = 0,
    InProgress = 1,
    Accepted = 2,
    Failed = 3,
}

/// <summary>Estado de un documento soporte de nómina electrónica o de una nota de ajuste.</summary>
public enum ElectronicPayrollDocumentStatus
{
    Generated = 0,
    Signed = 1,
    InProcess = 2,
    Accepted = 3,
    Rejected = 4,
    Superseded = 5,
}

/// <summary>Tipo de nota de ajuste (documento 103).</summary>
public enum AdjustmentNoteType
{
    Replace = 1,
    Eliminate = 2,
}

/// <summary>Operación del servicio central en un intento de transmisión.</summary>
public enum TransmissionOperation
{
    SendNominaSync = 1,
    SendTestSetAsync = 2,
    GetStatus = 3,
    GetStatusZip = 4,
    SignOnly = 5,
}

/// <summary>Cómo terminó un intento.</summary>
public enum TransmissionOutcome
{
    Accepted = 1,
    Rejected = 2,
    InProcess = 3,
    TransportError = 4,
    ServiceError = 5,
    Signed = 6,
}
