using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class EdicionDeSolicitudRadicada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EditsAfterSubmission",
                schema: "dbo",
                table: "LND_CreditApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "LND_CreditApplicationEdits",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplicationId = table.Column<int>(type: "int", nullable: false),
                    StatusAtEdit = table.Column<int>(type: "int", nullable: false),
                    EditedByUserId = table.Column<int>(type: "int", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Changes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LevelBeforeId = table.Column<int>(type: "int", nullable: true),
                    LevelAfterId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_LND_CreditApplicationEdits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationEdits_LND_AttributionLevels_LevelAfterId",
                        column: x => x.LevelAfterId,
                        principalSchema: "dbo",
                        principalTable: "LND_AttributionLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationEdits_LND_AttributionLevels_LevelBeforeId",
                        column: x => x.LevelBeforeId,
                        principalSchema: "dbo",
                        principalTable: "LND_AttributionLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationEdits_LND_CreditApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "dbo",
                        principalTable: "LND_CreditApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_CreditApplicationEdits_SEC_Users_EditedByUserId",
                        column: x => x.EditedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationEdits_App_EditedAt",
                schema: "dbo",
                table: "LND_CreditApplicationEdits",
                columns: new[] { "ApplicationId", "EditedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationEdits_EditedByUserId",
                schema: "dbo",
                table: "LND_CreditApplicationEdits",
                column: "EditedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationEdits_LevelAfterId",
                schema: "dbo",
                table: "LND_CreditApplicationEdits",
                column: "LevelAfterId");

            migrationBuilder.CreateIndex(
                name: "IX_LND_CreditApplicationEdits_LevelBeforeId",
                schema: "dbo",
                table: "LND_CreditApplicationEdits",
                column: "LevelBeforeId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_CreditApplicationEdits_PublicId",
                schema: "dbo",
                table: "LND_CreditApplicationEdits",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LND_CreditApplicationEdits",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "EditsAfterSubmission",
                schema: "dbo",
                table: "LND_CreditApplications");
        }
    }
}
