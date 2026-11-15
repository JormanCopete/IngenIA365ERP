using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace IngenIA365ERP.Persistence.Migrations.PostgreSql.Application
{
    /// <inheritdoc />
    public partial class CanalDigital : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CDG_ChannelEnablements",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EnabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    DisabledReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    DisabledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChangeSequence = table.Column<long>(type: "bigint", nullable: false),
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
                    table.PrimaryKey("PK_CDG_ChannelEnablements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CDG_ChannelEnablements_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CDG_ChannelSettings",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ChannelCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LogoAttachmentPublicId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    SecondaryColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    SupportPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SupportEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SupportHours = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PaymentSessionMinutes = table.Column<int>(type: "integer", nullable: false),
                    RequireOtpToPay = table.Column<bool>(type: "boolean", nullable: false),
                    InactivityMinutes = table.Column<int>(type: "integer", nullable: false),
                    EnablementPolicy = table.Column<int>(type: "integer", nullable: false),
                    AllowNonAssociates = table.Column<bool>(type: "boolean", nullable: false),
                    SmsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DueReminderDays = table.Column<int>(type: "integer", nullable: false),
                    SurplusPolicy = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("PK_CDG_ChannelSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CDG_ContactUpdates",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    Field = table.Column<int>(type: "integer", nullable: false),
                    NewValue = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedBy = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_CDG_ContactUpdates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CDG_ContactUpdates_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CDG_LegalTexts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_CDG_LegalTexts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CDG_PaymentDestinations",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Gateway = table.Column<int>(type: "integer", nullable: false),
                    SitePublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    CollectionAgreementId = table.Column<int>(type: "integer", nullable: false),
                    OfferPse = table.Column<bool>(type: "boolean", nullable: false),
                    OfferCard = table.Column<bool>(type: "boolean", nullable: false),
                    ReversalCutoffLocalTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CredentialsStatus = table.Column<int>(type: "integer", nullable: false),
                    CredentialsCheckedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LoginLast4 = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_CDG_PaymentDestinations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CDG_PaymentDestinations_RCD_CollectionAgreements_Collection~",
                        column: x => x.CollectionAgreementId,
                        principalSchema: "dbo",
                        principalTable: "RCD_CollectionAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CDG_PaymentNotices",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Reference = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PersonId = table.Column<int>(type: "integer", nullable: false),
                    DestinationId = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    GatewayRequestId = table.Column<long>(type: "bigint", nullable: false),
                    InternalReference = table.Column<long>(type: "bigint", nullable: true),
                    Authorization = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    GatewayReceipt = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Franchise = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    PaymentMethodName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    IssuerName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayerIp = table.Column<string>(type: "character varying(46)", maxLength: 46, nullable: true),
                    PayerUserAgent = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ChannelAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    OutcomeReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReversalReceiptId = table.Column<long>(type: "bigint", nullable: true),
                    ReversalReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
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
                    table.PrimaryKey("PK_CDG_PaymentNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CDG_PaymentNotices_CDG_PaymentDestinations_DestinationId",
                        column: x => x.DestinationId,
                        principalSchema: "dbo",
                        principalTable: "CDG_PaymentDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CDG_PaymentNotices_COR_People_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "dbo",
                        principalTable: "COR_People",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CDG_PaymentNotices_RCD_Receipts_ReceiptId",
                        column: x => x.ReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CDG_PaymentNotices_RCD_Receipts_ReversalReceiptId",
                        column: x => x.ReversalReceiptId,
                        principalSchema: "dbo",
                        principalTable: "RCD_Receipts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CDG_PublishedConcepts",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    LineCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DestinationId = table.Column<int>(type: "integer", nullable: true),
                    AllowDue = table.Column<bool>(type: "boolean", nullable: false),
                    AllowPartial = table.Column<bool>(type: "boolean", nullable: false),
                    PartialMinimum = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AllowPrepayment = table.Column<bool>(type: "boolean", nullable: false),
                    AllowPayoff = table.Column<bool>(type: "boolean", nullable: false),
                    CanCombine = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultPrepaymentChoice = table.Column<int>(type: "integer", nullable: true),
                    PublishStatement = table.Column<bool>(type: "boolean", nullable: false),
                    PublishCertificate = table.Column<bool>(type: "boolean", nullable: false),
                    PublishTaxCertificate = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_CDG_PublishedConcepts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CDG_PublishedConcepts_CDG_PaymentDestinations_DestinationId",
                        column: x => x.DestinationId,
                        principalSchema: "dbo",
                        principalTable: "CDG_PaymentDestinations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CDG_PaymentNoticeItems",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PaymentNoticeId = table.Column<int>(type: "integer", nullable: false),
                    ConceptKind = table.Column<int>(type: "integer", nullable: false),
                    ProductPublicId = table.Column<Guid>(type: "uuid", nullable: false),
                    Modality = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PrepaymentChoice = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("PK_CDG_PaymentNoticeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CDG_PaymentNoticeItems_CDG_PaymentNotices_PaymentNoticeId",
                        column: x => x.PaymentNoticeId,
                        principalSchema: "dbo",
                        principalTable: "CDG_PaymentNotices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CDG_ChannelEnablements_ChangeSequence",
                schema: "dbo",
                table: "CDG_ChannelEnablements",
                column: "ChangeSequence");

            migrationBuilder.CreateIndex(
                name: "UK_CDG_ChannelEnablements_Person",
                schema: "dbo",
                table: "CDG_ChannelEnablements",
                column: "PersonId",
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_CDG_ChannelEnablements_PublicId",
                schema: "dbo",
                table: "CDG_ChannelEnablements",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_CDG_ChannelSettings_PublicId",
                schema: "dbo",
                table: "CDG_ChannelSettings",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_CDG_ChannelSettings_Version",
                schema: "dbo",
                table: "CDG_ChannelSettings",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_CDG_ContactUpdates_Person_Field_Reported",
                schema: "dbo",
                table: "CDG_ContactUpdates",
                columns: new[] { "PersonId", "Field", "ReportedAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_CDG_ContactUpdates_PublicId",
                schema: "dbo",
                table: "CDG_ContactUpdates",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_CDG_LegalTexts_Kind_Version",
                schema: "dbo",
                table: "CDG_LegalTexts",
                columns: new[] { "Kind", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_CDG_LegalTexts_PublicId",
                schema: "dbo",
                table: "CDG_LegalTexts",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDG_PaymentDestinations_CollectionAgreementId",
                schema: "dbo",
                table: "CDG_PaymentDestinations",
                column: "CollectionAgreementId");

            migrationBuilder.CreateIndex(
                name: "UK_CDG_PaymentDestinations_PublicId",
                schema: "dbo",
                table: "CDG_PaymentDestinations",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_CDG_PaymentDestinations_SitePublicId",
                schema: "dbo",
                table: "CDG_PaymentDestinations",
                column: "SitePublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDG_PaymentNoticeItems_Notice",
                schema: "dbo",
                table: "CDG_PaymentNoticeItems",
                column: "PaymentNoticeId");

            migrationBuilder.CreateIndex(
                name: "UK_CDG_PaymentNoticeItems_PublicId",
                schema: "dbo",
                table: "CDG_PaymentNoticeItems",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDG_PaymentNotices_DestinationId",
                schema: "dbo",
                table: "CDG_PaymentNotices",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "IX_CDG_PaymentNotices_Person_Approved",
                schema: "dbo",
                table: "CDG_PaymentNotices",
                columns: new[] { "PersonId", "ApprovedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_CDG_PaymentNotices_ReceiptId",
                schema: "dbo",
                table: "CDG_PaymentNotices",
                column: "ReceiptId");

            migrationBuilder.CreateIndex(
                name: "IX_CDG_PaymentNotices_ReversalReceiptId",
                schema: "dbo",
                table: "CDG_PaymentNotices",
                column: "ReversalReceiptId");

            migrationBuilder.CreateIndex(
                name: "UK_CDG_PaymentNotices_PublicId",
                schema: "dbo",
                table: "CDG_PaymentNotices",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UK_CDG_PaymentNotices_Reference",
                schema: "dbo",
                table: "CDG_PaymentNotices",
                column: "Reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CDG_PublishedConcepts_DestinationId",
                schema: "dbo",
                table: "CDG_PublishedConcepts",
                column: "DestinationId");

            migrationBuilder.CreateIndex(
                name: "UK_CDG_PublishedConcepts_Kind_Line",
                schema: "dbo",
                table: "CDG_PublishedConcepts",
                columns: new[] { "Kind", "LineCode" },
                unique: true,
                filter: "\"IsDeleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "UK_CDG_PublishedConcepts_PublicId",
                schema: "dbo",
                table: "CDG_PublishedConcepts",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CDG_ChannelEnablements",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDG_ChannelSettings",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDG_ContactUpdates",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDG_LegalTexts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDG_PaymentNoticeItems",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDG_PublishedConcepts",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDG_PaymentNotices",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "CDG_PaymentDestinations",
                schema: "dbo");
        }
    }
}
