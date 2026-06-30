using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenancePlanning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MaintenancePlanningDbContext))]
    [Migration("20260701100000_AddPersistenceIndexesAndConstraints")]
    public partial class AddPersistenceIndexesAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_integration_imports_Status_ReceivedAtUtc",
                schema: "planning",
                table: "integration_imports",
                columns: new[] { "Status", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_integration_imports_Status_ReceivedAtUtc",
                schema: "planning",
                table: "integration_imports");
        }
    }
}
