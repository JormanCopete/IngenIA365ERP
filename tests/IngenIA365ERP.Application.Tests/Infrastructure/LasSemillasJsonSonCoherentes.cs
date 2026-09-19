using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Persistence.Seeding.Parametric;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// T028 (feature 009): las semillas JSON de contabilidad son datos que revisa el contador, y un
/// código mal tecleado sembraría un plan de cuentas cojo en cada cooperativa nueva. Aquí se
/// comprueba lo que la norma exige del archivo: padre existente, longitudes por nivel, naturaleza,
/// rubro existente, códigos únicos, conteo mayor que cero, usos válidos y un código por módulo.
/// </summary>
public class LasSemillasJsonSonCoherentes
{
    private static readonly HashSet<string> RubrosValidos =
        FinancialStatementItemsSeeder.Rubros().Items.Select(i => i.Code).ToHashSet(StringComparer.Ordinal);

    public static TheoryData<string> Catalogos => [.. AccountCatalogsSeeder.Recursos];

    [Theory]
    [MemberData(nameof(Catalogos))]
    public void Cada_catalogo_es_coherente(string recurso)
    {
        var catalogo = RecursoJson.Leer<CatalogoJson>(recurso);
        catalogo.Code.Should().NotBeNullOrWhiteSpace();
        catalogo.Accounts.Should().NotBeEmpty();

        var codigos = catalogo.Accounts.Select(a => a[0]).ToList();
        codigos.Should().OnlyHaveUniqueItems("un código repetido se insertaría dos veces");
        codigos.Should().OnlyContain(c => c.All(char.IsDigit), "los códigos oficiales son numéricos");
        codigos.Should().OnlyContain(c => CatalogoJson.NivelDe(c) > 0, "sólo hay clases (1), grupos (2), cuentas (4) y subcuentas (6)");

        var conjunto = codigos.ToHashSet(StringComparer.Ordinal);
        var huerfanas = codigos.Where(c => CatalogoJson.PadreDe(c) is { } p && !conjunto.Contains(p)).ToList();
        huerfanas.Should().BeEmpty("toda cuenta cuelga de una existente");

        var entradas = catalogo.Entradas();
        entradas.Should().HaveCount(codigos.Count);
        entradas.Should().OnlyContain(e => e.Name.Length > 0 && e.Name.Length <= 200);
        entradas.Should().OnlyContain(e => e.Nature == AccountNature.Debit || e.Nature == AccountNature.Credit);
        entradas.Where(e => !RubrosValidos.Contains(e.NiifItemCode)).Should().BeEmpty("el rubro tiene que existir en rubros-niif.json");

        // Nueve clases y las excepciones apuntan a prefijos que existen.
        entradas.Where(e => e.Level == 1).Should().HaveCount(9);
        catalogo.Natures.Exceptions.Keys.Where(k => !codigos.Any(c => c.StartsWith(k, StringComparison.Ordinal)))
            .Should().BeEmpty("una excepción de naturaleza sin cuentas es una errata");
    }

    [Fact]
    public void Los_rubros_son_coherentes()
    {
        var rubros = FinancialStatementItemsSeeder.Rubros();
        rubros.Groups.Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        rubros.Items.Should().NotBeEmpty();
        rubros.Items.Select(i => i.Code).Should().OnlyHaveUniqueItems();
        rubros.Items.Should().OnlyContain(i => i.Code.Length <= 20 && i.Section.Length > 0 && (i.Sign == 1 || i.Sign == -1));
        rubros.Items.Where(i => i.Parent is not null && !RubrosValidos.Contains(i.Parent)).Should().BeEmpty("el padre de un rubro existe");
        rubros.Expandir("test").Select(i => (i.NiifGroup, i.Code)).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Los_tipos_de_comprobante_tienen_usos_validos_y_un_codigo_por_modulo()
    {
        var tipos = VoucherTypesSeeder.Semillas();
        tipos.Should().NotBeEmpty();
        tipos.Select(t => t.Code).Should().OnlyHaveUniqueItems();
        tipos.Should().OnlyContain(t => t.Code.Length >= 2 && t.Code.Length <= 10 && t.Code.ToUpperInvariant() == t.Code);
        tipos.Where(t => t.Usage == VoucherUsage.Module).Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Module), "un tipo de módulo dice de qué módulo es");
        tipos.Where(t => t.Usage is VoucherUsage.Manual or VoucherUsage.Opening or VoucherUsage.Closing).Should().OnlyContain(t => t.Module == null);
        tipos.Should().Contain(t => t.Code == "CG" && t.Usage == VoucherUsage.Manual);
        tipos.Should().Contain(t => t.Code == "NM" && t.Module == "NOM");
        tipos.Should().Contain(t => t.Code == "AP" && t.Usage == VoucherUsage.Opening);
        tipos.Should().Contain(t => t.Code == "CI" && t.Usage == VoucherUsage.Closing);
        tipos.Where(t => t.Module is not null).Select(t => t.Module!).Distinct()
            .Should().BeSubsetOf(["NOM", "CAR", "INV", "TES", "CDT", "ACT"]);
    }

    [Fact]
    public void Los_tipos_de_documento_cruce_traen_los_sembrados_del_modelo()
    {
        var tipos = CrossDocumentTypesSeeder.Semillas();
        tipos.Select(t => t.Code).Should().OnlyHaveUniqueItems();
        tipos.Select(t => t.Code).Should().Contain(["FV", "FC", "CC", "NC", "ND", "CT", "PG", "OT"]);
    }
}
