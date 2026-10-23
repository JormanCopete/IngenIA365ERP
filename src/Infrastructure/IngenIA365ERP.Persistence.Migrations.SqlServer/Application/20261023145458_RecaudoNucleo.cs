using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActivatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CutoffTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ContractVersionObligations = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    ContractVersionAccounts = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    PublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
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
SET IDENTITY_INSERT [dbo].[RCD_Setup] ON;
INSERT INTO [dbo].[RCD_Setup] ([Id], [PublicId], [Status], [CutoffTime], [ContractVersionObligations], [ContractVersionAccounts], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT 1, NEWID(), 1, CAST('18:00' AS time), 1, 1, 0, SYSUTCDATETIME(), N'SYSTEM'
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[RCD_Setup]);
SET IDENTITY_INSERT [dbo].[RCD_Setup] OFF;
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
