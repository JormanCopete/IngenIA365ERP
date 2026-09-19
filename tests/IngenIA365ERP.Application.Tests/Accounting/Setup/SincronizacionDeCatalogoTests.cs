using FluentAssertions;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Accounting.Setup;

/// <summary>
/// 2026-09-18: el PUC solidario pasó de una transcripción del equipo (695 códigos, varios que no
/// existen) al CUIF oficial (2.110). Un catálogo ya sembrado se pone al día cuando el archivo trae
/// otra versión: corrige en su sitio, inserta lo que falta, retira lo que sobra, invalida la
/// validación del contador y, si el plan de la empresa sigue intacto, lo vuelve a copiar.
/// </summary>
public class SincronizacionDeCatalogoTests
{
    private static CatalogoJson Archivo(string version, params (string Code, string Name)[] cuentas) => new()
    {
        Code = "PRUEBA", Name = "Catálogo de prueba (oficial)", Version = version,
        Natures = new CatalogoJson.NaturalezasJson { Default = new() { ["1"] = "D", ["2"] = "C", ["3"] = "C" }, Exceptions = new() { ["1199"] = "C" } },
        Niif = [new("1", "ESF-A-EFE"), new("2", "ESF-P-OTR"), new("3", "ESF-PT-RES")],
        Accounts = cuentas.Select(c => new[] { c.Code, c.Name }).ToList(),
    };

    private static async Task<AccountCatalog> SembradoAsync(ContabilidadTestData d)
    {
        var catalogo = await d.Db.AccountCatalogs.SingleAsync(c => c.Code == "PRUEBA");
        catalogo.Source = CatalogSource.Official;
        catalogo.Version = "v1";
        catalogo.ValidatedAt = ContabilidadTestData.Ahora; catalogo.ValidatedBy = "contador";
        foreach (var (code, name) in new[] { ("1", "ACTIVO"), ("11", "EFECTIVO"), ("1105", "CAJA"), ("110505", "Caja general"), ("1115", "CUENTAS DE AHORRO (inventada)") })
            d.Db.AccountCatalogEntries.Add(new AccountCatalogEntry
            {
                CatalogId = catalogo.Id, Code = code, Name = name, Level = CatalogoJson.NivelDe(code), Nature = AccountNature.Debit,
                NiifItemCode = "ESF-A-EFE", ParentCode = CatalogoJson.PadreDe(code), CreatedBy = "seed",
            });
        await d.Db.SaveChangesAsync();
        return catalogo;
    }

    private static readonly (string, string)[] Oficial =
    [
        ("1", "ACTIVO"), ("11", "EFECTIVO Y EQUIVALENTE AL EFECTIVO"), ("1105", "CAJA"), ("110505", "CAJA GENERAL"), ("110510", "CAJA MENOR"),
        ("3", "PATRIMONIO"), ("32", "RESERVAS"), ("3205", "RESERVA PROTECCIÓN DE APORTES"),
    ];

