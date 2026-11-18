using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class ServiciosDeVehiculoEnVentas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_TRN_ServiceCharges_Vehicle_Service_Period",
                schema: "dbo",
                table: "TRN_ServiceCharges");

            migrationBuilder.AlterColumn<int>(
                name: "BillingRunId",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<short>(
                name: "Origin",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPaidOff",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ItemReference",
                schema: "dbo",
                table: "INV_DocumentLines",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UK_TRN_ServiceCharges_SaleLine",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                column: "InvoiceLinePublicId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Origin\" = 1");

            migrationBuilder.CreateIndex(
                name: "UK_TRN_ServiceCharges_Vehicle_Service_Period",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                columns: new[] { "VehicleId", "ServiceId", "Period" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Status\" <> 3 AND \"Origin\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_TRN_ServiceCharges_SaleLine",
                schema: "dbo",
                table: "TRN_ServiceCharges");

            migrationBuilder.DropIndex(
                name: "UK_TRN_ServiceCharges_Vehicle_Service_Period",
                schema: "dbo",
                table: "TRN_ServiceCharges");

            migrationBuilder.DropColumn(
                name: "Origin",
                schema: "dbo",
                table: "TRN_ServiceCharges");

            migrationBuilder.DropColumn(
                name: "RequiresPaidOff",
                schema: "dbo",
                table: "TRN_ServiceCharges");

            migrationBuilder.DropColumn(
                name: "ItemReference",
                schema: "dbo",
                table: "INV_DocumentLines");

            migrationBuilder.AlterColumn<int>(
                name: "BillingRunId",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UK_TRN_ServiceCharges_Vehicle_Service_Period",
                schema: "dbo",
                table: "TRN_ServiceCharges",
                columns: new[] { "VehicleId", "ServiceId", "Period" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE AND \"Status\" <> 3");
        }
    }
}
