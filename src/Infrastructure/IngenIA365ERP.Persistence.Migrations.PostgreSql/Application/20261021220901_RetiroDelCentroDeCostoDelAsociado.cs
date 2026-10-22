using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// Retiro de <c>COR_Associates.CostCenterId</c>: el asociado ya no se clasifica por centro de costo (contable) sino por su grupo.
    /// Destructiva con guarda (Principio XII): se niega si alguna fila (también dada de baja) tiene valor; la cabecera de
    /// aprobación va al principio de <c>Up()</c>. <c>Down()</c> recrea la columna vacía con su índice y su llave.
    /// </summary>
    public partial class RetiroDelCentroDeCostoDelAsociado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MIGRACION-DESTRUCTIVA-APROBADA (Principio XII; grupos del asociado, retiro de COR_Associates.CostCenterId).
            //   Qué borra: la columna CostCenterId de COR_Associates con su índice y su llave a COR_CostCenters. Ningún comando la escribía
            //     (la consulta de asociados sólo la leía) y el centro de costo del asociado se reemplaza por el grupo del asociado
            //     (COR_Associates.AssociateGroupId, migración GruposDelAsociado). El catálogo COR_CostCenters no se toca.
            //   Diagnóstico: (2026-10-21, sólo lectura por SSH) DEV (ingenia365erp) y QA (ingenia365erp y coop_prueba): COR_Associates tiene 0 filas, 0 con CostCenterId.
            //     Producción (erp-pdn, 2026-10-21, sólo lectura): ingenia365erp y cooflopal: COR_Associates tiene 0 filas y 0 con CostCenterId (cooflopal: 11 empleados, 0 centros de costo).
            //     La guarda de abajo lo comprueba sola (incluye filas dadas de baja).
            //   Respaldo: pg_dump -Fc de ingenia365erp, ingenia365erp_admin y cooflopal en /root/respaldos de erp-pdn (erp-pdn-<base>-20261021-pre-grupos-centro-empleado.dump, PGDMP).
            //   Segundo revisor: Jorman Copete (dueño), con su aprobación del 2026-10-21 para el release a producción.
            // La guarda no admite aprobación en la base: si alguna fila tiene valor, la migración se niega y no toca nada.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    filas bigint;
                BEGIN
                    IF EXISTS (SELECT 1 FROM information_schema.columns
                               WHERE table_schema = 'dbo' AND table_name = 'COR_Associates' AND column_name = 'CostCenterId') THEN
                        EXECUTE 'SELECT count(*) FROM dbo."COR_Associates" WHERE "CostCenterId" IS NOT NULL' INTO filas;
                        IF filas > 0 THEN
                            RAISE EXCEPTION 'RetiroDelCentroDeCostoDelAsociado: % asociado(s) tienen COR_Associates.CostCenterId. La migracion borra esa columna (el centro de costo del asociado se reemplazo por el grupo del asociado); revise esas filas con el duenio antes de aplicarla (Principio XII).', filas;
                        END IF;
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_COR_Associates_COR_CostCenters_CostCenterId",
                schema: "dbo",
                table: "COR_Associates");

            migrationBuilder.DropIndex(
                name: "IX_COR_Associates_CostCenterId",
                schema: "dbo",
                table: "COR_Associates");

            migrationBuilder.DropColumn(
                name: "CostCenterId",
                schema: "dbo",
                table: "COR_Associates");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CostCenterId",
                schema: "dbo",
                table: "COR_Associates",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_COR_Associates_CostCenterId",
                schema: "dbo",
                table: "COR_Associates",
                column: "CostCenterId");

            migrationBuilder.AddForeignKey(
                name: "FK_COR_Associates_COR_CostCenters_CostCenterId",
                schema: "dbo",
                table: "COR_Associates",
                column: "CostCenterId",
                principalSchema: "dbo",
                principalTable: "COR_CostCenters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
