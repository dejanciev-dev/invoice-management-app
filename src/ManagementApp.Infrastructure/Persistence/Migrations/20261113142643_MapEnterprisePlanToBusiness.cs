using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The Enterprise plan was replaced by Business. Tenants on it move to the new code but keep the limits and
    /// features copied onto them when the plan was applied, so nothing they can do changes.
    /// </summary>
    public partial class MapEnterprisePlanToBusiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Tenants SET Subscription_PlanCode = 'business' WHERE Subscription_PlanCode = 'enterprise';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Only tenants that were moved can't be told apart from ones put on Business later; leave the code as is.
        }
    }
}
