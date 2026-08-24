using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Admin
{
    /// <inheritdoc />
    public partial class AprovisionamientoPorCooperativa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuditDatabaseName",
                schema: "dbo",
                table: "ADM_Tenants",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MigrationsVersion",
                schema: "dbo",
                table: "ADM_Tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvisioningError",
                schema: "dbo",
                table: "ADM_Tenants",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProvisioningState",
                schema: "dbo",
                table: "ADM_Tenants",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<int>(
                name: "RedisDbIndex",
                schema: "dbo",
                table: "ADM_Tenants",
                type: "int",
                nullable: true);

            // Relleno: la base de cada cooperativa se llama como su esquema. Va ANTES
            // del indice unico, que fallaria si dos filas quedaran con el mismo valor
            // — no puede pasar, porque SchemaName ya es unico.
            //
            // ProvisioningState se queda en 'Pending' a proposito: estas cooperativas
            // tienen su ESQUEMA aprovisionado, no su BASE. Marcarlas Ready mentiria.
            migrationBuilder.Sql(
                "UPDATE [dbo].[ADM_Tenants] SET [DatabaseName] = [SchemaName] " +
                "WHERE [DatabaseName] IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_ADM_Tenants_DatabaseName",
                schema: "dbo",
                table: "ADM_Tenants",
                column: "DatabaseName",
                unique: true,
                filter: "[DatabaseName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ADM_Tenants_DatabaseName",
                schema: "dbo",
                table: "ADM_Tenants");

            migrationBuilder.DropColumn(
                name: "AuditDatabaseName",
                schema: "dbo",
                table: "ADM_Tenants");

            migrationBuilder.DropColumn(
                name: "MigrationsVersion",
                schema: "dbo",
                table: "ADM_Tenants");

            migrationBuilder.DropColumn(
                name: "ProvisioningError",
                schema: "dbo",
                table: "ADM_Tenants");

            migrationBuilder.DropColumn(
                name: "ProvisioningState",
                schema: "dbo",
                table: "ADM_Tenants");

            migrationBuilder.DropColumn(
                name: "RedisDbIndex",
                schema: "dbo",
                table: "ADM_Tenants");
        }
    }
}
