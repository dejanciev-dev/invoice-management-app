using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkInvoicesToCustomers : Migration
    {
        /// <summary>Marks the customers this migration creates, so <see cref="Down"/> can remove exactly those.</summary>
        private const string CreatedByMigration = "migration:LinkInvoicesToCustomers";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerId",
                table: "Invoices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_TenantId_CustomerId_IssueDate",
                table: "Invoices",
                columns: new[] { "TenantId", "CustomerId", "IssueDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_Customers_CustomerId",
                table: "Invoices",
                column: "CustomerId",
                principalTable: "Customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            CreateCustomersFromBillToText(migrationBuilder);
        }

        /// <summary>
        /// Turns each distinct "Bill to" block of a tenant's existing invoices into a customer and links those invoices to
        /// it. The first line becomes the name (cut to 200 characters) and the remaining lines the billing address. When
        /// all of the block's invoices use one currency, that becomes the customer's currency. The "To" text itself is
        /// kept unchanged as each invoice's printed snapshot.
        /// <para>
        /// Blocks are compared without carriage returns and with the database's collation (case-insensitive and
        /// ignoring trailing spaces by default), so "ACME\r\nLondon" and "Acme\nLondon" become one customer. MERGE ...
        /// OUTPUT maps every new customer id back to its exact block, so linking never depends on the (possibly cut) name.
        /// Written for SQL Server 2017+ (no TRIM(chars FROM ...), which needs 2022).
        /// </para>
        /// </summary>
        private static void CreateCustomersFromBillToText(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                DECLARE @Created TABLE (CustomerId int NOT NULL, TenantId uniqueidentifier NOT NULL, BillTo nvarchar(1000) NOT NULL);

                WITH Blocks AS (
                    SELECT TenantId, REPLACE([To], CHAR(13), '') AS BillTo, Currency, Created
                    FROM Invoices
                    WHERE CustomerId IS NULL
                ),
                Groups AS (
                    SELECT TenantId,
                           BillTo,
                           CHARINDEX(CHAR(10), BillTo) AS LineBreak,
                           MIN(Created) AS Created,
                           CASE WHEN MIN(Currency) = MAX(Currency) THEN MIN(Currency) END AS Currency
                    FROM Blocks
                    WHERE LTRIM(RTRIM(BillTo)) <> ''
                    GROUP BY TenantId, BillTo
                ),
                Split AS (
                    SELECT TenantId, BillTo, Created, Currency,
                           LTRIM(RTRIM(CASE WHEN LineBreak > 0 THEN LEFT(BillTo, LineBreak - 1) ELSE BillTo END)) AS FirstLine,
                           CASE WHEN LineBreak > 0 THEN SUBSTRING(BillTo, LineBreak + 1, 1000) END AS Rest
                    FROM Groups
                ),
                Parsed AS (
                    -- The address starts at the first character that is not a space, tab or line break, so blank lines
                    -- between the name and the address are dropped.
                    SELECT TenantId, BillTo, Created, Currency, FirstLine,
                           NULLIF(RTRIM(SUBSTRING(Rest, NULLIF(PATINDEX(N'%[^ ' + CHAR(9) + CHAR(10) + N']%', Rest), 0), 1000)), '') AS Address
                    FROM Split
                )
                MERGE INTO Customers AS target
                USING Parsed AS source ON 1 = 0
                WHEN NOT MATCHED THEN
                    INSERT (TenantId, Name, Address, Currency, Created, CreatedBy)
                    VALUES (source.TenantId,
                            LEFT(COALESCE(NULLIF(source.FirstLine, ''), LTRIM(RTRIM(source.BillTo))), 200),
                            source.Address,
                            source.Currency,
                            source.Created,
                            '{CreatedByMigration}')
                OUTPUT inserted.Id, source.TenantId, source.BillTo INTO @Created;

                UPDATE i
                SET i.CustomerId = c.CustomerId
                FROM Invoices i
                JOIN @Created c ON c.TenantId = i.TenantId AND c.BillTo = REPLACE(i.[To], CHAR(13), '')
                WHERE i.CustomerId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_Customers_CustomerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_CustomerId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_TenantId_CustomerId_IssueDate",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "Invoices");

            // Customers created by hand stay; only the ones made from "Bill to" text are removed.
            migrationBuilder.Sql($"DELETE FROM Customers WHERE CreatedBy = '{CreatedByMigration}';");
        }
    }
}
