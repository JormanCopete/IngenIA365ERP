using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class RecaudoYTesoreriaPuestaEnMarcha : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOpeningLoad",
                schema: "dbo",
                table: "TES_CashRemittances",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "OpeningBalance",
                schema: "dbo",
                table: "TES_BankAccounts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "OpeningBalanceOn",
                schema: "dbo",
                table: "TES_BankAccounts",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpeningLoad",
                schema: "dbo",
                table: "RCD_ReceivedChecks",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "RCD_OpeningSquares",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalLoaded = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalLedger = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDifference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeadlineOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MinutesAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefinitiveSquareId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RCD_OpeningSquares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_OpeningSquares_RCD_OpeningSquares_DefinitiveSquareId",
                        column: x => x.DefinitiveSquareId,
                        principalSchema: "dbo",
                        principalTable: "RCD_OpeningSquares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_OpeningSquares_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_OpeningSquares_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_ReferenceFigures",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Concept = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Dimension = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LoadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LoadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImportBatchPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RCD_ReferenceFigures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_ReferenceFigures_SEC_Users_LoadedByUserId",
                        column: x => x.LoadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_OpeningSquares",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalLoaded = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalLedger = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalDifference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RegisteredByUserId = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DeadlineOn = table.Column<DateOnly>(type: "date", nullable: true),
                    MinutesAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefinitiveSquareId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TES_OpeningSquares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_OpeningSquares_SEC_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_OpeningSquares_SEC_Users_RegisteredByUserId",
                        column: x => x.RegisteredByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TES_OpeningSquares_TES_OpeningSquares_DefinitiveSquareId",
                        column: x => x.DefinitiveSquareId,
                        principalSchema: "dbo",
                        principalTable: "TES_OpeningSquares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_ReferenceFigures",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Concept = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Dimension = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LoadedByUserId = table.Column<int>(type: "integer", nullable: false),
                    LoadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ImportBatchPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceFileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TES_ReferenceFigures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_ReferenceFigures_SEC_Users_LoadedByUserId",
                        column: x => x.LoadedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RCD_OpeningSquareLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OpeningSquareId = table.Column<int>(type: "integer", nullable: false),
                    Concept = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Dimension = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DimensionLabel = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Loaded = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Ledger = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsComparable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RCD_OpeningSquareLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_OpeningSquareLines_RCD_OpeningSquares_OpeningSquareId",
                        column: x => x.OpeningSquareId,
                        principalSchema: "dbo",
                        principalTable: "RCD_OpeningSquares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TES_OpeningSquareLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    PublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OpeningSquareId = table.Column<int>(type: "integer", nullable: false),
                    Concept = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Dimension = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    DimensionLabel = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Loaded = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Ledger = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsComparable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TES_OpeningSquareLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TES_OpeningSquareLines_TES_OpeningSquares_OpeningSquareId",
                        column: x => x.OpeningSquareId,
                        principalSchema: "dbo",
                        principalTable: "TES_OpeningSquares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TES_Activations_OpeningSquareId",
                schema: "dbo",
                table: "TES_Activations",
                column: "OpeningSquareId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_Activations_OpeningSquareId",
                schema: "dbo",
                table: "RCD_Activations",
                column: "OpeningSquareId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_OpeningSquareLines_PublicId",
                schema: "dbo",
                table: "RCD_OpeningSquareLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_RCD_OpeningSquareLines_Square_Concept_Dimension",
                schema: "dbo",
                table: "RCD_OpeningSquareLines",
                columns: new[] { "OpeningSquareId", "Concept", "Dimension" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_OpeningSquares_ApprovedByUserId",
                schema: "dbo",
                table: "RCD_OpeningSquares",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_OpeningSquares_DefinitiveSquareId",
                schema: "dbo",
                table: "RCD_OpeningSquares",
                column: "DefinitiveSquareId");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_OpeningSquares_Kind_Status",
                schema: "dbo",
                table: "RCD_OpeningSquares",
                columns: new[] { "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_OpeningSquares_RegisteredByUserId",
                schema: "dbo",
                table: "RCD_OpeningSquares",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_OpeningSquares_PublicId",
                schema: "dbo",
                table: "RCD_OpeningSquares",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReferenceFigures_Cutoff_Concept_Dimension",
                schema: "dbo",
                table: "RCD_ReferenceFigures",
                columns: new[] { "CutoffDate", "Concept", "Dimension" });

            migrationBuilder.CreateIndex(
                name: "IX_RCD_ReferenceFigures_LoadedByUserId",
                schema: "dbo",
                table: "RCD_ReferenceFigures",
                column: "LoadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_ReferenceFigures_PublicId",
                schema: "dbo",
                table: "RCD_ReferenceFigures",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_OpeningSquareLines_PublicId",
                schema: "dbo",
                table: "TES_OpeningSquareLines",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_TES_OpeningSquareLines_Square_Concept_Dimension",
                schema: "dbo",
                table: "TES_OpeningSquareLines",
                columns: new[] { "OpeningSquareId", "Concept", "Dimension" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_OpeningSquares_ApprovedByUserId",
                schema: "dbo",
                table: "TES_OpeningSquares",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_OpeningSquares_DefinitiveSquareId",
                schema: "dbo",
                table: "TES_OpeningSquares",
                column: "DefinitiveSquareId");

            migrationBuilder.CreateIndex(
                name: "IX_TES_OpeningSquares_Kind_Status",
                schema: "dbo",
                table: "TES_OpeningSquares",
                columns: new[] { "Kind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_OpeningSquares_RegisteredByUserId",
                schema: "dbo",
                table: "TES_OpeningSquares",
                column: "RegisteredByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_OpeningSquares_PublicId",
                schema: "dbo",
                table: "TES_OpeningSquares",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TES_ReferenceFigures_Cutoff_Concept_Dimension",
                schema: "dbo",
                table: "TES_ReferenceFigures",
                columns: new[] { "CutoffDate", "Concept", "Dimension" });

            migrationBuilder.CreateIndex(
                name: "IX_TES_ReferenceFigures_LoadedByUserId",
                schema: "dbo",
                table: "TES_ReferenceFigures",
                column: "LoadedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_TES_ReferenceFigures_PublicId",
                schema: "dbo",
                table: "TES_ReferenceFigures",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_RCD_Activations_RCD_OpeningSquares_OpeningSquareId",
                schema: "dbo",
                table: "RCD_Activations",
                column: "OpeningSquareId",
                principalSchema: "dbo",
                principalTable: "RCD_OpeningSquares",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TES_Activations_TES_OpeningSquares_OpeningSquareId",
                schema: "dbo",
                table: "TES_Activations",
                column: "OpeningSquareId",
                principalSchema: "dbo",
                principalTable: "TES_OpeningSquares",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RCD_Activations_RCD_OpeningSquares_OpeningSquareId",
                schema: "dbo",
                table: "RCD_Activations");

            migrationBuilder.DropForeignKey(
                name: "FK_TES_Activations_TES_OpeningSquares_OpeningSquareId",
                schema: "dbo",
                table: "TES_Activations");

            migrationBuilder.DropTable(
                name: "RCD_OpeningSquareLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_ReferenceFigures",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_OpeningSquareLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_ReferenceFigures",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "RCD_OpeningSquares",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TES_OpeningSquares",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_TES_Activations_OpeningSquareId",
                schema: "dbo",
                table: "TES_Activations");

            migrationBuilder.DropIndex(
                name: "IX_RCD_Activations_OpeningSquareId",
                schema: "dbo",
                table: "RCD_Activations");

            migrationBuilder.DropColumn(
                name: "IsOpeningLoad",
                schema: "dbo",
                table: "TES_CashRemittances");

            migrationBuilder.DropColumn(
                name: "OpeningBalance",
                schema: "dbo",
                table: "TES_BankAccounts");

            migrationBuilder.DropColumn(
                name: "OpeningBalanceOn",
                schema: "dbo",
                table: "TES_BankAccounts");

            migrationBuilder.DropColumn(
                name: "IsOpeningLoad",
                schema: "dbo",
                table: "RCD_ReceivedChecks");
        }
    }
}
