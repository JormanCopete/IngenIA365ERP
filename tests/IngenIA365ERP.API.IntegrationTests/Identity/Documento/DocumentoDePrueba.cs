using System.Security.Cryptography;
using System.Text;

namespace IngenIA365ERP.API.IntegrationTests.Identity.Documento;

/// <summary>
/// Feature 017 (decisión del dueño del 2026-10-28): ninguna invitación nueva sale sin documento. Las e2e que
/// invitan a alguien de paso le dan una cédula derivada de su correo: el mismo correo da siempre el mismo
/// documento, así que invitar a la misma persona en otra cooperativa cae en la misma identidad, y dos correos
/// distintos no comparten documento (10 dígitos sobre una huella SHA-256).
/// </summary>
public static class DocumentoDePrueba
{
    /// <summary>La cédula (CC) de prueba de <paramref name="correo"/>.</summary>
    public static string Numero(string correo)
    {
        var huella = SHA256.HashData(Encoding.UTF8.GetBytes(correo.Trim().ToLowerInvariant()));
        var valor = BitConverter.ToUInt64(huella, 0) % 900_000_000UL;
        return (1_000_000_000UL + valor).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>El cuerpo de <c>POST /api/tenants/{id}/invitations</c> con documento, nombres y correo.</summary>
    public static Dictionary<string, object?> Invitacion(string correo, Dictionary<string, object?>? extra = null)
    {
        var numero = Numero(correo);
        var cuerpo = new Dictionary<string, object?>
        {
            ["email"] = correo,
            ["documentType"] = "CC",
            ["documentNumber"] = numero,
            ["documentNumberConfirmation"] = numero,
            ["firstName"] = "Persona",
            ["lastName"] = "De Prueba",
        };
        if (extra is not null)
            foreach (var (clave, valor) in extra) cuerpo[clave] = valor;
        return cuerpo;
    }
}
