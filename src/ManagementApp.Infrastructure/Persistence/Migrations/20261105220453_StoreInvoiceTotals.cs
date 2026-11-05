using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class StoreInvoiceTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BalanceDue",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Invoices",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_DueDate",
                table: "Invoices",
                columns: new[] { "TenantId", "DueDate" });

            BackfillTotals(migrationBuilder);
        }

        /// <summary>
        /// Calculates the stored totals of existing invoices with the same rules as the domain model (Invoice and
        /// InvoiceItem): line amounts and percentages are rounded to two decimals half away from zero, which is what
        /// SQL Server's ROUND does; a flat discount is capped at the subtotal; percentage tax applies after the discount.
        /// From now on the application keeps these columns up to date on every save.
        /// </summary>
        private static void BackfillTotals(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH Subtotals AS (
                    SELECT i.Id,
                           COALESCE((SELECT SUM(ROUND(it.Quantity * it.UnitPrice, 2))
                                     FROM InvoiceItem it
                                     WHERE it.InvoiceId = i.Id), 0) AS Subtotal
                    FROM Invoices i
                ),
                Discounts AS (
                    SELECT s.Id, s.Subtotal,
                           CASE WHEN i.DiscountType = 'Percentage' THEN ROUND(s.Subtotal * i.Discount / 100, 2)
                                WHEN i.Discount < s.Subtotal THEN i.Discount
                                ELSE s.Subtotal END AS DiscountAmount
                    FROM Subtotals s
                    JOIN Invoices i ON i.Id = s.Id
                ),
                Totals AS (
                    SELECT d.Id,
                           d.Subtotal - d.DiscountAmount
                             + CASE WHEN i.TaxType = 'Percentage' THEN ROUND((d.Subtotal - d.DiscountAmount) * i.Tax / 100, 2)
                                    ELSE i.Tax END AS Total
                    FROM Discounts d
                    JOIN Invoices i ON i.Id = d.Id
                )
                UPDATE i
                SET i.Total = t.Total,
                    i.BalanceDue = t.Total - i.AmountPaid
                FROM Invoices i
                JOIN Totals t ON t.Id = i.Id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_DueDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "BalanceDue",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Invoices");
        }
    }
}
