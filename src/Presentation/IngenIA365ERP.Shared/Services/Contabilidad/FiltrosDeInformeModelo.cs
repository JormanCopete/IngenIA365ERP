using System.Globalization;
using System.Text;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Los filtros combinables de las consultas contables (feature 009 E2, FR-043) tal como los
/// maneja la pantalla. Es el espejo de <c>FiltrosDeInforme</c> de Application: los nombres de
/// <see cref="ToQuery"/> son los de la query string de <c>/api/reports/accounting/{vista}</c>
/// (contracts/api.md §8), y <see cref="DesdeQuery"/> los lee de vuelta para que una pantalla
/// pueda llegar a otra con los mismos filtros («ver el libro auxiliar de esta cuenta con este
/// tercero»). Es mutable porque lo edita el panel de filtros; la API recibe la copia serializada.
/// </summary>
public sealed class FiltrosDeInformeModelo
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public string? AccountFrom { get; set; }
    public string? AccountTo { get; set; }
    /// <summary>Rama del plan: la cuenta y todo lo que cuelga de ella.</summary>
    public Guid? AccountPublicId { get; set; }
    /// <summary>Sólo para mostrar la cuenta elegida; no viaja a la API.</summary>
    public string? AccountTexto { get; set; }
    /// <summary>
    /// Código de rubro NIIF («ESF-A-EFE»): las cuentas del rubro y de sus descendientes. No tiene
    /// control propio en el panel —llega desde los estados financieros, con el clic en una fila—;
    /// el panel lo muestra y deja quitarlo.
    /// </summary>
    public string? NiifItem { get; set; }
    public Guid? Person { get; set; }
    /// <summary>Sólo para mostrar al tercero elegido; no viaja a la API.</summary>
    public string? PersonTexto { get; set; }
    /// <summary>Código del tipo de documento cruce (FC, RC…).</summary>
    public string? CrossDocumentType { get; set; }
    public string? CrossDocumentNumber { get; set; }
    public Guid? CostCenter { get; set; }
    public Guid? Branch { get; set; }
    public string? VoucherType { get; set; }
    public string? Origin { get; set; }
    public string? User { get; set; }
    public int? Level { get; set; }
    public bool WithThirdParties { get; set; }
    public bool IncludeClosing { get; set; }

    /// <summary>«TIPO|NÚMERO» o sólo «TIPO», como lo espera la API; nulo si no hay documento.</summary>
    public string? CrossDocument
    {
        get
        {
            var tipo = Limpio(CrossDocumentType)?.ToUpperInvariant();
            var numero = Limpio(CrossDocumentNumber);
            if (tipo is null) return null;
            return numero is null ? tipo : $"{tipo}|{numero}";
        }
    }

    /// <summary>
    /// Cuántos filtros hay puestos además de las fechas: la pantalla lo muestra en el título del
    /// panel plegable («Filtros (2)») para que se sepa que el resultado está acotado sin abrirlo.
    /// </summary>
    public int Cantidad =>
        (Limpio(AccountFrom) is not null || Limpio(AccountTo) is not null ? 1 : 0)
        + (AccountPublicId is not null ? 1 : 0)
        + (Limpio(NiifItem) is not null ? 1 : 0)
        + (Person is not null ? 1 : 0)
        + (CrossDocument is not null ? 1 : 0)
        + (CostCenter is not null ? 1 : 0)
        + (Branch is not null ? 1 : 0)
        + (Limpio(VoucherType) is not null ? 1 : 0)
        + (Limpio(Origin) is not null ? 1 : 0)
        + (Limpio(User) is not null ? 1 : 0)
        + (Level is not null ? 1 : 0)
        + (WithThirdParties ? 1 : 0)
        + (IncludeClosing ? 1 : 0);

    /// <summary>Query string sin «?» inicial, con los nombres de la API y fechas <c>yyyy-MM-dd</c>. Vacía si no hay nada.</summary>
    public string ToQuery()
    {
        var sb = new StringBuilder();
        Agregar(sb, "from", From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Agregar(sb, "to", To?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Agregar(sb, "accountFrom", Limpio(AccountFrom));
        Agregar(sb, "accountTo", Limpio(AccountTo));
        Agregar(sb, "accountPublicId", AccountPublicId?.ToString());
        Agregar(sb, "niifItem", Limpio(NiifItem)?.ToUpperInvariant());
        Agregar(sb, "person", Person?.ToString());
        Agregar(sb, "crossDocument", CrossDocument);
        Agregar(sb, "costCenter", CostCenter?.ToString());
        Agregar(sb, "branch", Branch?.ToString());
        Agregar(sb, "voucherType", Limpio(VoucherType));
        Agregar(sb, "origin", Limpio(Origin));
        Agregar(sb, "user", Limpio(User));
        Agregar(sb, "level", Level?.ToString(CultureInfo.InvariantCulture));
        if (WithThirdParties) Agregar(sb, "withThirdParties", "true");
        if (IncludeClosing) Agregar(sb, "includeClosing", "true");
        return sb.ToString();
    }

    /// <summary>
    /// Lee <c>?from=&amp;to=&amp;person=…</c> (con o sin «?», o una URL completa). Lo que no
    /// reconoce lo ignora: <c>vista</c>, <c>node</c>, <c>estado</c> son de cada pantalla.
    /// </summary>
    public static FiltrosDeInformeModelo DesdeQuery(string? query)
    {
        var m = new FiltrosDeInformeModelo();
        if (string.IsNullOrWhiteSpace(query)) return m;
        var q = query;
        var signo = q.IndexOf('?');
        if (signo >= 0) q = q[(signo + 1)..];
        foreach (var par in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var igual = par.IndexOf('=');
            var clave = Uri.UnescapeDataString(igual < 0 ? par : par[..igual]);
            var valor = igual < 0 ? string.Empty : Uri.UnescapeDataString(par[(igual + 1)..].Replace('+', ' '));
            if (string.IsNullOrWhiteSpace(valor)) continue;
            switch (clave.ToLowerInvariant())
            {
                case "from": if (DateOnly.TryParse(valor, CultureInfo.InvariantCulture, out var d)) m.From = d; break;
                case "to": if (DateOnly.TryParse(valor, CultureInfo.InvariantCulture, out var h)) m.To = h; break;
                case "accountfrom": m.AccountFrom = valor; break;
                case "accountto": m.AccountTo = valor; break;
                case "accountpublicid": if (Guid.TryParse(valor, out var c)) m.AccountPublicId = c; break;
                case "niifitem": m.NiifItem = valor.ToUpperInvariant(); break;
                case "person": if (Guid.TryParse(valor, out var p)) m.Person = p; break;
                case "crossdocument":
                    var partes = valor.Split('|', 2, StringSplitOptions.TrimEntries);
                    m.CrossDocumentType = Limpio(partes[0]);
                    m.CrossDocumentNumber = partes.Length > 1 ? Limpio(partes[1]) : null;
                    break;
                case "costcenter": if (Guid.TryParse(valor, out var cc)) m.CostCenter = cc; break;
                case "branch": if (Guid.TryParse(valor, out var b)) m.Branch = b; break;
                case "vouchertype": m.VoucherType = valor; break;
                case "origin": m.Origin = valor; break;
                case "user": m.User = valor; break;
                case "level": if (int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) m.Level = n; break;
                case "withthirdparties": m.WithThirdParties = EsVerdadero(valor); break;
                case "includeclosing": m.IncludeClosing = EsVerdadero(valor); break;
            }
        }
        return m;
    }

    /// <summary>Copia independiente: la pantalla edita una y consulta con la otra hasta que pulsa «Aplicar».</summary>
    public FiltrosDeInformeModelo Copia() => new()
    {
        From = From, To = To, AccountFrom = AccountFrom, AccountTo = AccountTo,
        AccountPublicId = AccountPublicId, AccountTexto = AccountTexto, NiifItem = NiifItem, Person = Person, PersonTexto = PersonTexto,
        CrossDocumentType = CrossDocumentType, CrossDocumentNumber = CrossDocumentNumber,
        CostCenter = CostCenter, Branch = Branch, VoucherType = VoucherType, Origin = Origin, User = User,
        Level = Level, WithThirdParties = WithThirdParties, IncludeClosing = IncludeClosing,
    };

    /// <summary>Une dos trozos de query string cuidando el «&amp;»; cualquiera puede venir vacío.</summary>
    public static string Unir(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b;
        if (string.IsNullOrEmpty(b)) return a;
        return $"{a}&{b}";
    }

    private static void Agregar(StringBuilder sb, string clave, string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return;
        if (sb.Length > 0) sb.Append('&');
        sb.Append(clave).Append('=').Append(Uri.EscapeDataString(valor));
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static bool EsVerdadero(string s) => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1";
}
