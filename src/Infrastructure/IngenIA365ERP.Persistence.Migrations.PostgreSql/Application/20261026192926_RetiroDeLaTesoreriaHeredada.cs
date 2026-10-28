using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Feature 015 — Recaudo y Tesorería, después de T2: retiro de la Tesorería anterior (specs/015-recaudo-tesoreria/tasks.md fase 46, T0469 a T0479;
    /// data-model §24; FR-161; C3 = A).
    ///
    /// <para>
    /// Destructiva con guarda (Principio XII; la cabecera de aprobación, con el diagnóstico, el respaldo y el segundo revisor, va al principio de
    /// <c>Up()</c>). Borra las tres tablas de la Tesorería anterior —<c>TRS_Checks</c>, <c>TRS_Concepts</c> y <c>TRS_Invoices</c>—, sin llaves desde ninguna
    /// tabla viva (las de <c>TRS_Concepts</c> hacia el plan de cuentas se van con ella). Generada sobre el modelo sin sus tres entidades, en commit propio.
    /// </para>
    ///
    /// <para>
    /// Qué hace, en orden: (1) guarda: cuenta las filas —incluidas las dadas de baja— de las tres tablas (saltando las que no existan); si alguna tiene filas
    /// y la base no trae la fila viva <c>COR_SystemSettings.SettingKey = 'TES.RetiroHeredado.Aprobado'</c>, se niega nombrando cada tabla con su cantidad y
    /// cuántos pendientes (cheques girados sin cobrar, facturas por pagar) no se cargaron todavía a <c>TES_LegacyPendingLoads</c>, sin tocar nada
    /// (PostgreSQL <c>RAISE EXCEPTION</c>, SQL Server <c>THROW 50016</c>); (2) borra las tres tablas.
    /// </para>
    ///
    /// <para>
    /// <b>Irreversible</b>: <c>Down()</c> lanza. Lo borrado sólo vuelve desde el respaldo previo y la exportación para archivo (runbook
    /// docs/operaciones/tesoreria-retiro-heredado.md, «Restauración»).
    /// </para>
    /// </summary>
    public partial class RetiroDeLaTesoreriaHeredada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MIGRACION-DESTRUCTIVA-APROBADA (Principio XII; feature 015, fase 46, T0474; data-model §24; FR-161). Completada por el dueño, segundo revisor,
            // el 2026-10-28 (T0469, T0478, T0479):
            //   Diagnóstico: corrido en sólo lectura con specs/015-recaudo-tesoreria/diagnostico-tesoreria-heredada.sql el 2026-10-27/28. TRS_Checks,
            //     TRS_Concepts y TRS_Invoices tienen 0 filas en DEV ingenia365erp, QA coop_prueba e ingenia365erp, y PDN cooflopal e ingenia365erp.
            //   Exportación y carga de pendientes: no aplica (T0473): ninguna base tiene filas en la Tesorería anterior.
            //   Respaldo: OBLIGATORIO ANTES DE APLICAR. pg_dump -Fc de cada base de DEV y QA antes del despliegue a develop, con el sufijo -pre-f015.dump (lo toma
            //     el dueño); producción lleva el suyo, con el mismo sufijo, antes del release.
            //   Segundo revisor: Jorman Copete, aprobación escrita del 2026-10-28, registrada en docs/operaciones/tesoreria-retiro-heredado.md.
            // Con la guarda, sólo se pierden filas que el dueño aprobó perder (fila TES.RetiroHeredado.Aprobado de la cooperativa); con las tablas vacías no
            // hace falta esa fila.

            // (1) Guarda: sólo sobre la Tesorería anterior vacía, o con la aprobación del dueño en la base.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    tabla text;
                    filas bigint;
                    conFilas text;
                    cheques bigint := 0;
                    facturas bigint := 0;
                BEGIN
                    IF EXISTS (SELECT 1 FROM dbo."COR_SystemSettings"
                               WHERE "SettingKey" = 'TES.RetiroHeredado.Aprobado' AND "IsDeleted" = false) THEN
                        RETURN;
                    END IF;
                    -- Las tres tablas, contando tambien las filas dadas de baja (count(*) no mira IsDeleted).
                    FOREACH tabla IN ARRAY ARRAY['TRS_Checks', 'TRS_Concepts', 'TRS_Invoices'] LOOP
                        IF to_regclass(format('dbo.%I', tabla)) IS NOT NULL THEN
                            EXECUTE format('SELECT count(*) FROM dbo.%I', tabla) INTO filas;
                            IF filas > 0 THEN
                                conFilas := concat_ws(', ', conFilas, format('%s (%s filas)', tabla, filas));
                            END IF;
                        END IF;
                    END LOOP;
                    IF conFilas IS NOT NULL THEN
                        -- Los pendientes que todavia no se cargaron con la plantilla tesoreria.pendientes-heredados (la referencia es el Id heredado).
                        IF to_regclass('dbo."TES_LegacyPendingLoads"') IS NOT NULL THEN
                            IF to_regclass('dbo."TRS_Checks"') IS NOT NULL THEN
                                EXECUTE 'SELECT count(*) FROM dbo."TRS_Checks" c WHERE c."IsDeleted" = false AND c."Status" = ''E'' AND NOT EXISTS (SELECT 1 FROM dbo."TES_LegacyPendingLoads" l WHERE l."Kind" = 1 AND l."IsDeleted" = false AND l."LegacyReference" = c."Id"::text)' INTO cheques;
                            END IF;
                            IF to_regclass('dbo."TRS_Invoices"') IS NOT NULL THEN
                                EXECUTE 'SELECT count(*) FROM dbo."TRS_Invoices" f WHERE f."IsDeleted" = false AND f."Status" = ''P'' AND NOT EXISTS (SELECT 1 FROM dbo."TES_LegacyPendingLoads" l WHERE l."Kind" = 2 AND l."IsDeleted" = false AND l."LegacyReference" = f."Id"::text)' INTO facturas;
                            END IF;
                        END IF;
                        RAISE EXCEPTION 'RetiroDeLaTesoreriaHeredada: la Tesoreria anterior tiene filas: %; pendientes sin cargar a la Tesoreria nueva: % cheques sin cobrar y % facturas por pagar. La migracion borra TRS_Checks, TRS_Concepts y TRS_Invoices; solo se aplica sobre tablas vacias o con la aprobacion del dueno para esta cooperativa (fila COR_SystemSettings.SettingKey = ''TES.RetiroHeredado.Aprobado''), despues de la exportacion para archivo y la carga de los pendientes, previo respaldo y segundo revisor (feature 015, FR-161, Principio XII; runbook docs/operaciones/tesoreria-retiro-heredado.md).', conFilas, cheques, facturas;
                    END IF;
                END $$;
                """);

            // (2) Las tres tablas.
            migrationBuilder.DropTable(
                name: "TRS_Checks",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRS_Concepts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TRS_Invoices",
                schema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException(
                "RetiroDeLaTesoreriaHeredada es irreversible (feature 015, FR-161, Principio XII): lo borrado vuelve sólo desde el respaldo previo y la " +
                "exportación para archivo (docs/operaciones/tesoreria-retiro-heredado.md, «Restauración»).");
    }
}