    [Fact]
    public async Task Con_la_misma_version_no_toca_nada()
    {
        var d = new ContabilidadTestData(iniciada: false);
        var catalogo = await SembradoAsync(d);

        var r = await SincronizacionDeCatalogo.SincronizarAsync(d.Db, catalogo, Archivo("v1", Oficial), "seed", ContabilidadTestData.Ahora, NullLogger.Instance, CancellationToken.None);

        r.Should().BeNull();
        catalogo.ValidatedBy.Should().Be("contador");
        (await d.Db.AccountCatalogEntries.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task Con_otra_version_corrige_inserta_retira_e_invalida_al_contador()
    {
        var d = new ContabilidadTestData(iniciada: false);
        var catalogo = await SembradoAsync(d);

        var r = await SincronizacionDeCatalogo.SincronizarAsync(d.Db, catalogo, Archivo("v2", Oficial), "seed", ContabilidadTestData.Ahora, NullLogger.Instance, CancellationToken.None);

        r.Should().NotBeNull();
        r!.Corregidas.Should().Be(2, "11 y 110505 cambian de nombre");
        r.Insertadas.Should().Be(4, "110510, 3, 32 y 3205");
        r.Retiradas.Should().Be(1, "1115 no existe en el oficial");
        r.PlanRefrescado.Should().BeFalse(); r.PlanNoRefrescadoPorque.Should().BeNull("no hay contabilidad iniciada");
        catalogo.Version.Should().Be("v2"); catalogo.EntryCount.Should().Be(8);
        catalogo.ValidatedAt.Should().BeNull(); catalogo.ValidatedBy.Should().BeNull();
        var vivas = await d.Db.AccountCatalogEntries.Where(e => !e.IsDeleted).ToListAsync();
        vivas.Select(e => e.Code).Should().BeEquivalentTo(Oficial.Select(o => o.Item1));
        vivas.Single(e => e.Code == "110505").Name.Should().Be("CAJA GENERAL");
        vivas.Single(e => e.Code == "3205").Nature.Should().Be(AccountNature.Credit);
        (await d.Db.AccountCatalogEntries.IgnoreQueryFilters().SingleAsync(e => e.Code == "1115")).IsDeleted.Should().BeTrue("retirada, no borrada");

        // Volver a correr con la misma versión ya no hace nada; una tercera versión revive lo retirado.
        (await SincronizacionDeCatalogo.SincronizarAsync(d.Db, catalogo, Archivo("v2", Oficial), "seed", ContabilidadTestData.Ahora, NullLogger.Instance, CancellationToken.None)).Should().BeNull();
        var r3 = await SincronizacionDeCatalogo.SincronizarAsync(d.Db, catalogo, Archivo("v3", [.. Oficial, ("1115", "CUENTAS DE AHORRO")]), "seed", ContabilidadTestData.Ahora, NullLogger.Instance, CancellationToken.None);
        r3!.Corregidas.Should().Be(1, "1115 revive con su nombre nuevo"); r3.Insertadas.Should().Be(0);
        (await d.Db.AccountCatalogEntries.CountAsync(e => e.Code == "1115")).Should().Be(1, "no se duplica: el índice (catálogo, código) es único");
    }

    [Fact]
    public async Task Un_plan_intacto_copiado_del_catalogo_se_vuelve_a_copiar_y_uno_con_cuentas_propias_no()
    {
        var d = new ContabilidadTestData(iniciada: true);
        var catalogo = await SembradoAsync(d);
        var cuantasAntes = await d.Db.ChartOfAccounts.CountAsync(a => !a.IsDeleted);

        var r = await SincronizacionDeCatalogo.SincronizarAsync(d.Db, catalogo, Archivo("v2", Oficial), "seed", ContabilidadTestData.Ahora, NullLogger.Instance, CancellationToken.None);

        r!.PlanRefrescado.Should().BeTrue();
        var plan = await d.Db.ChartOfAccounts.Where(a => !a.IsDeleted).ToListAsync();
        plan.Select(a => a.Code).Should().BeEquivalentTo(Oficial.Select(o => o.Item1));
        plan.Should().OnlyContain(a => a.Origin == AccountOrigin.Catalog && !a.IsMovement);
        plan.Single(a => a.Code == "3205").ParentId.Should().Be(plan.Single(a => a.Code == "32").Id);
        (await d.Db.ChartOfAccounts.IgnoreQueryFilters().CountAsync(a => a.IsDeleted)).Should().Be(cuantasAntes, "el plan anterior queda retirado, no borrado");

        // Con una cuenta propia, el plan se conserva y el motivo queda dicho.
        var propia = new ChartOfAccount { Code = "11050501", Name = "Caja principal", Level = 5, Nature = AccountNature.Debit, NiifItemCode = "ESF-A-EFE", Origin = AccountOrigin.Company, IsMovement = true, CreatedBy = "test" };
        d.Db.ChartOfAccounts.Add(propia); await d.Db.SaveChangesAsync();
        var r3 = await SincronizacionDeCatalogo.SincronizarAsync(d.Db, catalogo, Archivo("v3", Oficial), "seed", ContabilidadTestData.Ahora, NullLogger.Instance, CancellationToken.None);
        r3!.PlanRefrescado.Should().BeFalse();
        r3.PlanNoRefrescadoPorque.Should().Contain("1 cuenta(s) propia(s)");
        (await d.Db.ChartOfAccounts.CountAsync(a => !a.IsDeleted)).Should().Be(plan.Count + 1);
    }
}
