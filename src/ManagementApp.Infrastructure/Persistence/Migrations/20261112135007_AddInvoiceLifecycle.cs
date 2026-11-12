using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the Draft → Issued → Void lifecycle. Every existing invoice is treated as issued. Rolling back loses which
    /// invoices were drafts or void, and void invoices would count as unpaid again.
    /// </summary>
    public partial class AddInvoiceLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_DueDate",
                table: "Invoices");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IssuedAt",
                table: "Invoices",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Invoices",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Issued"); // existing invoices; new ones always get their state from the app

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "Invoices",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                table: "Invoices",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_State_DueDate",
                table: "Invoices",
                columns: new[] { "TenantId", "State", "DueDate" });

            // Invoices that existed before the lifecycle were already sent to customers, so they become issued, as of
            // when they were created. Only invoices made from now on start as drafts.
            migrationBuilder.Sql("UPDATE Invoices SET IssuedAt = Created WHERE State = 'Issued' AND IssuedAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_State_DueDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "IssuedAt",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "Invoices");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_DueDate",
                table: "Invoices",
                columns: new[] { "TenantId", "DueDate" });
        }
    }
}
