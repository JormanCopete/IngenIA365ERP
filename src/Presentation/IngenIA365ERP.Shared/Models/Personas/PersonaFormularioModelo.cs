using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Shared.Services.Core;

namespace IngenIA365ERP.Shared.Models.Personas;

/// <summary>
/// El formulario de persona, en un solo sitio (feature 008, FR-006). Es el modelo que editan
/// <c>PersonaCampos</c>/<c>PersonaDialog</c> en Personas, Empleados y Asociados; hasta el
/// 2026-09-13 cada pantalla tenía su copia privada con campos y catálogos distintos.
///
/// <para>
/// Las banderas <b>simples</b> (cliente, proveedor, asesor, tercero, factura) se editan aquí.
/// Las <b>derivadas</b> (empleado, asociado, vendedor) son de sólo lectura: las enciende el
/// módulo que crea la fila hija y aquí se muestran como distintivo. Por eso
/// <see cref="AInput"/> no las incluye — el contrato del servidor tampoco las tiene.
/// </para>
/// </summary>
public sealed class PersonaFormularioModelo
{
    public Guid? PublicId { get; set; }

    // ---- Identificación ----
    [Required(ErrorMessage = "Tipo de documento obligatorio.")]
    public string IdType { get; set; } = "C";

    [Required(ErrorMessage = "Número de documento obligatorio.")]
    [MaxLength(20)]
    public string TaxId { get; set; } = "";

    [MaxLength(2)]
    public string? TaxIdCheckDigit { get; set; }

    [MaxLength(40)]
    public string? IdIssuedAt { get; set; }

    /// <summary>Fecha de expedición; <c>DateTime?</c> porque es lo que edita <c>SfDatePicker</c>.</summary>
    public DateTime? IdIssueDateDt { get; set; }

    [Required(ErrorMessage = "Nombres obligatorios.")]
    [MaxLength(150)]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Apellidos obligatorios.")]
    [MaxLength(150)]
    public string LastName { get; set; } = "";

    /// <summary>Feature 010 (D-06): segundo apellido separado, para la DIAN y la PILA.</summary>
    [MaxLength(150)]
    public string? SecondLastName { get; set; }

    /// <summary>Feature 010 (D-06): los nombres que siguen al primero.</summary>
    [MaxLength(150)]
    public string? OtherNames { get; set; }

    [MaxLength(150)]
    public string? BusinessName { get; set; }

    public string? PersonType { get; set; } = "01";

    // ---- Contacto ----
    [MaxLength(120)]
    public string? Address { get; set; }

    [MaxLength(40)]
    public string? Phone1 { get; set; }

    [MaxLength(40)]
    public string? Phone2 { get; set; }

    [MaxLength(30)]
    public string? Mobile { get; set; }

    [EmailAddress(ErrorMessage = "Correo electrónico no válido.")]
    [MaxLength(120)]
    public string? Email { get; set; }

    public Guid? CityPublicId { get; set; }

    // ---- Demografía ----
    public string? Gender { get; set; }
    public string? MaritalStatus { get; set; }
    public DateTime? DateOfBirthDt { get; set; }
    public string? EducationLevel { get; set; }

    // ---- Roles que son sólo una marca (editables) ----
    public bool IsThirdParty { get; set; }
    public bool IsAdvisor { get; set; }
    public bool IsCustomer { get; set; }
    public bool IsSupplier { get; set; }
    public bool ReceivesInvoice { get; set; }

    // ---- Roles derivados (sólo lectura: reflejo de la fila hija) ----
    public bool EsEmpleado { get; private set; }
    public bool EsAsociado { get; private set; }
    public bool EsVendedor { get; private set; }

    public string? Status { get; set; } = "A";

    public string NombreVisible => !string.IsNullOrWhiteSpace(BusinessName) ? BusinessName : $"{FirstName} {LastName}".Trim();

    /// <summary>Un modelo nuevo, con los valores por defecto del alta y, si se sabe, el documento ya escrito.</summary>
    public static PersonaFormularioModelo Nuevo(string? documento = null) => new()
    {
        IdType = "C", Status = "A", PersonType = "01", TaxId = documento?.Trim() ?? "",
    };

    /// <summary>El modelo a partir de lo que devolvió la API.</summary>
    public static PersonaFormularioModelo DesdeDto(PersonaDto p) => new()
    {
        PublicId = p.PublicId,
        IdType = string.IsNullOrWhiteSpace(p.IdType) ? "C" : p.IdType,
        TaxId = p.TaxId,
        TaxIdCheckDigit = p.TaxIdCheckDigit,
        IdIssuedAt = p.IdIssuedAt,
        IdIssueDateDt = p.IdIssueDate?.ToDateTime(TimeOnly.MinValue),
        FirstName = p.FirstName,
        LastName = p.LastName,
        SecondLastName = p.SecondLastName,
        OtherNames = p.OtherNames,
        BusinessName = p.BusinessName,
        PersonType = p.PersonType,
        Address = p.Address,
        Phone1 = p.Phone1,
        Phone2 = p.Phone2,
        Mobile = p.Mobile,
        Email = p.Email,
        CityPublicId = p.CityPublicId,
        Gender = p.Gender,
        MaritalStatus = p.MaritalStatus,
        DateOfBirthDt = p.DateOfBirth?.ToDateTime(TimeOnly.MinValue),
        EducationLevel = p.EducationLevel,
        IsThirdParty = p.IsThirdParty,
        IsAdvisor = p.IsAdvisor,
        IsCustomer = p.IsCustomer,
        IsSupplier = p.IsSupplier,
        ReceivesInvoice = p.ReceivesInvoice,
        EsEmpleado = p.IsEmployee,
        EsAsociado = p.IsAssociate,
        EsVendedor = p.IsSalesperson,
        Status = string.IsNullOrWhiteSpace(p.Status) ? "A" : p.Status,
    };

