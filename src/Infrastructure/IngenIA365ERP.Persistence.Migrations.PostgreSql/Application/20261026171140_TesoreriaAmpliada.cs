using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class TesoreriaAmpliada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TES_LegacyPendingLoads",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    LegacyReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    BankAccountId = table.Column<int>(type: "integer", nullable: true),
                    CheckNumber = table.Column<long>(type: "bigint", nullable: true),
                    BeneficiaryPersonId = table.Column<int>(type: "integer", nullable: true),
                    BeneficiaryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MigratedToPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    LoadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LoadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("PK_TES_LegacyPendingLoads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_LegacyPendingLoads_COR_People_BeneficiaryPersonId",
                        column: x => x.BeneficiaryPersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_LegacyPendingLoads_SEC_Users_LoadedByUserId",
                        column: x => x.LoadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_LegacyPendingLoads_TES_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "dbo",
                        principalTable: "TES_BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TES_LegacyPendingLoads_BankAccountId",
                schema: "dbo",
                table: "TES_LegacyPendingLoads",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_LegacyPendingLoads_BeneficiaryPersonId",
                schema: "dbo",
                table: "TES_LegacyPendingLoads",
                column: "BeneficiaryPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_LegacyPendingLoads_LoadedByUserId",
                schema: "dbo",
                table: "TES_LegacyPendingLoads",
                column: "LoadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_LegacyPendingLoads_Kind_Reference",
                schema: "dbo",
                table: "TES_LegacyPendingLoads",
                columns: new[] { "Kind", "LegacyReference" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_TES_LegacyPendingLoads_PublicId",
                schema: "dbo",
                table: "TES_LegacyPendingLoads",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TES_LegacyPendingLoads",
                schema: "dbo");
        }
    }
}
