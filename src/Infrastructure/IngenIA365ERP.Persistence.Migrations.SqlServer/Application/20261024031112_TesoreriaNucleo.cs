using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActivatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ResultWithThirdPerson = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
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
                    table.PrimaryKey("PK_TES_Setup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TES_UserBankAccountScopes",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    BankAccountId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_TES_UserBankAccountScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_UserBankAccountScopes_SEC_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_TES_Setup_PublicId",
                schema: "dbo",
                table: "TES_Setup",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_UserBankAccountScopes_PublicId",
                schema: "dbo",
                table: "TES_UserBankAccountScopes",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_UserBankAccountScopes_UserId_BankAccountId",
                schema: "dbo",
                table: "TES_UserBankAccountScopes",
                columns: new[] { "UserId", "BankAccountId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            // Feature 015 (T0050; data-model §9.1): la fila única de TES_Setup, con Id = 1 y sin activar. Idempotente. Nivel 1 del cerrojo de Tesorería.
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [dbo].[TES_Setup] ON;
INSERT INTO [dbo].[TES_Setup] ([Id], [PublicId], [Status], [ResultWithThirdPerson], [IsDeleted], [CreatedAt], [CreatedBy])
SELECT 1, NEWID(), 1, 0, 0, SYSUTCDATETIME(), N'SYSTEM'
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[TES_Setup]);
SET IDENTITY_INSERT [dbo].[TES_Setup] OFF;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TES_Setup",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_UserBankAccountScopes",
                schema: "dbo");
        }
    }
}