    /// <summary>Lo que viaja al servidor (<c>PersonInput</c>): sin las banderas derivadas.</summary>
    public PersonaEntradaDto AInput() => new()
    {
        IdType = IdType,
        TaxId = TaxId.Trim(),
        TaxIdCheckDigit = Vacio(TaxIdCheckDigit),
        IdIssuedAt = Vacio(IdIssuedAt),
        IdIssueDate = IdIssueDateDt is { } exp ? DateOnly.FromDateTime(exp) : null,
        FirstName = FirstName.Trim(),
        LastName = LastName.Trim(),
        SecondLastName = Vacio(SecondLastName),
        OtherNames = Vacio(OtherNames),
        BusinessName = Vacio(BusinessName),
        PersonType = Vacio(PersonType),
        Address = Vacio(Address),
        Phone1 = Vacio(Phone1),
        Phone2 = Vacio(Phone2),
        Mobile = Vacio(Mobile),
        Email = Vacio(Email),
        CityPublicId = CityPublicId,
        Gender = Vacio(Gender),
        MaritalStatus = Vacio(MaritalStatus),
        DateOfBirth = DateOfBirthDt is { } nac ? DateOnly.FromDateTime(nac) : null,
        EducationLevel = Vacio(EducationLevel),
        IsThirdParty = IsThirdParty,
        IsAdvisor = IsAdvisor,
        IsCustomer = IsCustomer,
        IsSupplier = IsSupplier,
        ReceivesInvoice = ReceivesInvoice,
        Status = Vacio(Status),
    };

    /// <summary>
    /// Validación del lado del cliente, alineada con <c>PersonInputValidator</c> (Principio VIII):
    /// lo que aquí falla, el servidor también lo rechazaría. Devuelve los avisos; vacío si pasa.
    /// </summary>
    public IReadOnlyList<string> Validar()
    {
        var avisos = new List<string>();
        if (string.IsNullOrWhiteSpace(IdType)) avisos.Add("El tipo de documento es obligatorio.");
        if (string.IsNullOrWhiteSpace(TaxId)) avisos.Add("El número de documento es obligatorio.");
        else if (TaxId.Trim().Length > 20) avisos.Add("El número de documento admite hasta 20 caracteres.");
        if (string.IsNullOrWhiteSpace(FirstName)) avisos.Add("Los nombres son obligatorios.");
        if (string.IsNullOrWhiteSpace(LastName)) avisos.Add("Los apellidos son obligatorios.");
        if (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email.Trim()))
            avisos.Add("El correo electrónico no es válido.");
        if (DateOfBirthDt is { } nac && nac.Date >= DateTime.UtcNow.Date)
            avisos.Add("La fecha de nacimiento debe ser anterior a hoy.");
        return avisos;
    }

    /// <summary>
    /// Si hay algo que proponer: los apellidos o los nombres traen más de una palabra y el campo
    /// separado correspondiente está vacío. Es lo que enciende el botón «Proponer partición».
    /// </summary>
    public bool PuedeProponerParticion =>
        (string.IsNullOrWhiteSpace(SecondLastName) && Palabras(LastName).Length > 1)
        || (string.IsNullOrWhiteSpace(OtherNames) && Palabras(FirstName).Length > 1);

    /// <summary>
    /// Feature 010 (D-06): <b>propone</b> partir «Apellidos» y «Nombres» por el primer espacio —la
    /// primera palabra se queda y el resto pasa a «Segundo apellido» / «Otros nombres»— y la persona
    /// confirma o corrige antes de guardar. Es una propuesta y no una migración porque partir
    /// «De la Hoz Mejía» por el espacio se equivoca; por eso nada lo hace por dato ni al guardar.
    /// Sólo toca los campos separados que están vacíos. Devuelve si cambió algo.
    /// </summary>
    public bool ProponerParticion()
    {
        var cambio = false;
        if (string.IsNullOrWhiteSpace(SecondLastName))
        {
            var apellidos = Palabras(LastName);
            if (apellidos.Length > 1)
            {
                LastName = apellidos[0];
                SecondLastName = string.Join(' ', apellidos.Skip(1));
                cambio = true;
            }
        }
        if (string.IsNullOrWhiteSpace(OtherNames))
        {
            var nombres = Palabras(FirstName);
            if (nombres.Length > 1)
            {
                FirstName = nombres[0];
                OtherNames = string.Join(' ', nombres.Skip(1));
                cambio = true;
            }
        }
        return cambio;
    }

    private static string[] Palabras(string? s) =>
        (s ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
