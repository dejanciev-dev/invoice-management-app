using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Moves each company's next invoice number into its own <c>InvoiceNumberSequences</c> row and makes numbers a
    /// property of issued invoices only: drafts lose the number they were given when created, and get the next one of
    /// the sequence when they are issued.
    /// </summary>
    public partial class AssignInvoiceNumbersOnIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceNumberSequences",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NextNumber = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceNumberSequences", x => x.TenantId);
                    table.ForeignKey(
                        name: "FK_InvoiceNumberSequences_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Each company carries on from where its numbering setting was.
            migrationBuilder.Sql(
                "INSERT INTO InvoiceNumberSequences (TenantId, NextNumber) SELECT Id, InvoiceSettings_NextNumber FROM Tenants;");

            migrationBuilder.DropColumn(
                name: "InvoiceSettings_NextNumber",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_InvoiceNumber",
                table: "Invoices");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "Invoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            // Drafts get their number when issued. Issued and void invoices keep theirs.
            migrationBuilder.Sql("UPDATE Invoices SET InvoiceNumber = NULL WHERE State = 'Draft';");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true,
                filter: "[InvoiceNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_InvoiceNumber",
                table: "Invoices");

            // Numbers were required before: drafts get a placeholder that cannot clash with a real number.
            migrationBuilder.Sql(
                "UPDATE Invoices SET InvoiceNumber = CONCAT('DRAFT-', Id) WHERE InvoiceNumber IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "Invoices",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_InvoiceNumber",
                table: "Invoices",
                columns: new[] { "TenantId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.AddColumn<int>(
                name: "InvoiceSettings_NextNumber",
                table: "Tenants",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE t SET InvoiceSettings_NextNumber = s.NextNumber
                FROM Tenants t JOIN InvoiceNumberSequences s ON s.TenantId = t.Id;
                """);

            migrationBuilder.DropTable(
                name: "InvoiceNumberSequences");
        }
    }
}
