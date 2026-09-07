using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Admin
{
    /// <inheritdoc />
    public partial class RecuperacionMfaPorCorreo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowEmailRecovery",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EmailRecoveryDelayHours",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies",
                type: "int",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.CreateTable(
                name: "MfaRecoveryRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CentralUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CancelTokenHash = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    EjecutableDesde = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraEn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EjecutadaEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CanceladaEn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoDeCancelacion = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IpSolicitante = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaRecoveryRequests", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MfaRecoveryRequests",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "AllowEmailRecovery",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies");

            migrationBuilder.DropColumn(
                name: "EmailRecoveryDelayHours",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies");
        }
    }
}
