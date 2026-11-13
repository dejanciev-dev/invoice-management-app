using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubscriptionEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    At = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FromPlanCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToPlanCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TrialEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CurrentPeriodEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GraceEndsAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    MaxUsers = table.Column<int>(type: "int", nullable: true),
                    MaxInvoicesPerMonth = table.Column<int>(type: "int", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProviderEventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionEvents_ProviderEventId",
                table: "SubscriptionEvents",
                column: "ProviderEventId",
                unique: true,
                filter: "[ProviderEventId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionEvents_TenantId_At",
                table: "SubscriptionEvents",
                columns: new[] { "TenantId", "At" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionEvents");
        }
    }
}
