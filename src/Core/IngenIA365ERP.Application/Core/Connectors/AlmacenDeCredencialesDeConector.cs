using System.Security.Cryptography;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core.Connectors;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Connectors;

/// <summary>
/// Las credenciales de un conector en la base de la cooperativa (feature 020, R16; FR-042, FR-057; T144): un JSON con los campos que declara
/// la definición, cifrado con DataProtection —el llavero de <c>ADM_DataProtectionKeys</c>— con el propósito
/// <see cref="Proposito"/> y la cooperativa como subpropósito, así una fila copiada a otra base no se descifra allí. Es el <b>único</b> que
/// llama a <c>Unprotect</c> con ese propósito (<c>SoloElAlmacenDescifraCredenciales</c>) y nunca guarda: el que lo llama hace el
/// <c>SaveChangesAsync</c>. Un campo que llega vacío conserva el valor anterior: el formulario nunca rellena los secretos.
/// </summary>
public sealed class AlmacenDeCredencialesDeConector(IApplicationDbContext db, IDataProtectionProvider proveedor, ICurrentTenantService cooperativa)
{
    public const string Proposito = "IngenIA365ERP.Connectors.Credential.v1";

    private IDataProtector Protector() => proveedor.CreateProtector(Proposito, cooperativa.TenantId ?? "sin-cooperativa");

    /// <summary>Si el conector ya tiene credenciales.</summary>
    public Task<bool> TieneAsync(int conectorId, CancellationToken ct) => db.ConnectorCredentials.AsNoTracking().AnyAsync(c => c.ConnectorId == conectorId, ct);

    /// <summary>
    /// Fusiona los campos dados con los que había (un campo vacío o ausente conserva el anterior) y devuelve los nombres que siguen sin
    /// valor; si no falta ninguno, deja la fila cifrada lista para guardar y la verificación anterior en blanco.
    /// </summary>
    public async Task<IReadOnlyList<string>> PrepararAsync(int conectorId, IReadOnlyCollection<string> campos, IReadOnlyDictionary<string, string> dados,
        string quien, DateTime ahora, CancellationToken ct)
    {
        var fila = await db.ConnectorCredentials.FirstOrDefaultAsync(c => c.ConnectorId == conectorId, ct);
        var actuales = fila is null ? new Dictionary<string, string>(StringComparer.Ordinal) : Descifrar(fila.ProtectedPayload);
        var nuevos = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var campo in campos)
        {
            if (dados.TryGetValue(campo, out var valor) && !string.IsNullOrEmpty(valor)) nuevos[campo] = valor;
            else if (actuales.TryGetValue(campo, out var anterior) && !string.IsNullOrEmpty(anterior)) nuevos[campo] = anterior;
        }
        var faltan = campos.Where(c => !nuevos.ContainsKey(c)).ToList();
        if (faltan.Count > 0) return faltan;

        if (fila is null)
        {
            fila = new ConnectorCredential { ConnectorId = conectorId, CreatedBy = quien };
            db.ConnectorCredentials.Add(fila);
        }
        fila.ProtectedPayload = Protector().Protect(JsonSerializer.Serialize(nuevos));
        fila.SetAt = ahora;
        fila.SetBy = quien;
        fila.LastVerifiedAt = null;
        fila.LastVerificationOk = null;
        fila.LastVerificationMessage = null;
        return [];
    }

    /// <summary>Los campos de credencial del conector, descifrados; nulo si no tiene o si no se pueden descifrar (llavero perdido).</summary>
    public async Task<IReadOnlyDictionary<string, string>?> LeerAsync(int conectorId, CancellationToken ct)
    {
        var protegido = await db.ConnectorCredentials.AsNoTracking().Where(c => c.ConnectorId == conectorId).Select(c => c.ProtectedPayload).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(protegido)) return null;
        var campos = Descifrar(protegido);
        return campos.Count == 0 ? null : campos;
    }

    private Dictionary<string, string> Descifrar(string protegido)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(Protector().Unprotect(protegido)) ?? new(StringComparer.Ordinal);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
