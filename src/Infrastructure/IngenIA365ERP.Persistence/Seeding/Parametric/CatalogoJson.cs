using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Persistence.Seeding.Parametric;

/// <summary>
/// Un catálogo de cuentas tal como viene en <c>puc-*.json</c> (docs/manual/semillas-json.md) y su
/// expansión a entradas: el nivel sale de la longitud del código, la naturaleza de la clase salvo
/// los prefijos con excepción, el rubro NIIF del prefijo más largo que coincida y el padre del
/// código recortado. Lo usan el seeder y la prueba de coherencia; así los dos miran lo mismo.
/// </summary>
public sealed class CatalogoJson
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public NaturalezasJson Natures { get; init; } = new();
    public List<RubroPorPrefijoJson> Niif { get; init; } = [];
    public List<string[]> Accounts { get; init; } = [];

    public sealed class NaturalezasJson
    {
        public Dictionary<string, string> Default { get; init; } = [];
        public Dictionary<string, string> Exceptions { get; init; } = [];
    }

    public sealed record RubroPorPrefijoJson(string Prefix, string Item);

    /// <summary>Una entrada ya resuelta (sin catálogo asignado todavía).</summary>
    public sealed record EntradaResuelta(string Code, string Name, byte Level, AccountNature Nature, string NiifItemCode, string? ParentCode);

    public static byte NivelDe(string code) => code.Length switch { 1 => 1, 2 => 2, 4 => 3, 6 => 4, _ => 0 };

    public static string? PadreDe(string code) => code.Length switch { 2 => code[..1], 4 => code[..2], 6 => code[..4], _ => null };

    public AccountNature? NaturalezaDe(string code)
    {
        // El prefijo de excepción más largo manda; si no hay, la clase.
        var excepcion = Natures.Exceptions.Keys.Where(code.StartsWith).OrderByDescending(k => k.Length).FirstOrDefault();
        var letra = excepcion is not null ? Natures.Exceptions[excepcion] : Natures.Default.GetValueOrDefault(code[..1]);
        return letra?.ToUpperInvariant() switch { "D" => AccountNature.Debit, "C" => AccountNature.Credit, _ => null };
    }

    public string? RubroDe(string code) =>
        Niif.Where(r => code.StartsWith(r.Prefix, StringComparison.Ordinal)).OrderByDescending(r => r.Prefix.Length).Select(r => r.Item).FirstOrDefault();

    /// <summary>Todas las entradas resueltas, en el orden del archivo. Lanza si algo no resuelve (la prueba lo dice antes).</summary>
    public IReadOnlyList<EntradaResuelta> Entradas()
    {
        var lista = new List<EntradaResuelta>(Accounts.Count);
        foreach (var fila in Accounts)
        {
            var code = fila[0].Trim();
            var nivel = NivelDe(code);
            var naturaleza = NaturalezaDe(code) ?? throw new InvalidOperationException($"{Code}: la cuenta {code} no tiene naturaleza.");
            var rubro = RubroDe(code) ?? throw new InvalidOperationException($"{Code}: la cuenta {code} no cae en ningún rubro NIIF.");
            lista.Add(new EntradaResuelta(code, fila[1].Trim(), nivel, naturaleza, rubro, PadreDe(code)));
        }
        return lista;
    }

    public AccountCatalog ComoCatalogo(string createdBy)
    {
        var entradas = Entradas();
        var catalogo = new AccountCatalog
        {
            Code = Code.Trim().ToUpperInvariant(),
            Name = Name,
            Version = Version,
            Source = CatalogSource.Official,
            EntryCount = entradas.Count,
            CreatedBy = createdBy,
        };
        foreach (var e in entradas)
        {
            catalogo.Entries.Add(new AccountCatalogEntry
            {
                Code = e.Code, Name = e.Name, Level = e.Level, Nature = e.Nature, NiifItemCode = e.NiifItemCode, ParentCode = e.ParentCode, CreatedBy = createdBy,
            });
        }
        return catalogo;
    }
}

/// <summary>Los rubros de <c>rubros-niif.json</c>: un rubro sirve a los grupos de "groups" (o a todos los del archivo).</summary>
public sealed class RubrosJson
{
    public List<byte> Groups { get; init; } = [];
    public List<RubroJson> Items { get; init; } = [];

    public sealed class RubroJson
    {
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public FinancialStatementKind Statement { get; init; }
        public string Section { get; init; } = string.Empty;
        public int Order { get; init; }
        public short Sign { get; init; } = 1;
        public string? Parent { get; init; }
        public List<byte>? Groups { get; init; }
    }

    public IEnumerable<FinancialStatementItem> Expandir(string createdBy)
    {
        foreach (var r in Items)
            foreach (var grupo in r.Groups ?? Groups)
                yield return new FinancialStatementItem
                {
                    NiifGroup = grupo, Code = r.Code, Name = r.Name, Statement = r.Statement, Section = r.Section,
                    Order = r.Order, Sign = r.Sign, ParentCode = r.Parent, CreatedBy = createdBy,
                };
    }
}
