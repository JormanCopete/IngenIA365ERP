using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class ReaperturaDelCierreDelDia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_RCD_DayCloses_CollectionPointDayId",
                schema: "dbo",
                table: "RCD_DayCloses");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                schema: "dbo",
                table: "RCD_DayCloses",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "RCD_DayCloseReopenings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayCloseId = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ReopenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReopenedByUserId = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_RCD_DayCloseReopenings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RCD_DayCloseReopenings_RCD_DayCloses_DayCloseId",
                        column: x => x.DayCloseId,
                        principalSchema: "dbo",
                        principalTable: "RCD_DayCloses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RCD_DayCloseReopenings_SEC_Users_ReopenedByUserId",
                        column: x => x.ReopenedByUserId,
                        principalSchema: "dbo",
                        principalTable: "SEC_Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DayCloses_CollectionPointDayId_Version",
                schema: "dbo",
                table: "RCD_DayCloses",
                columns: new[] { "CollectionPointDayId", "Version" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_RCD_DayCloseReopenings_ReopenedByUserId",
                schema: "dbo",
                table: "RCD_DayCloseReopenings",
                column: "ReopenedByUserId");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DayCloseReopenings_DayCloseId",
                schema: "dbo",
                table: "RCD_DayCloseReopenings",
                column: "DayCloseId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DayCloseReopenings_PublicId",
                schema: "dbo",
                table: "RCD_DayCloseReopenings",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RCD_DayCloseReopenings",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "UK_RCD_DayCloses_CollectionPointDayId_Version",
                schema: "dbo",
                table: "RCD_DayCloses");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "dbo",
                table: "RCD_DayCloses");

            migrationBuilder.CreateIndex(
                name: "UK_RCD_DayCloses_CollectionPointDayId",
                schema: "dbo",
                table: "RCD_DayCloses",
                column: "CollectionPointDayId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");
        }
    }
}
