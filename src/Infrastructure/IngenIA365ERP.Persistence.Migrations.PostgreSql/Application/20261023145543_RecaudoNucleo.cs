using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class RecaudoNucleo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RCD_PayerControls",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RCD_PayerControls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_PayerControls_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_Setup",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActivatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CutoffTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ContractVersionObligations = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    ContractVersionAccounts = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
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
                    table.PrimaryKey("PK_RCD_Setup", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PayerControls_PersonId",
                schema: "dbo",
                table: "RCD_PayerControls",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_PayerControls_PublicId",
                schema: "dbo",
                table: "RCD_PayerControls",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_Setup_PublicId",
                schema: "dbo",
                table: "RCD_Setup",
                column: "PublicId",
                unique: true);

            // Feature 015 (T0050; data-model §1.1): la fila única de RCD_Setup, con Id = 1, sin activar, la hora de corte por defecto (18:00,
            // Recaudo.HoraDeCorte) y las versiones 1 de los contratos. Idempotente: no hace nada si la fila ya existe. Es el nivel 1 del cerrojo.
            migrationBuilder.Sql(@"
INSERT INTO dbo.""RCD_Setup"" (""Id"", ""PublicId"", ""Status"", ""CutoffTime"", ""ContractVersionObligations"", ""ContractVersionAccounts"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
OVERRIDING SYSTEM VALUE
SELECT 1, gen_random_uuid(), 1, TIME '18:00', 1, 1, FALSE, now(), 'SYSTEM'
WHERE NOT EXISTS (SELECT 1 FROM dbo.""RCD_Setup"");
SELECT setval(pg_get_serial_sequence('dbo.""RCD_Setup""', 'Id'), GREATEST((SELECT MAX(""Id"") FROM dbo.""RCD_Setup""), 1));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RCD_PayerControls",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_Setup",
                schema: "dbo");
        }
    }
}
