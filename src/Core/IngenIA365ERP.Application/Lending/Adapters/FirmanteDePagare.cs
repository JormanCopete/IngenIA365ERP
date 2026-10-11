namespace IngenIA365ERP.Application.Lending.Adapters;

// Feature 013, US3 (bloque 7B; T0436; T71; contracts/adaptadores-y-archivos.md §3.2): las formas del puerto del depósito de valores. Nombres en
// español y sin el sufijo Dto (precedente de ResultadoDeCanal de la 012, §3.1).

/// <summary>
/// Quien firma el pagaré (§3.2): el deudor y cada codeudor, con la copia de su documento y su nombre tal como están al crearlo (I-1). El depósito
/// les escribe para que firmen; el ERP no. (nuevo)
/// </summary>
/// <param name="PersonaPublicId">La persona del maestro.</param>
/// <param name="Rol"><c>Debtor</c> o <c>CoDebtor</c>.</param>
/// <param name="TipoDeDocumento">El tipo de documento (<c>CC</c>, <c>NIT</c>…).</param>
/// <param name="NumeroDeDocumento">El número de documento; el canal <c>SIMULADO</c> decide por su último dígito (§3.7).</param>
/// <param name="NombreCompleto">El nombre completo.</param>
/// <param name="Correo">El correo, si la persona lo tiene: por ahí la notifica el depósito.</param>
public sealed record FirmanteDePagare(
    Guid PersonaPublicId,
    string Rol,
    string TipoDeDocumento,
    string NumeroDeDocumento,
    string NombreCompleto,
    string? Correo)
{
    public const string Deudor = "Debtor";
    public const string Codeudor = "CoDebtor";
}
