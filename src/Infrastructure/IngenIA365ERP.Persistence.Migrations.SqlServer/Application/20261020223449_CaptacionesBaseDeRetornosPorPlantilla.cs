using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.SqlServer.Application
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssemblyDistributionId = table.Column<int>(type: "int", nullable: false),
                    LoadVersion = table.Column<int>(type: "int", nullable: false),
                    PersonId = table.Column<int>(type: "int", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Concept = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
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
                    table.PrimaryKey("PK_LND_AssemblyReturnBases", x => x.Id);
                    table.CheckConstraint("CK_LND_AssemblyReturnBases_Amount", "[BaseAmount] >= 0");
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
