using System.Globalization;
using System.Text;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Feature 012 (T177; T24; decisiones-transversales §2.14, Order 82; data-model §0.2): los códigos DIVIPOLA del DANE en
/// <c>COR_Cities.DaneCode</c>, desde <c>Data/divipola.json</c> (embebido). A ellos se refieren por código la sucursal
/// (<c>COR_Branches.MunicipalityDaneCode</c>, que ReteICA de compras propone) y las tarifas municipales. El archivo trae
/// los 1.122 municipios, islas y áreas no municipalizadas del DANE, descargados el 2026-09-25 del conjunto abierto
/// «DIVIPOLA - Códigos municipios» de datos.gov.co (<c>gdxc-w37w</c>); al publicar el DANE uno nuevo sólo cambia el JSON
/// y su <c>version</c>.
///
/// <para>
/// <b>Actualiza por nombre y agrega lo que falta</b>, idempotente: una ciudad viva sin código cuyo nombre —o uno de los
/// alias del archivo, sin tildes ni mayúsculas— coincide con un municipio <b>del mismo departamento</b> recibe su código y
/// conserva su nombre (así no se duplican «Bogota» ni «Buga», que la semilla heredada escribió sin tildes o corto); un
/// municipio sin ciudad se agrega. Lo mismo con los departamentos (por código, luego por nombre) y el país (Colombia). Una
/// ciudad que ya tiene el código no se toca. La columna llega con el par <c>PlataformaParaInventario</c> (T186).
/// </para>
/// </summary>
public sealed class DivipolaSeeder : IDataSeeder
{
    public int Order => 82;
    public SeedCategory Category => SeedCategory.Parametric;
    public SeedScope Scope => SeedScope.Tenant;

    public const string Archivo = "divipola.json";
    public const string Pais = "Colombia";

    public Task<int> SeedAsync(SeedContext context, CancellationToken ct) => AplicarAsync(context.TenantDb!, ct);

    public static SemillaDivipola Semilla() => RecursoJson.Leer<SemillaDivipola>(Archivo);

    /// <summary>La semilla sobre cualquier contexto de la cooperativa (probable con InMemory). Devuelve cuántas filas tocó.</summary>
    public static async Task<int> AplicarAsync(IApplicationDbContext db, CancellationToken ct)
    {
        var semilla = Semilla();
        var cambios = 0;

        var pais = (await db.Countries.ToListAsync(ct)).FirstOrDefault(p => Normalizar(p.Name) == Normalizar(Pais));
        if (pais is null)
        {
            pais = new Country { Name = Pais, CreatedBy = SeedContext.ParametricCreatedBy };
            db.Countries.Add(pais);
            cambios++;
        }

        List<Department> departamentos = pais.Id == 0 ? new() : await db.Departments.Where(d => d.CountryId == pais.Id).ToListAsync(ct);
        var porCodigo = new Dictionary<string, Department>(StringComparer.Ordinal);
        foreach (var d in semilla.Departamentos)
        {
            var nombres = Nombres(d.Nombre, d.Alias);
            var existente = departamentos.FirstOrDefault(x => x.Code == d.Codigo)
                            ?? departamentos.FirstOrDefault(x => nombres.Contains(Normalizar(x.Name)));
            if (existente is null)
            {
                existente = new Department { Country = pais, Code = d.Codigo, Name = d.Nombre, CreatedBy = SeedContext.ParametricCreatedBy };
                db.Departments.Add(existente);
                departamentos.Add(existente);
                cambios++;
            }
            porCodigo[d.Codigo] = existente;
        }

        var ciudades = await db.Cities.Where(c => !c.IsDeleted).ToListAsync(ct);
        var conCodigo = ciudades.Where(c => c.DaneCode != null).Select(c => c.DaneCode!).ToHashSet(StringComparer.Ordinal);
        foreach (var m in semilla.Municipios)
        {
            if (conCodigo.Contains(m.Codigo)) continue;

            var departamento = porCodigo[m.Codigo[..2]];
            var nombres = Nombres(m.Nombre, m.Alias);
            var existente = ciudades.FirstOrDefault(c => c.DaneCode == null
                && MismoDepartamento(c, departamento)
                && nombres.Contains(Normalizar(c.Name)));
            if (existente is not null)
            {
                existente.DaneCode = m.Codigo;
                existente.UpdatedAt = DateTime.UtcNow;
                existente.UpdatedBy = SeedContext.ParametricCreatedBy;
            }
            else
            {
                var nueva = new City { Department = departamento, Name = m.Nombre, DaneCode = m.Codigo, CreatedBy = SeedContext.ParametricCreatedBy };
                db.Cities.Add(nueva);
                ciudades.Add(nueva);
            }
            conCodigo.Add(m.Codigo);
            cambios++;
        }

        if (cambios > 0) await db.SaveChangesAsync(ct);
        return cambios;
    }

    private static bool MismoDepartamento(City ciudad, Department departamento) =>
        ReferenceEquals(ciudad.Department, departamento) || (departamento.Id != 0 && ciudad.DepartmentId == departamento.Id);

    private static HashSet<string> Nombres(string nombre, IEnumerable<string>? alias) =>
        new[] { nombre }.Concat(alias ?? []).Select(Normalizar).ToHashSet(StringComparer.Ordinal);

    /// <summary>Sin tildes, en minúsculas, sin puntuación y con un solo espacio: «Bogotá, D.C.» y «Bogota D.C» son lo mismo.</summary>
    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // ------------------------------------------------------------------------------------------ el archivo --

    public sealed class SemillaDivipola
    {
        public string Version { get; set; } = string.Empty;
        public string Fuente { get; set; } = string.Empty;
        public List<string> Notas { get; set; } = [];
        public List<EntradaDivipola> Departamentos { get; set; } = [];
        public List<EntradaDivipola> Municipios { get; set; } = [];
    }

    public sealed class EntradaDivipola
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public List<string>? Alias { get; set; }
    }
}
