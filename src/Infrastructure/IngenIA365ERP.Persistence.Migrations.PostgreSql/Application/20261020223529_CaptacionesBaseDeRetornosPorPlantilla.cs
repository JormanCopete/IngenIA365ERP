using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CaptacionesBaseDeRetornosPorPlantilla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LND_AssemblyReturnBases",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AssemblyDistributionId = table.Column<int>(type: "integer", nullable: false),
                    LoadVersion = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Concept = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_LND_AssemblyReturnBases", x => x.Id);
                    table.CheckConstraint("CK_LND_AssemblyReturnBases_Amount", "\"BaseAmount\" >= 0");
                    table.ForeignKey(
                        name: "FK_LND_AssemblyReturnBases_Distribution",
                        column: x => x.AssemblyDistributionId,
                        principalSchema: "dbo",
                        principalTable: "LND_AssemblyDistributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LND_AssemblyReturnBases_Person",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LND_AssemblyReturnBases_PersonId",
                schema: "dbo",
                table: "LND_AssemblyReturnBases",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "UK_LND_AssemblyReturnBases_PublicId",
                schema: "dbo",
                table: "LND_AssemblyReturnBases",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_LND_AssemblyReturnBases_Version_Person",
                schema: "dbo",
                table: "LND_AssemblyReturnBases",
                columns: new[] { "AssemblyDistributionId", "LoadVersion", "PersonId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LND_AssemblyReturnBases",
                schema: "dbo");
        }
    }
}
