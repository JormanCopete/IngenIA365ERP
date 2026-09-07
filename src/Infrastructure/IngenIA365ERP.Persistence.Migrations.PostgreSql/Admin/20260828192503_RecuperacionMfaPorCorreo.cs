using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Admin
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
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "EmailRecoveryDelayHours",
                schema: "dbo",
                table: "ADM_TenantMfaPolicies",
                type: "integer",
                nullable: false,
                defaultValue: 24);

            migrationBuilder.CreateTable(
                name: "MfaRecoveryRequests",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CentralUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    CancelTokenHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    EjecutableDesde = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EjecutadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CanceladaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    MotivoDeCancelacion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IpSolicitante = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
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
