using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Complemento de <c>BaseLegal2026Corregida</c> (2026-09-13). Aquella condicionó la fila de
    /// <c>HORAS_MES</c> a <c>CreatedBy = 'system:seed'</c>, pero en producción la semilla corrió
    /// bajo un usuario (el interceptor de auditoría escribe quién ejecutó, no quién definió) y la
    /// fila quedó con 240 sin corregirse; los conceptos sí se corrigieron porque su guarda era
    /// <c>Origin = Seed</c>. Aquí la guarda es lo que identifica a la fila intacta de la semilla
    /// vieja: valor 240 y la fuente original «Código Sustantivo del Trabajo y Ley 50 de 1990».
    /// Una vigencia ya corregida a mano (otro valor u otra fuente) no se toca. <c>Down</c>
    /// revierte con la condición inversa.
    /// </summary>
    public partial class HorasMesBase2026 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE dbo."PAY_LegalParameters"
                SET "Value" = 220, "Source" = 'Ley 2101 de 2021 (jornada de 44 h desde el 15/07/2025)',
                    "UpdatedBy" = 'system:seed', "UpdatedAt" = (now() at time zone 'utc')
                WHERE "Code" = 'HORAS_MES' AND "ValidFrom" = TIMESTAMPTZ '2026-01-01 00:00:00+00'
                  AND "Value" = 240 AND "Source" = 'Código Sustantivo del Trabajo y Ley 50 de 1990';
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE dbo."PAY_LegalParameters"
                SET "Value" = 240, "Source" = 'Código Sustantivo del Trabajo y Ley 50 de 1990',
                    "UpdatedBy" = 'system:seed', "UpdatedAt" = (now() at time zone 'utc')
                WHERE "Code" = 'HORAS_MES' AND "ValidFrom" = TIMESTAMPTZ '2026-01-01 00:00:00+00'
                  AND "Value" = 220 AND "Source" = 'Ley 2101 de 2021 (jornada de 44 h desde el 15/07/2025)';
                """);

        }
    }
}
