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
/// <c>NOW()</c> a secas.
/// </para>
///
/// <para>
/// Consolidación de migraciones (2026-11-15): <c>NominaPrestacionesYDian</c> quedó absorbida por <c>EsquemaBase</c>. Las dos
/// pruebas que leían su archivo se retiraron con ella: ese paso de datos ya corrió en todas las bases que existían y sobre
/// una base nueva no hace nada (actualiza filas que la semilla todavía no escribió). Queda la del sembrador, que es la que
/// pone al día una base migrada con los textos viejos.
/// </para>
/// </summary>
public class MigracionYSemillaDicenLaMismaNormaTests
{
    [Fact]
    public async Task Una_base_migrada_con_los_textos_viejos_de_la_migracion_se_pone_al_dia_en_el_arranque_siguiente()
    {
        using var db = TestDbContextFactory.Create();
        var base2026 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.PayrollLegalParameters.Add(new PayrollLegalParameter
        {
            Code = LegalParameterCodes.TransportAllowance, Name = "Auxilio de transporte", Kind = LegalParameterKind.Amount, Value = 249_095m,
            ValidFrom = base2026, Source = "Decreto 1470 de 2025 (auxilio de transporte 2026)", CreatedBy = SeedContext.ParametricCreatedBy,
        });
        // Feature 013 (T93): el SMMLV ya no es de esta semilla; lo adopta la réplica (EfectoDeReplicacionEnNominaTests).
        db.PayrollLegalParameters.Add(new PayrollLegalParameter
        {
            Code = LegalParameterCodes.Smmlv, Name = "Salario mínimo", Kind = LegalParameterKind.Amount, Value = 1_750_905m,
            ValidFrom = base2026, Source = "Decreto 1469 de 2025 (salario mínimo 2026; Decreto 159 de 2026, mismo valor)", CreatedBy = SeedContext.ParametricCreatedBy,
        });
        await db.SaveChangesAsync();

        await PayrollLegalParametersSeeder.AplicarAsync(db, CancellationToken.None);

        var catalogo = PayrollLegalParametersSeeder.Catalogo();
        (await db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.TransportAllowance)).Source
            .Should().Be(catalogo.Single(p => p.Code == LegalParameterCodes.TransportAllowance).Source);
        (await db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.Smmlv)).Source
            .Should().Be("Decreto 1469 de 2025 (salario mínimo 2026; Decreto 159 de 2026, mismo valor)", "la semilla de Nómina ya no toca el SMMLV");
    }

}
