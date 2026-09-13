using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <summary>
    /// Corrección de la vigencia base 2026 de la semilla de nómina (2026-09-13). La semilla es
    /// idempotente por <c>(Code, ValidFrom)</c> y por eso <b>no pisa</b> filas existentes: las
    /// cooperativas sembradas antes del 2026-09-12 quedaron con los valores viejos del
    /// 2026-01-01 —<c>HORAS_MES</c> 240, <c>RECARGO_DOMINICAL</c> 0,75, extras dominicales
    /// 2,00 / 2,50— aunque desde el 15/07/2025 rigen 44 h/semana (220 h/mes, Ley 2101 de 2021)
    /// y desde el 01/07/2025 el recargo dominical del 80 % (Ley 2466 de 2025). Se actualizan
    /// <b>sólo</b> las filas que siguen siendo las de la semilla: origen/creador de semilla y el
    /// valor viejo intacto; una vigencia que la cooperativa ya corrigió a mano no se toca.
    /// <c>Down</c> devuelve los valores anteriores con la misma condición.
    /// </summary>
    public partial class BaseLegal2026Corregida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE dbo.PAY_LegalParameters
                SET [Value] = 220, [Source] = N'Ley 2101 de 2021 (jornada de 44 h desde el 15/07/2025)',
                    UpdatedBy = 'system:seed', UpdatedAt = SYSUTCDATETIME()
                WHERE Code = 'HORAS_MES' AND ValidFrom = '2026-01-01'
                  AND [Value] = 240 AND CreatedBy = 'system:seed';

                UPDATE c
                SET c.UnitFactor = v.nuevo, c.UpdatedBy = 'system:seed', c.UpdatedAt = SYSUTCDATETIME()
                FROM dbo.PAY_ConceptDefinitions c
                JOIN (VALUES ('RECARGO_DOMINICAL', 0.75, 0.80),
                             ('HEX_DOM_DIURNA',    2.00, 2.05),
                             ('HEX_DOM_NOCTURNA',  2.50, 2.55)) AS v(code, viejo, nuevo) ON v.code = c.Code
                WHERE c.ValidFrom = '2026-01-01' AND c.Origin = 0 AND c.UnitFactor = v.viejo;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE dbo.PAY_LegalParameters
                SET [Value] = 240, UpdatedBy = 'system:seed', UpdatedAt = SYSUTCDATETIME()
                WHERE Code = 'HORAS_MES' AND ValidFrom = '2026-01-01'
                  AND [Value] = 220 AND CreatedBy = 'system:seed';

                UPDATE c
                SET c.UnitFactor = v.viejo, c.UpdatedBy = 'system:seed', c.UpdatedAt = SYSUTCDATETIME()
                FROM dbo.PAY_ConceptDefinitions c
                JOIN (VALUES ('RECARGO_DOMINICAL', 0.75, 0.80),
                             ('HEX_DOM_DIURNA',    2.00, 2.05),
                             ('HEX_DOM_NOCTURNA',  2.50, 2.55)) AS v(code, viejo, nuevo) ON v.code = c.Code
                WHERE c.ValidFrom = '2026-01-01' AND c.Origin = 0 AND c.UnitFactor = v.nuevo;
                """);

        }
    }
}
