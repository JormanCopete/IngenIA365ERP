using IngenIA365ERP.Shared.Models.Personas;

namespace IngenIA365ERP.Shared.Services.Core;

// DTOs del maestro de personas tal como los sirve la API (feature 008). `Shared` no referencia
// `Application`, así que se duplican aquí con los mismos nombres de propiedad; el JSON es el
// contrato (specs/008-alta-persona-un-paso/contracts/api.md).

/// <summary>Lo que devuelve <c>GET /api/core/people/{id}</c>: la persona completa, banderas incluidas.</summary>
public sealed record PersonaDto
{
    public Guid PublicId { get; init; }
    public string LastName { get; init; } = "";
    public string FirstName { get; init; } = "";
    /// <summary>Feature 010 (D-06): segundo apellido y otros nombres separados, para la DIAN y la PILA.</summary>
    public string? SecondLastName { get; init; }
    public string? OtherNames { get; init; }
    public string TaxId { get; init; } = "";
    public string? TaxIdCheckDigit { get; init; }
    public string? IdIssuedAt { get; init; }
    public DateOnly? IdIssueDate { get; init; }
    public string IdType { get; init; } = "C";
    public string? PersonType { get; init; }
    public string? BusinessName { get; init; }
    public string? Email { get; init; }
    public string? Phone1 { get; init; }
    public string? Phone2 { get; init; }
    public string? Mobile { get; init; }
    public string? Address { get; init; }
    public Guid? CityPublicId { get; init; }
    public string? CityName { get; init; }
    public string? Gender { get; init; }
    public string? MaritalStatus { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? EducationLevel { get; init; }
    public string? Status { get; init; }
    public bool IsAssociate { get; init; }
    public bool IsEmployee { get; init; }
    public bool IsSalesperson { get; init; }
    public bool IsThirdParty { get; init; }
    public bool IsAdvisor { get; init; }
    public bool IsCustomer { get; init; }
    public bool IsSupplier { get; init; }
    public bool ReceivesInvoice { get; init; }

    // Perfil tributario (feature 012, T174).
    public bool IsVatResponsible { get; init; }
    public bool IsSelfWithholder { get; init; }
    public bool IsVatWithholdingAgent { get; init; }
    public bool IsSimpleTaxRegime { get; init; }
    public bool IsIncomeTaxFiler { get; init; }
    public bool IsObligatedToInvoice { get; init; }
    public bool IsLargeContributor { get; init; }
    public bool WithholdingExempt { get; init; }
    public bool IcaWithholdingExempt { get; init; }
    public string? CiiuCode { get; init; }

    public string NombreVisible => NombreDePersona.Visible(BusinessName, FirstName, OtherNames, LastName, SecondLastName);
}

/// <summary>
/// Lo que un cliente puede escribir de una persona (<c>PersonInput</c> del servidor): cuerpo de
/// <c>POST /api/core/people</c>, de <c>PUT /api/core/people/{id}</c> y de la parte «person» de las
/// altas en un paso. Sin <c>IsEmployee</c>/<c>IsAssociate</c>/<c>IsSalesperson</c>: esas las
/// escribe el módulo que crea el rol.
/// </summary>
public sealed record PersonaEntradaDto
{
    public string IdType { get; init; } = "C";
    public string TaxId { get; init; } = "";
    public string? TaxIdCheckDigit { get; init; }
    public string? IdIssuedAt { get; init; }
    public DateOnly? IdIssueDate { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string? SecondLastName { get; init; }
    public string? OtherNames { get; init; }
    public string? BusinessName { get; init; }
    public string? PersonType { get; init; }
    public string? Address { get; init; }
    public string? Phone1 { get; init; }
    public string? Phone2 { get; init; }
    public string? Mobile { get; init; }
    public string? Email { get; init; }
    public Guid? CityPublicId { get; init; }
    public string? Gender { get; init; }
    public string? MaritalStatus { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? EducationLevel { get; init; }
    public bool IsThirdParty { get; init; }
    public bool IsAdvisor { get; init; }
    public bool IsCustomer { get; init; }
    public bool IsSupplier { get; init; }
    public bool ReceivesInvoice { get; init; }
    public string? Status { get; init; }

    // Perfil tributario (feature 012, T174). El formulario siempre los manda; el servidor admite nulo = no cambia.
    public bool? IsVatResponsible { get; init; }
    public bool? IsSelfWithholder { get; init; }
    public bool? IsVatWithholdingAgent { get; init; }
    public bool? IsSimpleTaxRegime { get; init; }
    public bool? IsIncomeTaxFiler { get; init; }
    public bool? IsObligatedToInvoice { get; init; }
    public bool? IsLargeContributor { get; init; }
    public bool? WithholdingExempt { get; init; }
    public bool? IcaWithholdingExempt { get; init; }
    /// <summary>Vacío quita el CIIU; nulo lo deja como está.</summary>
    public string? CiiuCode { get; init; }

    /// <summary>
    /// Feature 012 (T175): la autorización de datos capturada al crear (<c>authorization</c>). Sólo en el alta; nula no
    /// viaja.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public AutorizacionAlCrearDto? Authorization { get; init; }
}

/// <summary>
/// La decisión del titular sobre la política de tratamiento de datos al crearlo (<c>AutorizacionAlCrear</c> del
/// servidor). <see cref="Decision"/> es <c>Accepted</c> o <c>Declined</c>; <see cref="PolicyVersionPublicId"/> nulo sólo si
/// la cooperativa no tiene política publicada.
/// </summary>
public sealed record AutorizacionAlCrearDto(string Decision, Guid? PolicyVersionPublicId, string? Channel);

/// <summary><c>GET /api/compliance/habeas-data/policies/current</c>: la política que se le muestra al titular.</summary>
public sealed record PoliticaDeDatosVigenteDto(Guid PolicyVersionPublicId, int Version, string Title, string Text, DateTime PublishedAt);

/// <summary>Una fila de <c>GET /api/core/people/search?q=&amp;rol=</c>.</summary>
public sealed record PersonaBusquedaDto(
    Guid PublicId,
    string IdentificationNumber,
    string FullName,
    bool IsAssociate,
    bool IsEmployee,
    bool IsSalesperson,
    bool IsCustomer,
    bool IsSupplier,
    bool IsAdvisor,
    bool IsThirdParty,
    bool ReceivesInvoice,
    string? CityName,
    string? Status);

/// <summary>
/// <c>GET /api/core/people/by-document?taxId=</c>: la persona dueña de un documento,
/// <b>eliminadas incluidas</b>. Es lo que el diálogo consulta para ofrecer «Usar esa persona»
/// o «Restaurar persona» tras un 422.
/// </summary>
public sealed record PersonaPorDocumentoDto(
    Guid PublicId,
    string FullName,
    string TaxId,
    string IdType,
    bool IsDeleted,
    DateTime? DeletedAt,
    bool IsEmployee,
    bool IsAssociate,
    bool IsSalesperson,
    string? Status);

/// <summary>
/// Cuerpo de <c>POST /api/payroll/employees/with-person</c>. La parte laboral viaja como el mismo
/// objeto que la pantalla ya arma para <c>POST /api/payroll/employees</c> (sin
/// <c>personPublicId</c>); System.Text.Json serializa el tipo real.
/// </summary>
public sealed record AltaEmpleadoConPersonaRequest(PersonaEntradaDto Person, object Employee);

public sealed record AltaEmpleadoConPersonaResultado(Guid PersonPublicId, Guid EmployeePublicId);

/// <summary>Cuerpo de <c>POST /api/core/associates/with-person</c>; misma idea que el de empleados.</summary>
public sealed record AltaAsociadoConPersonaRequest(PersonaEntradaDto Person, object Associate);

public sealed record AltaAsociadoConPersonaResultado(Guid PersonPublicId, Guid AssociatePublicId);

/// <summary>Una ciudad para el desplegable del formulario (<c>PublicId</c> anulable porque el control lo exige así).</summary>
public sealed record OpcionDeCiudad(Guid? PublicId, string Name);

/// <summary>Códigos de error del maestro de personas que la pantalla trata de forma especial.</summary>
public static class ErroresDePersona
{
    /// <summary>Documento de una persona viva: la pantalla ofrece «Usar esa persona».</summary>
    public const string DocumentoDuplicado = "Person.TaxIdDuplicate";

    /// <summary>Documento de una persona eliminada: la pantalla ofrece «Restaurar persona» a quien puede.</summary>
    public const string DocumentoEliminado = "Person.TaxIdDeleted";
}
