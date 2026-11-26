using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CajerosDeLaCajaDelPuntoDeVenta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "INV_CashRegisterCashiers",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CashRegisterId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_INV_CashRegisterCashiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_INV_CashRegisterCashiers_INV_CashRegisters_CashRegisterId",
                        column: x => x.CashRegisterId,
                        principalSchema: "dbo",
                        principalTable: "INV_CashRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_INV_CashRegisterCashiers_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_INV_CashRegisterCashiers_UserId",
                schema: "dbo",
                table: "INV_CashRegisterCashiers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashRegisterCashiers_CashRegisterId_UserId_ValidFrom",
                schema: "dbo",
                table: "INV_CashRegisterCashiers",
                columns: new[] { "CashRegisterId", "UserId", "ValidFrom" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_INV_CashRegisterCashiers_PublicId",
                schema: "dbo",
                table: "INV_CashRegisterCashiers",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "INV_CashRegisterCashiers",
                schema: "dbo");
        }
    }
}
