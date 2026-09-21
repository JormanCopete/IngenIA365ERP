using System.Text.RegularExpressions;
using FluentAssertions;
using IngenIA365ERP.Application.Payroll.LegalParameters;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Persistence.Seeding;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Infrastructure;

/// <summary>
/// Revisión de N1 (feature 010, 2026-09-21): el paso de datos (3) de la migración
/// <c>NominaPrestacionesYDian</c> precisa el <c>Source</c> de los parámetros que todavía llevan el
/// texto genérico de la semilla 2026 —una decisión del plan (data-model §4.1)— y corre ANTES que el
/// seeder, que respeta cualquier <c>Source</c> no genérico. Escribía cuatro textos distintos de las
/// constantes de <see cref="PayrollLegalParametersSeeder"/>: la cooperativa migrada quedaba con una
/// norma y la creada después con otra, para siempre. Aquí se cruzan los literales de las dos
/// migraciones con el catálogo, y se fija que los textos viejos de la migración pasaron a genéricos
/// para que una base ya migrada se ponga al día sola en el arranque siguiente.
///
/// <para>
/// De paso, la migración PostgreSQL insertaba <c>CreatedAt</c> (timestamptz) con
/// <c>NOW() AT TIME ZONE 'UTC'</c>, un timestamp sin zona que el motor reinterpreta en la zona de la
/// sesión: con el servidor en America/Bogota la política quedaba creada cinco horas después. Es
/// <c>NOW()</c> a secas, y esta prueba lo vigila en el archivo.
/// </para>
/// </summary>
public class MigracionYSemillaDicenLaMismaNormaTests
{
    private static readonly Regex SourceEnSql = new(@"SET\s+(?:\[Source\]|""Source"")\s*=\s*N?'([^']+)'", RegexOptions.Compiled);

    private const string MigracionPostgres = "src/Infrastructure/IngenIA365ERP.Persistence.Migrations.PostgreSql/Application/20260921034933_NominaPrestacionesYDian.cs";
    private const string MigracionSqlServer = "src/Infrastructure/IngenIA365ERP.Persistence.Migrations.SqlServer/Application/20260921034851_NominaPrestacionesYDian.cs";

    [Theory]
    [InlineData(MigracionPostgres)]
    [InlineData(MigracionSqlServer)]
    public void Cada_Source_que_escribe_la_migracion_es_una_constante_del_seeder(string relativo)
    {
        var codigo = File.ReadAllText(Path.Combine(RaizDelRepositorio(), relativo));
        var enMigracion = SourceEnSql.Matches(codigo).Select(m => m.Groups[1].Value).ToList();
        var enSeeder = PayrollLegalParametersSeeder.Catalogo().Select(p => p.Source!).ToHashSet(StringComparer.Ordinal);

        enMigracion.Should().NotBeEmpty("el paso (3) de la migración precisa cinco normas");
        enMigracion.Should().OnlyContain(s => enSeeder.Contains(s),
            "la cooperativa migrada y la creada después tienen que leer la MISMA norma; el seeder respeta todo Source no genérico y no lo corregiría");
        (codigo.Contains("\"IsDeleted\" = FALSE AND \"Source\"", StringComparison.Ordinal) || codigo.Contains("[IsDeleted] = 0 AND [Source]", StringComparison.Ordinal))
            .Should().BeTrue("una vigencia eliminada no se reescribe");
    }

    [Fact]
    public void En_PostgreSQL_CreatedAt_se_inserta_con_NOW_a_secas_porque_la_columna_es_timestamptz()
    {
        // Sólo el SQL: el comentario del paso (1) explica precisamente la forma que se retiró.
        var sql = string.Join('\n', File.ReadLines(Path.Combine(RaizDelRepositorio(), MigracionPostgres)).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        sql.Should().NotContain("NOW() AT TIME ZONE", "«NOW() AT TIME ZONE 'UTC'» es un timestamp sin zona que el motor reinterpreta en la zona de la sesión");
        Regex.Matches(sql, @"NOW\(\)").Count.Should().Be(2, "las dos políticas copiadas de COR_SystemSettings");
    }

    [Fact]
    public async Task Una_base_migrada_con_los_textos_viejos_de_la_migracion_se_pone_al_dia_en_el_arranque_siguiente()
    {
        using var db = TestDbContextFactory.Create();
        var base2026 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.PayrollLegalParameters.Add(new PayrollLegalParameter
        {
            Code = LegalParameterCodes.Smmlv, Name = "Salario mínimo", Kind = LegalParameterKind.Amount, Value = 1_750_905m,
            ValidFrom = base2026, Source = "Decreto 1469 de 2025 (salario mínimo 2026; Decreto 159 de 2026, mismo valor)", CreatedBy = SeedContext.ParametricCreatedBy,
        });
        db.PayrollLegalParameters.Add(new PayrollLegalParameter
        {
            Code = LegalParameterCodes.Uvt, Name = "UVT", Kind = LegalParameterKind.Amount, Value = 52_374m,
            ValidFrom = base2026, Source = "Resolución DIAN 000238 de 2025 (UVT 2026)", CreatedBy = SeedContext.ParametricCreatedBy,
        });
        await db.SaveChangesAsync();

        await PayrollLegalParametersSeeder.AplicarAsync(db, CancellationToken.None);

        var catalogo = PayrollLegalParametersSeeder.Catalogo();
        (await db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.Smmlv)).Source
            .Should().Be(catalogo.Single(p => p.Code == LegalParameterCodes.Smmlv).Source);
        (await db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.Uvt)).Source
            .Should().Be(catalogo.Single(p => p.Code == LegalParameterCodes.Uvt).Source);
    }

    private static string RaizDelRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "IngenIA365ERP.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encontró la raíz del repositorio (IngenIA365ERP.slnx).");
    }
}
