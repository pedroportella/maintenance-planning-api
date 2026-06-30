using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenancePlanning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(MaintenancePlanningDbContext))]
    [Migration("20260630120000_AddPlanningRunIdempotency")]
    public partial class AddPlanningRunIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "planning",
                table: "planning_runs",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: "planning",
                table: "planning_runs",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [planning].[planning_runs]
                SET [IdempotencyKey] = N'seed-planning-run-2026-01-15',
                    [RequestHash] = N'sha256-seed-planning-run'
                WHERE [Id] = '50000000-0000-0000-0000-000000000001';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_planning_runs_IdempotencyKey",
                schema: "planning",
                table: "planning_runs",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_planning_runs_IdempotencyKey",
                schema: "planning",
                table: "planning_runs");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "planning",
                table: "planning_runs");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                schema: "planning",
                table: "planning_runs");
        }
    }
}
