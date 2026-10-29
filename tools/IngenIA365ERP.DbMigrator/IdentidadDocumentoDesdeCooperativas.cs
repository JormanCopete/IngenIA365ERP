using System.Globalization;
using System.Text;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Entities.Admin;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace IngenIA365ERP.DbMigrator;

/// <summary>
/// Feature 017 (T021, research R14, FR-047): tarea de una sola vez que sube a la cuenta central el
/// documento de las cuentas que ya existían, tomado de la persona enlazada en cada cooperativa.
/// <list type="bullet">
///   <item>Recorre el directorio y abre la base de cada cooperativa por la fábrica autorizada
///         (Principio IV): nunca un join entre bases.</item>
///   <item>Si todas las cooperativas de una cuenta la enlazan a una persona con el mismo tipo de acceso
///         y número, se lo registra (con nombres si faltan) y deja historial con motivo
///         <c>Migration</c>. Si no coinciden, el documento ya es de otra cuenta, o el tipo no sirve
///         para entrar (NIT, RC), va al reporte de conflictos y no se toca.</item>
///   <item>Idempotente: una cuenta que ya tiene documento no se vuelve a mirar; correrla dos veces da
///         lo mismo y el reporte sale igual.</item>
///   <item>No pasa por <c>UserManager</c>: no rota sellos ni corta sesiones.</item>
/// </list>
/// </summary>
internal static class IdentidadDocumentoDesdeCooperativas
{
    private sealed record Vinculo(string Cooperativa, string IdType, string TaxId, string? Nombre, string? OtrosNombres,
        string? Apellido, string? SegundoApellido);

    public static async Task<int> EjecutarAsync(IServiceProvider sp, string? salida)
    {
        var adminDb = sp.GetRequiredService<AdminDbContext>();
        var fabrica = sp.GetRequiredService<ITenantDbContextFactory>();

        var activas = await adminDb.Tenants.AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new { t.Name, t.DatabaseName, t.SchemaName, t.ConnectionString })
            .ToListAsync();

        var porCuenta = new Dictionary<Guid, List<Vinculo>>();
        var fallidas = new List<string>();
        foreach (var c in activas)
        {
            var nombreDeBase = c.DatabaseName ?? c.SchemaName;
            if (string.IsNullOrWhiteSpace(nombreDeBase) || nombreDeBase.Equals("dbo", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                await using var ambito = fabrica.Abrir(nombreDeBase, c.ConnectionString);
                var filas = await ambito.Db.Users.AsNoTracking()
                    .Where(u => !u.IsDeleted && u.CentralUserId != null && u.PersonId != null)
                    .Join(ambito.Db.People.AsNoTracking().Where(p => !p.IsDeleted), u => u.PersonId, p => p.Id,
                        (u, p) => new { u.CentralUserId, p.IdType, p.TaxId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName })
                    .ToListAsync();
                foreach (var f in filas)
                {
                    if (!porCuenta.TryGetValue(f.CentralUserId!.Value, out var lista))
                        porCuenta[f.CentralUserId.Value] = lista = [];
                    lista.Add(new Vinculo(c.Name, f.IdType, f.TaxId, f.FirstName, f.OtherNames, f.LastName, f.SecondLastName));
                }
            }
            catch (Exception ex)
            {
                fallidas.Add(c.Name);
                Log.Error(ex, "  {Cooperativa}: no se pudo leer su base {Base}. Se sigue con las demás.", c.Name, nombreDeBase);
            }
        }

        var cuentas = await adminDb.Users.Where(u => !u.IsDeleted && u.DocumentUsername == null).ToListAsync();
        var conflictos = new StringBuilder("cuenta,correo,motivo,cooperativas\n");
        int registradas = 0, enConflicto = 0;
        var ahora = DateTime.UtcNow;

        foreach (var cuenta in cuentas)
        {
            if (!porCuenta.TryGetValue(cuenta.Id, out var vinculos) || vinculos.Count == 0)
                continue; // sin persona enlazada: completará su documento al entrar (FR-029)

            var documentos = vinculos
                .Select(v => (Tipo: TiposDeDocumento.PorCodigoDePersona(v.IdType), Numero: UsuarioDeDocumento.NormalizarNumero(v.TaxId), v))
                .ToList();
            var distintos = documentos.Select(d => (d.Tipo?.Codigo, d.Numero)).Distinct().ToList();
            var cooperativas = string.Join(" | ", vinculos.Select(v => v.Cooperativa).Distinct());

            string? motivo = null;
            DocumentoValidado? documento = null;
            if (distintos.Count > 1)
                motivo = "documentos distintos entre cooperativas";
            else if (documentos[0].Tipo is not { SirveParaEntrar: true } tipo)
                motivo = $"el tipo {vinculos[0].IdType} no sirve para entrar";
            else if ((documento = UsuarioDeDocumento.Validar(tipo.Codigo, documentos[0].Numero, soloDeAcceso: true, out var error)) is null)
                motivo = $"documento inválido: {error}";
            else if (await adminDb.Users.AnyAsync(u => u.Id != cuenta.Id && !u.IsDeleted && u.DocumentUsername == documento.Usuario))
                motivo = "el documento ya es de otra cuenta";

            if (motivo is not null)
            {
                enConflicto++;
                conflictos.Append(CultureInfo.InvariantCulture,
                    $"{cuenta.Id},{Csv(cuenta.Email)},{Csv(motivo)},{Csv(cooperativas)}\n");
                continue;
            }

            var origen = documentos[0].v;
            cuenta.DocumentType = documento!.Tipo.Codigo;
            cuenta.DocumentNumber = documento.Numero;
            cuenta.DocumentUsername = documento.Usuario;
            cuenta.FirstName ??= Recortar(origen.Nombre);
            cuenta.OtherNames ??= Recortar(origen.OtrosNombres);
            cuenta.LastName ??= Recortar(origen.Apellido);
            cuenta.SecondLastName ??= Recortar(origen.SegundoApellido);
            adminDb.DocumentHistory.Add(DocumentHistoryEntry.Registrar(
                cuenta.Id, null, null, null, documento.Tipo.Codigo, documento.Numero, documento.Usuario,
                DocumentChangeReason.Migration, DocumentChangeActor.Process, null, ahora,
                notes: $"Tomado de la persona enlazada en: {cooperativas}."));
            registradas++;
        }

        await adminDb.SaveChangesAsync();

        var archivo = salida ?? Path.Combine(Directory.GetCurrentDirectory(), "identidad-documento-conflictos.csv");
        await File.WriteAllTextAsync(archivo, conflictos.ToString(), Encoding.UTF8);

        Log.Information(
            "identidad-documento: {Registradas} cuenta(s) con documento registrado, {Conflictos} en conflicto (reporte: {Archivo}).",
            registradas, enConflicto, archivo);
        if (fallidas.Count > 0)
        {
            Log.Error("identidad-documento: {Cuenta} cooperativa(s) no se pudieron leer: {Lista}. Vuelva a correr la tarea.",
                fallidas.Count, string.Join(", ", fallidas));
            return 3;
        }
        return 0;
    }

    private static string? Recortar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length <= 60 ? t : t[..60];
    }

    private static string Csv(string? s) => "\"" + (s ?? string.Empty).Replace("\"", "\"\"") + "\"";
}
