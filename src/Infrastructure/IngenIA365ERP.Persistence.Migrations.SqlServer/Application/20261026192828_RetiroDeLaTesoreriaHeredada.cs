using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
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
                IF NOT EXISTS (SELECT 1 FROM [dbo].[COR_SystemSettings]
                               WHERE [SettingKey] = N'TES.RetiroHeredado.Aprobado' AND [IsDeleted] = 0)
                BEGIN
                    DECLARE @tabla sysname, @filas bigint, @conFilas nvarchar(max), @sql nvarchar(max), @mensaje nvarchar(2048),
                            @cheques bigint = 0, @facturas bigint = 0;
                    -- Las tres tablas, contando tambien las filas dadas de baja (COUNT_BIG(*) no mira IsDeleted).
                    DECLARE tablas CURSOR LOCAL FAST_FORWARD FOR
                        SELECT t FROM (VALUES (N'TRS_Checks'), (N'TRS_Concepts'), (N'TRS_Invoices')) AS v(t);
                    OPEN tablas;
                    FETCH NEXT FROM tablas INTO @tabla;
                    WHILE @@FETCH_STATUS = 0
                    BEGIN
                        IF OBJECT_ID(N'[dbo].' + QUOTENAME(@tabla), N'U') IS NOT NULL
                        BEGIN
                            SET @sql = N'SELECT @n = COUNT_BIG(*) FROM [dbo].' + QUOTENAME(@tabla);
                            EXEC sp_executesql @sql, N'@n bigint OUTPUT', @n = @filas OUTPUT;
                            IF @filas > 0
                                SET @conFilas = CONCAT(@conFilas, CASE WHEN @conFilas IS NULL THEN N'' ELSE N', ' END,
                                                       @tabla, N' (', @filas, N' filas)');
                        END
                        FETCH NEXT FROM tablas INTO @tabla;
                    END
                    CLOSE tablas;
                    DEALLOCATE tablas;
                    IF @conFilas IS NOT NULL
                    BEGIN
                        -- Los pendientes que todavia no se cargaron con la plantilla tesoreria.pendientes-heredados (la referencia es el Id heredado).
                        IF OBJECT_ID(N'[dbo].[TES_LegacyPendingLoads]', N'U') IS NOT NULL
                        BEGIN
                            IF OBJECT_ID(N'[dbo].[TRS_Checks]', N'U') IS NOT NULL
                                EXEC sp_executesql N'SELECT @n = COUNT_BIG(*) FROM [dbo].[TRS_Checks] c WHERE c.[IsDeleted] = 0 AND c.[Status] = ''E'' AND NOT EXISTS (SELECT 1 FROM [dbo].[TES_LegacyPendingLoads] l WHERE l.[Kind] = 1 AND l.[IsDeleted] = 0 AND l.[LegacyReference] = CAST(c.[Id] AS nvarchar(60)))',
                                    N'@n bigint OUTPUT', @n = @cheques OUTPUT;
                            IF OBJECT_ID(N'[dbo].[TRS_Invoices]', N'U') IS NOT NULL
                                EXEC sp_executesql N'SELECT @n = COUNT_BIG(*) FROM [dbo].[TRS_Invoices] f WHERE f.[IsDeleted] = 0 AND f.[Status] = ''P'' AND NOT EXISTS (SELECT 1 FROM [dbo].[TES_LegacyPendingLoads] l WHERE l.[Kind] = 2 AND l.[IsDeleted] = 0 AND l.[LegacyReference] = CAST(f.[Id] AS nvarchar(60)))',
                                    N'@n bigint OUTPUT', @n = @facturas OUTPUT;
                        END
                        SET @mensaje = CONCAT(N'RetiroDeLaTesoreriaHeredada: la Tesoreria anterior tiene filas: ', @conFilas,
                            N'; pendientes sin cargar a la Tesoreria nueva: ', @cheques, N' cheques sin cobrar y ', @facturas,
                            N' facturas por pagar. La migracion borra TRS_Checks, TRS_Concepts y TRS_Invoices; solo se aplica sobre tablas vacias o con la aprobacion del dueno para esta cooperativa (fila COR_SystemSettings.SettingKey = ''TES.RetiroHeredado.Aprobado''), despues de la exportacion para archivo y la carga de los pendientes, previo respaldo y segundo revisor (feature 015, FR-161, Principio XII; runbook docs/operaciones/tesoreria-retiro-heredado.md).');
                        THROW 50016, @mensaje, 1;
                    END
                END
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
