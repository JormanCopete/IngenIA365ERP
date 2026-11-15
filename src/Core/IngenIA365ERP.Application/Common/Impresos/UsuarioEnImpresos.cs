using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Impresos;

/// <summary>
/// Cómo sale un usuario en los documentos impresos (facturas, notas, recibos, egresos, comprobantes, actas…; pedido del dueño del
/// 2026-11-15). Los documentos guardan quién los hizo con el correo de la sesión, y ese correo salía tal cual en el papel. Ahora la
/// cooperativa elige en <see cref="Clave"/> (Parámetros del sistema, y en Administración › Usuarios) entre:
/// <list type="bullet">
/// <item><see cref="Correo"/>: lo de siempre;</item>
/// <item><see cref="Nombre"/> (por defecto): primer nombre y primer apellido de la persona del usuario;</item>
/// <item><see cref="NombreCompleto"/>: nombres y apellidos completos;</item>
/// <item><see cref="Iniciales"/>: las iniciales de los nombres y apellidos;</item>
/// <item><see cref="Cargo"/>: el cargo del usuario en la cooperativa (sin cargo, el nombre).</item>
/// </list>
/// Lo que no corresponde a un usuario de la cooperativa (un nombre ya escrito, «SYSTEM», un proceso) se deja como está; un usuario
/// sin persona vinculada sale con su correo. Es el único sitio que hace esta traducción: cada constructor de un documento impreso
/// le pide un <see cref="Formateador"/> con los usuarios que va a pintar. El sobrenombre libre por usuario queda para después de la
/// consolidación de migraciones (necesita una columna).
/// </summary>
public sealed class UsuarioEnImpresos(IApplicationDbContext db)
{
    public const string Clave = "Core.PrintedUserDisplay";

    public const string Correo = "Correo";
    public const string Nombre = "Nombre";
    public const string NombreCompleto = "NombreCompleto";
    public const string Iniciales = "Iniciales";
    public const string Cargo = "Cargo";

    public static readonly IReadOnlyList<string> Modos = [Correo, Nombre, NombreCompleto, Iniciales, Cargo];

    /// <summary>Traduce el texto guardado de un usuario (su correo, casi siempre) a como lo pide la cooperativa.</summary>
    public sealed class Formateador(string modo, IReadOnlyDictionary<string, Datos> porClave)
    {
        public string Modo { get; } = modo;

        public string? this[string? guardado] => Texto(guardado);

        public string? Texto(string? guardado)
        {
            if (string.IsNullOrWhiteSpace(guardado) || Modo == Correo) return guardado;
            return porClave.TryGetValue(guardado.Trim(), out var d) ? Formatear(Modo, d) ?? guardado : guardado;
        }

        /// <summary>El texto de quien no tiene nada guardado: lo mismo que <see cref="Texto"/>, pero nunca nulo.</summary>
        public string TextoONada(string? guardado) => Texto(guardado) ?? string.Empty;
    }

    /// <summary>Lo que hace falta de un usuario para pintarlo de cualquier forma.</summary>
    public sealed record Datos(string? PrimerNombre, string? OtrosNombres, string? PrimerApellido, string? SegundoApellido, string? Cargo);

    /// <summary>Un formateador para los usuarios guardados en <paramref name="usuarios"/> (se ignoran vacíos y repetidos).</summary>
    public async Task<Formateador> FormateadorAsync(IEnumerable<string?> usuarios, CancellationToken ct)
    {
        var modo = await ModoAsync(ct);
        if (modo == Correo) return new Formateador(modo, new Dictionary<string, Datos>());

        var claves = usuarios.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var porClave = new Dictionary<string, Datos>(StringComparer.OrdinalIgnoreCase);
        if (claves.Count == 0) return new Formateador(modo, porClave);

        var minusculas = claves.Select(c => c.ToLower()).ToList();
        var filas = await db.Users.AsNoTracking()
            .Where(u => (u.Email != null && minusculas.Contains(u.Email.ToLower()))
                        || (u.CentralUserPublicEmail != null && minusculas.Contains(u.CentralUserPublicEmail.ToLower()))
                        || minusculas.Contains(u.Username.ToLower()))
            .Select(u => new
            {
                u.Email, u.CentralUserPublicEmail, u.Username,
                Nombre = u.Person != null ? u.Person.FirstName : null,
                Otros = u.Person != null ? u.Person.OtherNames : null,
                Apellido = u.Person != null ? u.Person.LastName : null,
                Segundo = u.Person != null ? u.Person.SecondLastName : null,
                Cargo = u.Position != null ? u.Position.Name : null,
            })
            .ToListAsync(ct);

        foreach (var f in filas)
        {
            var datos = new Datos(f.Nombre, f.Otros, f.Apellido, f.Segundo, f.Cargo);
            foreach (var k in new[] { f.Email, f.CentralUserPublicEmail, f.Username })
                if (!string.IsNullOrWhiteSpace(k)) porClave.TryAdd(k.Trim(), datos);
        }
        return new Formateador(modo, porClave);
    }

    /// <summary>
    /// El texto de un usuario de la cooperativa por su <c>SEC_Users.Id</c> (cajeros, quien arqueó, quien aprobó…); su nombre de
    /// usuario si no hay datos para más; nulo si el usuario no existe.
    /// </summary>
    public async Task<string?> DeUsuarioAsync(int userId, CancellationToken ct)
    {
        var usuario = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.Username).FirstOrDefaultAsync(ct);
        if (usuario is null) return null;
        return (await FormateadorAsync([usuario], ct)).Texto(usuario);
    }

    /// <summary>El modo vigente; vacío o desconocido = <see cref="Nombre"/>.</summary>
    public async Task<string> ModoAsync(CancellationToken ct)
    {
        var valor = await db.SystemSettings.AsNoTracking().Where(s => s.SettingKey == Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct);
        return Normalizar(valor);
    }

    public static string Normalizar(string? valor) =>
        Modos.FirstOrDefault(m => m.Equals(valor?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Nombre;

    /// <summary>El texto del usuario en <paramref name="modo"/>; nulo si sus datos no alcanzan (sin persona): se deja lo guardado.</summary>
    public static string? Formatear(string modo, Datos d)
    {
        var tieneNombre = !string.IsNullOrWhiteSpace(d.PrimerNombre) || !string.IsNullOrWhiteSpace(d.PrimerApellido);
        string? Unir(params string?[] partes)
        {
            var texto = string.Join(" ", partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
            return texto.Length == 0 ? null : texto;
        }
        return modo switch
        {
            NombreCompleto => tieneNombre ? Unir(d.PrimerNombre, d.OtrosNombres, d.PrimerApellido, d.SegundoApellido) : null,
            Iniciales => tieneNombre
                ? Unir(d.PrimerNombre, d.OtrosNombres, d.PrimerApellido, d.SegundoApellido)!
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries).Aggregate(string.Empty, (a, p) => a + char.ToUpperInvariant(p[0]))
                : null,
            Cargo => !string.IsNullOrWhiteSpace(d.Cargo) ? d.Cargo!.Trim() : tieneNombre ? Unir(d.PrimerNombre, d.PrimerApellido) : null,
            _ => tieneNombre ? Unir(d.PrimerNombre, d.PrimerApellido) : null,
        };
    }
}
