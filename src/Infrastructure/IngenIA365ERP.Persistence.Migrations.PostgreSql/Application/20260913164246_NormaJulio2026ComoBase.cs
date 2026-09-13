using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Decisión del 2026-09-13: la base 2026 de la semilla lleva ya la norma de julio de 2026
    /// (jornada de 42 h → <c>HORAS_MES</c> 210; recargo dominical 90 % → <c>RECARGO_DOMINICAL</c>
    /// 0,90, extras dominicales 2,15 / 2,65). La plataforma está en pruebas y ninguna nómina real del
    /// primer semestre de 2026 se liquida aquí, así que no vale la pena cargar dos vigencias.
    /// Para las cooperativas ya sembradas la vigencia base (2026-01-01) toma el valor de julio, sólo
    /// si sigue siendo la de la semilla (valor viejo intacto, origen de semilla). Las vigencias de
    /// julio que ya existían <b>no se borran</b> (Principio XII: nada destructivo aquí; llevan el mismo
    /// valor y pueden estar referenciadas por novedades o líneas de corrida), así que esas
    /// cooperativas quedan con dos filas de igual valor y las nuevas con una. Lo registrado a mano
    /// no se toca. <c>Down</c> devuelve el valor y la fuente anteriores a la base.
    /// </summary>
    public partial class NormaJulio2026ComoBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                -- Base 2026 a 210 (la vigencia de julio, si existe, se conserva con el mismo valor).
                UPDATE dbo."PAY_LegalParameters" p
                SET "Value" = 210, "Source" = 'Ley 2101 de 2021 (jornada de 42 h desde el 15/07/2026)',
                    "UpdatedBy" = 'system:seed', "UpdatedAt" = (now() at time zone 'utc')
                WHERE p."Code" = 'HORAS_MES' AND p."ValidFrom" = TIMESTAMPTZ '2026-01-01 00:00:00+00'
                  AND p."Value" IN (220, 240);

                -- Base 2026 con los factores de julio (las versiones de julio, si existen, se conservan con el mismo valor).
                UPDATE dbo."PAY_ConceptDefinitions" c
                SET "UnitFactor" = v.nuevo,
                    "UpdatedBy" = 'system:seed', "UpdatedAt" = (now() at time zone 'utc')
                FROM (VALUES ('RECARGO_DOMINICAL', 0.80, 0.90), ('HEX_DOM_DIURNA', 2.05, 2.15), ('HEX_DOM_NOCTURNA', 2.55, 2.65)) AS v(code, viejo, nuevo)
                WHERE c."Code" = v.code AND c."ValidFrom" = TIMESTAMPTZ '2026-01-01 00:00:00+00'
                  AND c."Origin" = 0 AND c."UnitFactor" = v.viejo;
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE dbo."PAY_LegalParameters"
                SET "Value" = 220, "Source" = 'Ley 2101 de 2021 (jornada de 44 h desde el 15/07/2025)', "UpdatedBy" = 'system:seed', "UpdatedAt" = (now() at time zone 'utc')
                WHERE "Code" = 'HORAS_MES' AND "ValidFrom" = TIMESTAMPTZ '2026-01-01 00:00:00+00' AND "Value" = 210;
                UPDATE dbo."PAY_ConceptDefinitions" c
                SET "UnitFactor" = v.viejo, "UpdatedBy" = 'system:seed', "UpdatedAt" = (now() at time zone 'utc')
                FROM (VALUES ('RECARGO_DOMINICAL', 0.80, 0.90), ('HEX_DOM_DIURNA', 2.05, 2.15), ('HEX_DOM_NOCTURNA', 2.55, 2.65)) AS v(code, viejo, nuevo)
                WHERE c."Code" = v.code AND c."ValidFrom" = TIMESTAMPTZ '2026-01-01 00:00:00+00' AND c."Origin" = 0 AND c."UnitFactor" = v.nuevo;
                """);

        }
    }
}
