using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_AspNetUsers_OwnerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_OwnerId_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                table: "Invoices",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Existing accounts stay enabled.
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPlatformAdmin",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastLoginAt",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ImpersonatorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImpersonationSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ImpersonatorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TargetUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImpersonationSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Country = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DefaultCurrency = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimeZone = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StatusReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StatusChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HasLogo = table.Column<bool>(type: "bit", nullable: false),
                    InvoiceSettings_NumberPrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvoiceSettings_NextNumber = table.Column<int>(type: "int", nullable: false),
                    InvoiceSettings_NumberPadding = table.Column<int>(type: "int", nullable: false),
                    InvoiceSettings_DefaultPaymentTermsDays = table.Column<int>(type: "int", nullable: false),
                    InvoiceSettings_DefaultTaxRate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    InvoiceSettings_TaxLabel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvoiceSettings_DefaultNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InvoiceSettings_Footer = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InvoiceSettings_BrandColor = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: true),
                    EmailSettings_SenderName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EmailSettings_ReplyToEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailSettings_InvoiceEmailSubject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EmailSettings_InvoiceEmailBody = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NotificationSettings_OverdueRemindersEnabled = table.Column<bool>(type: "bit", nullable: false),
                    NotificationSettings_NotificationEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NotificationSettings_WeeklySummaryEnabled = table.Column<bool>(type: "bit", nullable: false),
                    Subscription_PlanCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Subscription_Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Subscription_BillingCycle = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Subscription_TrialEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Subscription_CurrentPeriodEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Subscription_MaxUsers = table.Column<int>(type: "int", nullable: true),
                    Subscription_MaxInvoicesPerMonth = table.Column<int>(type: "int", nullable: true),
                    Subscription_Features = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantInvitations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AcceptedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    AcceptedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantInvitations_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantLogos",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", maxLength: 524288, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantLogos", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_TenantLogos_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantMemberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Permissions = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantMemberships_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantMemberships_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            MigrateExistingData(migrationBuilder);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OwnerId",
                table: "Invoices",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_IssueDate",
                table: "Invoices",
                columns: new[] { "TenantId", "IssueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_IsPlatformAdmin",
                table: "AspNetUsers",
                column: "IsPlatformAdmin");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_Timestamp",
                table: "AuditLogs",
                columns: new[] { "TenantId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationSessions_ImpersonatorUserId",
                table: "ImpersonationSessions",
                column: "ImpersonatorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImpersonationSessions_TenantId",
                table: "ImpersonationSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_TenantId_Email",
                table: "TenantInvitations",
                columns: new[] { "TenantId", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_TokenHash",
                table: "TenantInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_TenantId_UserId",
                table: "TenantMemberships",
                columns: new[] { "TenantId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_UserId",
                table: "TenantMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Name",
                table: "Tenants",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                table: "Tenants",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_AspNetUsers_OwnerId",
                table: "Invoices",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Tenants_TenantId",
                table: "Invoices",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <summary>
        /// Before this migration every user only ever saw their own invoices. To keep exactly that isolation, each
        /// existing user gets a company of their own, becomes its admin and takes their invoices with them. Putting
        /// everyone into one shared default tenant would expose every user's invoices to all the others, and could
        /// break the per-company invoice number uniqueness. Existing users are put on the Enterprise plan (no limits)
        /// so nothing they could do before becomes restricted.
        /// </summary>
        private static void MigrateExistingData(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DECLARE @now datetimeoffset = SYSDATETIMEOFFSET();

                UPDATE AspNetUsers SET CreatedAt = @now WHERE CreatedAt = '0001-01-01T00:00:00+00:00';

                DECLARE @map TABLE (UserId nvarchar(450) NOT NULL PRIMARY KEY, TenantId uniqueidentifier NOT NULL);
                INSERT INTO @map (UserId, TenantId) SELECT Id, NEWID() FROM AspNetUsers;

                INSERT INTO Tenants (
                    Id, Name, Email, DefaultCurrency, DefaultLanguage, TimeZone, Status, HasLogo,
                    InvoiceSettings_NumberPrefix, InvoiceSettings_NextNumber, InvoiceSettings_NumberPadding,
                    InvoiceSettings_DefaultPaymentTermsDays, InvoiceSettings_DefaultTaxRate, InvoiceSettings_TaxLabel,
                    NotificationSettings_OverdueRemindersEnabled, NotificationSettings_WeeklySummaryEnabled,
                    Subscription_PlanCode, Subscription_Status, Subscription_BillingCycle,
                    Subscription_MaxUsers, Subscription_MaxInvoicesPerMonth, Subscription_Features,
                    Created, CreatedBy)
                SELECT
                    m.TenantId,
                    LEFT(COALESCE(u.Email, u.UserName, u.Id), 200),
                    u.Email,
                    COALESCE((SELECT TOP 1 i.Currency FROM Invoices i WHERE i.OwnerId = u.Id GROUP BY i.Currency ORDER BY COUNT(*) DESC), 'USD'),
                    'en', 'UTC', 'Active', 0,
                    'INV-', 1, 4, 30, 0, 'Tax',
                    1, 0,
                    'enterprise', 'Active', 'Monthly',
                    NULL, NULL, 15,
                    @now, 'migration'
                FROM @map m
                JOIN AspNetUsers u ON u.Id = m.UserId;

                -- 511 = every permission (Permission.All).
                INSERT INTO TenantMemberships (TenantId, UserId, Role, Permissions, IsActive, Created, CreatedBy)
                SELECT m.TenantId, m.UserId, 'TenantAdmin', 511, 1, @now, 'migration'
                FROM @map m;

                UPDATE i SET i.TenantId = m.TenantId
                FROM Invoices i
                JOIN @map m ON m.UserId = i.OwnerId;

                INSERT INTO AuditLogs (TenantId, UserId, Action, EntityType, EntityId, Timestamp, Details)
                SELECT m.TenantId, m.UserId, 'tenant.created', 'Tenant', CONVERT(nvarchar(100), m.TenantId), @now, N'{"source":"migration"}'
                FROM @map m;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_AspNetUsers_OwnerId",
                table: "Invoices");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Tenants_TenantId",
                table: "Invoices");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "ImpersonationSessions");

            migrationBuilder.DropTable(
                name: "TenantInvitations");

            migrationBuilder.DropTable(
                name: "TenantLogos");

            migrationBuilder.DropTable(
                name: "TenantMemberships");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_OwnerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_IssueDate",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_IsPlatformAdmin",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsPlatformAdmin",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastLoginAt",
                table: "AspNetUsers");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_OwnerId_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "OwnerId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_AspNetUsers_OwnerId",
                table: "Invoices",
                column: "OwnerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
