using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
{
    /// <inheritdoc />
    public partial class IntegracionContableDeRecaudoYTesoreria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ACC_OwnBankAccountLinks",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OwnBankAccountPublicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChartOfAccountId = table.Column<int>(type: "int", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
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
                    table.PrimaryKey("PK_ACC_OwnBankAccountLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ACC_OwnBankAccountLinks_ACC_ChartOfAccounts_ChartOfAccountId",
                        column: x => x.ChartOfAccountId,
                        principalSchema: "dbo",
                        principalTable: "ACC_ChartOfAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_ACC_OwnBankAccountLinks_ChartOfAccountId_Vigente",
                schema: "dbo",
                table: "ACC_OwnBankAccountLinks",
                column: "ChartOfAccountId",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_OwnBankAccountLinks_OwnBankAccountPublicId_Vigente",
                schema: "dbo",
                table: "ACC_OwnBankAccountLinks",
                column: "OwnBankAccountPublicId",
                unique: true,
                filter: "[ValidTo] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "UK_ACC_OwnBankAccountLinks_PublicId",
                schema: "dbo",
                table: "ACC_OwnBankAccountLinks",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ACC_OwnBankAccountLinks",
                schema: "dbo");
        }
    }
}
