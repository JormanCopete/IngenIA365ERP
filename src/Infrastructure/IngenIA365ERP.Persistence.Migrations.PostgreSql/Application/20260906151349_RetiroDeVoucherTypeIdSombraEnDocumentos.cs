using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <summary>
    /// MIGRACION-DESTRUCTIVA-APROBADA
    ///
    /// Retira la columna sombra <c>VoucherTypeId</c> de <c>ACC_Documents</c>. No es un dato:
    /// era una segunda relacion que EF creo por convencion porque el mapeo de
    /// <c>VoucherType.Documents</c> no enlazaba la navegacion <c>AccountingDocument.VoucherType</c>.
    /// El tipo de comprobante de cada documento sigue en <c>VoucherTypeCode</c>, la clave
    /// alterna que la aplicacion siempre escribio; la columna sombra solo podia quedar en 0 (el
    /// INSERT fallaba por su FK) o con el mismo Id que el codigo ya identifica. No hay nada que
    /// preservar, y por eso <c>Down</c> la reconstruye desde el codigo antes de volver a poner la FK.
    ///
    /// Principio XII: antes de aplicarla en un ambiente, backup de la base de cada cooperativa
    /// y segundo revisor. Referencias (PDN, 2026-09-07): backup CNPG <c>erp-db-pre-nomina-mfa-20260907</c>
    /// a S3 con archivado continuo (PITR), mas <c>pg_dump -Fc</c> de <c>ingenia365erp</c> y
    /// <c>ingenia365erp_admin</c> en el nodo (<c>/root/respaldos/</c>) / revisor: Jorman Copete, que
    /// autorizo el despliegue a produccion. DEV y QA la recibieron por AutoMigrate el 2026-09-06.
    /// Lo destapo la prueba e2e <c>CreateDocumentEndpointTests</c> el 2026-09-06: crear un
    /// comprobante contable manual por la API fallaba con
    /// <c>FK_ACC_Documents_ACC_VoucherTypes_VoucherTypeId</c>.
    /// </summary>
    public partial class RetiroDeVoucherTypeIdSombraEnDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ACC_Documents_ACC_VoucherTypes_VoucherTypeId",
                schema: "dbo",
                table: "ACC_Documents");

            migrationBuilder.DropIndex(
                name: "IX_ACC_Documents_VoucherTypeId",
                schema: "dbo",
                table: "ACC_Documents");

            migrationBuilder.DropColumn(
                name: "VoucherTypeId",
                schema: "dbo",
                table: "ACC_Documents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VoucherTypeId",
                schema: "dbo",
                table: "ACC_Documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ACC_Documents_VoucherTypeId",
                schema: "dbo",
                table: "ACC_Documents",
                column: "VoucherTypeId");

            // Reconstruir el valor desde el codigo antes de la FK: con el 0 por defecto la
            // restriccion fallaria en cualquier base con documentos.
            migrationBuilder.Sql("UPDATE dbo.\"ACC_Documents\" AS d SET \"VoucherTypeId\" = v.\"Id\" FROM dbo.\"ACC_VoucherTypes\" AS v WHERE v.\"Code\" = d.\"VoucherTypeCode\";");

            migrationBuilder.AddForeignKey(
                name: "FK_ACC_Documents_ACC_VoucherTypes_VoucherTypeId",
                schema: "dbo",
                table: "ACC_Documents",
                column: "VoucherTypeId",
                principalSchema: "dbo",
                principalTable: "ACC_VoucherTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
