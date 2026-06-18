using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenancePlanning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImportHttpContractAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "IgnoredDuplicateCount",
                schema: "planning",
                table: "integration_imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "IgnoredStaleCount",
                schema: "planning",
                table: "integration_imports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                schema: "planning",
                table: "integration_imports",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                schema: "planning",
                table: "integration_events",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Disposition",
                schema: "planning",
                table: "integration_events",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "planning",
                table: "integration_events",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAtUtc",
                schema: "planning",
                table: "integration_events",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "Readiness",
                schema: "planning",
                table: "integration_events",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SchemaVersion",
                schema: "planning",
                table: "integration_events",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceRecordId",
                schema: "planning",
                table: "integration_events",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SourceSystem",
                schema: "planning",
                table: "integration_events",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ValidationIssueCode",
                schema: "planning",
                table: "integration_events",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.UpdateData(
                schema: "planning",
                table: "integration_events",
                keyColumn: "Id",
                keyValue: new Guid("a0000000-0000-0000-0000-000000000001"),
                columns: new[] { "CorrelationId", "Disposition", "IdempotencyKey", "PublishedAtUtc", "Readiness", "SchemaVersion", "SourceRecordId", "SourceSystem", "ValidationIssueCode" },
                values: new object[] { "seed-correlation-1000", "accepted", "seed-work-order-event-1000", new DateTimeOffset(new DateTime(2026, 1, 15, 0, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Ready", "1.0", "WO-1000", "synthetic-source", null });

            migrationBuilder.UpdateData(
                schema: "planning",
                table: "integration_imports",
                keyColumn: "Id",
                keyValue: new Guid("90000000-0000-0000-0000-000000000001"),
                columns: new[] { "IgnoredDuplicateCount", "IgnoredStaleCount", "RequestHash" },
                values: new object[] { 0, 0, "sha256-seed-work-orders" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_events_SourceSystem_IdempotencyKey",
                schema: "planning",
                table: "integration_events",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_integration_events_SourceSystem_IdempotencyKey",
                schema: "planning",
                table: "integration_events");

            migrationBuilder.DropColumn(
                name: "IgnoredDuplicateCount",
                schema: "planning",
                table: "integration_imports");

            migrationBuilder.DropColumn(
                name: "IgnoredStaleCount",
                schema: "planning",
                table: "integration_imports");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                schema: "planning",
                table: "integration_imports");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                schema: "planning",
                table: "integration_events");

            migrationBuilder.DropColumn(
                name: "Disposition",
                schema: "planning",
                table: "integration_events");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "planning",
                table: "integration_events");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                schema: "planning",
                table: "integration_events");

            migrationBuilder.DropColumn(
                name: "Readiness",
                schema: "planning",
                table: "integration_events");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                schema: "planning",
                table: "integration_events");

            migrationBuilder.DropColumn(
                name: "SourceRecordId",
                schema: "planning",
                table: "integration_events");

            migrationBuilder.DropColumn(
                name: "SourceSystem",
                schema: "planning",
                table: "integration_events");

            migrationBuilder.DropColumn(
                name: "ValidationIssueCode",
                schema: "planning",
                table: "integration_events");
        }
    }
}
