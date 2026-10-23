using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class TesoreriaNucleo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TES_Setup",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActivatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ResultWithThirdPerson = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("PK_TES_Setup", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "UK_TES_Setup_PublicId",
                schema: "dbo",
                table: "TES_Setup",
                column: "PublicId",
                unique: true);

            // Feature 015 (T0050; data-model §9.1): la fila única de TES_Setup, con Id = 1 y sin activar. Idempotente. Nivel 1 del cerrojo de Tesorería.
            migrationBuilder.Sql(@"
INSERT INTO dbo.""TES_Setup"" (""Id"", ""PublicId"", ""Status"", ""ResultWithThirdPerson"", ""IsDeleted"", ""CreatedAt"", ""CreatedBy"")
OVERRIDING SYSTEM VALUE
SELECT 1, gen_random_uuid(), 1, FALSE, FALSE, now(), 'SYSTEM'
WHERE NOT EXISTS (SELECT 1 FROM dbo.""TES_Setup"");
SELECT setval(pg_get_serial_sequence('dbo.""TES_Setup""', 'Id'), GREATEST((SELECT MAX(""Id"") FROM dbo.""TES_Setup""), 1));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TES_Setup",
                schema: "dbo");
        }
    }
}
