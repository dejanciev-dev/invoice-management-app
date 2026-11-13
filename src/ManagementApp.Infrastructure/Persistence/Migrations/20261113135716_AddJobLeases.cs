using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ManagementApp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobLeases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobLeases",
                columns: table => new
                {
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LockedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LockedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobLeases", x => x.Name);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobLeases");
        }
    }
}
