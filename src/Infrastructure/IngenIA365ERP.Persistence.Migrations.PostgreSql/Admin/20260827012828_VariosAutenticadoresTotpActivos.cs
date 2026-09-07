using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Admin
{
    /// <inheritdoc />
    public partial class VariosAutenticadoresTotpActivos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ADM_MfaCredentials_TotpActivo",
                schema: "dbo",
                table: "ADM_MfaCredentials");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_ADM_MfaCredentials_TotpActivo",
                schema: "dbo",
                table: "ADM_MfaCredentials",
                column: "CentralUserId",
                unique: true,
                filter: "\"CredentialType\" = 'Totp' AND \"IsDeleted\" = FALSE");
        }
    }
}
