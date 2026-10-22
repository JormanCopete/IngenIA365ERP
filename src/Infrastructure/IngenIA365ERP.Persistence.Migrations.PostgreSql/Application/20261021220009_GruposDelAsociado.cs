using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class GruposDelAsociado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssociateGroupId",
                schema: "dbo",
                table: "COR_Associates",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "COR_AssociateGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
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
                    table.PrimaryKey("PK_COR_AssociateGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_COR_Associates_AssociateGroupId",
                schema: "dbo",
                table: "COR_Associates",
                column: "AssociateGroupId");

            migrationBuilder.CreateIndex(
                name: "UK_COR_AssociateGroups_Code",
                schema: "dbo",
                table: "COR_AssociateGroups",
                column: "Code",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_COR_AssociateGroups_PublicId",
                schema: "dbo",
                table: "COR_AssociateGroups",
                column: "PublicId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_COR_Associates_COR_AssociateGroups_AssociateGroupId",
                schema: "dbo",
                table: "COR_Associates",
                column: "AssociateGroupId",
                principalSchema: "dbo",
                principalTable: "COR_AssociateGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_COR_Associates_COR_AssociateGroups_AssociateGroupId",
                schema: "dbo",
                table: "COR_Associates");

            migrationBuilder.DropTable(
                name: "COR_AssociateGroups",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_COR_Associates_AssociateGroupId",
                schema: "dbo",
                table: "COR_Associates");

            migrationBuilder.DropColumn(
                name: "AssociateGroupId",
                schema: "dbo",
                table: "COR_Associates");
        }
    }
}
