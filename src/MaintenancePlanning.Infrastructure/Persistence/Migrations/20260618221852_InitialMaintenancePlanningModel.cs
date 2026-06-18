using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MaintenancePlanning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialMaintenancePlanningModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "planning");

            migrationBuilder.CreateTable(
                name: "functional_locations",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ParentSourceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    SourceUpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_functional_locations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "integration_imports",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ImportKind = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReceivedCount = table.Column<int>(type: "int", nullable: false),
                    AcceptedCount = table.Column<int>(type: "int", nullable: false),
                    RejectedCount = table.Column<int>(type: "int", nullable: false),
                    FailureCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_imports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_events",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    AggregateType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AggregateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastErrorCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "planning_runs",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Horizon = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    HorizonStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HorizonEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RequestedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planning_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "assets",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FunctionalLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSystem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AssetNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Criticality = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SourceUpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assets_functional_locations_FunctionalLocationId",
                        column: x => x.FunctionalLocationId,
                        principalSchema: "planning",
                        principalTable: "functional_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_events",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IntegrationImportId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    WorkOrderSourceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_events_integration_imports_IntegrationImportId",
                        column: x => x.IntegrationImportId,
                        principalSchema: "planning",
                        principalTable: "integration_imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "work_order_packages",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanningRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageNumber = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    EstimatedHours = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: false),
                    PlannedStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PlannedEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RecommendationRationale = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_order_packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_order_packages_planning_runs_PlanningRunId",
                        column: x => x.PlanningRunId,
                        principalSchema: "planning",
                        principalTable: "planning_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "major_events",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FunctionalLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReadinessIssueCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_major_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_major_events_assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "planning",
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_major_events_functional_locations_FunctionalLocationId",
                        column: x => x.FunctionalLocationId,
                        principalSchema: "planning",
                        principalTable: "functional_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_orders",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FunctionalLocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceSystem = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    SourceId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WorkOrderNumber = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    WorkType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Readiness = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReadinessIssueCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ReadinessIssueDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequiredStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DueAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ScheduledStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EstimatedHours = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    SourceUpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ImportedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourcePayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_work_orders_assets_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "planning",
                        principalTable: "assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_work_orders_functional_locations_FunctionalLocationId",
                        column: x => x.FunctionalLocationId,
                        principalSchema: "planning",
                        principalTable: "functional_locations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "package_items",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sequence = table.Column<int>(type: "int", nullable: false),
                    FitReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_package_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_package_items_work_order_packages_WorkOrderPackageId",
                        column: x => x.WorkOrderPackageId,
                        principalSchema: "planning",
                        principalTable: "work_order_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_package_items_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "planning",
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "planner_decisions",
                schema: "planning",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderPackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Decision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DecidedBy = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_planner_decisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_planner_decisions_work_order_packages_WorkOrderPackageId",
                        column: x => x.WorkOrderPackageId,
                        principalSchema: "planning",
                        principalTable: "work_order_packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_planner_decisions_work_orders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "planning",
                        principalTable: "work_orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "functional_locations",
                columns: new[] { "Id", "Code", "ImportedAtUtc", "Name", "ParentSourceId", "SourceId", "SourceSystem", "SourceUpdatedAtUtc" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000001"), "AREA-1000", new DateTimeOffset(new DateTime(2026, 1, 15, 0, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Area 1000", null, "FL-1000", "synthetic-source", new DateTimeOffset(new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "integration_imports",
                columns: new[] { "Id", "AcceptedCount", "CompletedAtUtc", "FailureCode", "IdempotencyKey", "ImportKind", "ReceivedAtUtc", "ReceivedCount", "RejectedCount", "SourceSystem", "Status" },
                values: new object[] { new Guid("90000000-0000-0000-0000-000000000001"), 2, new DateTimeOffset(new DateTime(2026, 1, 15, 0, 4, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "seed-work-orders-2026-01-15", "work-orders", new DateTimeOffset(new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 2, 0, "synthetic-source", "Completed" });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "outbox_events",
                columns: new[] { "Id", "AggregateId", "AggregateType", "AttemptCount", "AvailableAtUtc", "CreatedAtUtc", "EventType", "LastErrorCode", "PayloadJson", "PublishedAtUtc", "Status" },
                values: new object[] { new Guid("b0000000-0000-0000-0000-000000000001"), new Guid("60000000-0000-0000-0000-000000000001"), "WorkOrderPackage", 0, new DateTimeOffset(new DateTime(2026, 1, 15, 0, 11, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 15, 0, 11, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "planning.package.recommended", null, "{\"eventType\":\"planning.package.recommended\",\"packageNumber\":\"PKG-1000\"}", null, "Pending" });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "planning_runs",
                columns: new[] { "Id", "CompletedAtUtc", "Horizon", "HorizonEndUtc", "HorizonStartUtc", "RequestedBy", "RunNumber", "StartedAtUtc", "Status" },
                values: new object[] { new Guid("50000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 15, 0, 11, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "two-week", new DateTimeOffset(new DateTime(2026, 1, 30, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 16, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "local-review", "RUN-1000", new DateTimeOffset(new DateTime(2026, 1, 15, 0, 10, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Completed" });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "assets",
                columns: new[] { "Id", "AssetNumber", "Criticality", "FunctionalLocationId", "ImportedAtUtc", "Name", "SourceId", "SourceSystem", "SourceUpdatedAtUtc" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000001"), "AS-1000", "high", new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 15, 0, 1, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Primary Pump Set", "ASSET-1000", "synthetic-source", new DateTimeOffset(new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "integration_events",
                columns: new[] { "Id", "EventId", "EventType", "IntegrationImportId", "OccurredAtUtc", "PayloadHash", "RecordedAtUtc", "Status", "WorkOrderSourceId" },
                values: new object[] { new Guid("a0000000-0000-0000-0000-000000000001"), "seed-event-1000", "work-order-imported", new Guid("90000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "sha256-seed-event", new DateTimeOffset(new DateTime(2026, 1, 15, 0, 4, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Accepted", "WO-1000" });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "work_order_packages",
                columns: new[] { "Id", "EstimatedHours", "PackageNumber", "PlannedEndUtc", "PlannedStartUtc", "PlanningRunId", "RecommendationRationale", "Status", "Title" },
                values: new object[] { new Guid("60000000-0000-0000-0000-000000000001"), 8.5m, "PKG-1000", new DateTimeOffset(new DateTime(2026, 1, 20, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 1, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("50000000-0000-0000-0000-000000000001"), "Combines ready work with the available access window.", "Recommended", "Pump seal work package" });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "major_events",
                columns: new[] { "Id", "AssetId", "EndsAtUtc", "EventType", "FunctionalLocationId", "ImportedAtUtc", "ReadinessIssueCode", "Severity", "SourceId", "SourceSystem", "StartsAtUtc", "Title" },
                values: new object[] { new Guid("40000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 21, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "access-window", new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 15, 0, 4, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "medium", "EVT-1000", "synthetic-source", new DateTimeOffset(new DateTime(2026, 1, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Shared access window" });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "work_orders",
                columns: new[] { "Id", "AssetId", "DueAtUtc", "EstimatedHours", "FunctionalLocationId", "ImportedAtUtc", "Priority", "Readiness", "ReadinessIssueCode", "ReadinessIssueDetail", "RequiredStartUtc", "ScheduledStartUtc", "SourceId", "SourcePayloadHash", "SourceSystem", "SourceUpdatedAtUtc", "Status", "Title", "WorkOrderNumber", "WorkType" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 24, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 8.5m, new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 15, 0, 2, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "high", "Ready", null, null, new DateTimeOffset(new DateTime(2026, 1, 20, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "WO-1000", "sha256-ready-work-order", "synthetic-source", new DateTimeOffset(new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "ReadyForPlanning", "Replace pump seals", "WO-1000", "corrective" },
                    { new Guid("30000000-0000-0000-0000-000000000002"), new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 29, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 15, 0, 3, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "medium", "NeedsReview", "missing-estimate", "Estimated effort is required before packaging.", new DateTimeOffset(new DateTime(2026, 1, 22, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "WO-1001", "sha256-review-work-order", "synthetic-source", new DateTimeOffset(new DateTime(2026, 1, 15, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Imported", "Inspect standby valve", "WO-1001", "preventive" }
                });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "package_items",
                columns: new[] { "Id", "FitReason", "Sequence", "WorkOrderId", "WorkOrderPackageId" },
                values: new object[] { new Guid("70000000-0000-0000-0000-000000000001"), "Work order is ready and aligns with the access window.", 1, new Guid("30000000-0000-0000-0000-000000000001"), new Guid("60000000-0000-0000-0000-000000000001") });

            migrationBuilder.InsertData(
                schema: "planning",
                table: "planner_decisions",
                columns: new[] { "Id", "DecidedAtUtc", "DecidedBy", "Decision", "Notes", "ReasonCode", "WorkOrderId", "WorkOrderPackageId" },
                values: new object[] { new Guid("80000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 1, 15, 0, 12, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "local-review", "Deferred", "Synthetic seed decision for local review.", "awaiting-review", new Guid("30000000-0000-0000-0000-000000000001"), new Guid("60000000-0000-0000-0000-000000000001") });

            migrationBuilder.CreateIndex(
                name: "IX_assets_AssetNumber",
                schema: "planning",
                table: "assets",
                column: "AssetNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assets_FunctionalLocationId",
                schema: "planning",
                table: "assets",
                column: "FunctionalLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_assets_SourceSystem_SourceId",
                schema: "planning",
                table: "assets",
                columns: new[] { "SourceSystem", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_functional_locations_Code",
                schema: "planning",
                table: "functional_locations",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_functional_locations_SourceSystem_SourceId",
                schema: "planning",
                table: "functional_locations",
                columns: new[] { "SourceSystem", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_events_EventId",
                schema: "planning",
                table: "integration_events",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_events_IntegrationImportId",
                schema: "planning",
                table: "integration_events",
                column: "IntegrationImportId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_imports_SourceSystem_IdempotencyKey",
                schema: "planning",
                table: "integration_imports",
                columns: new[] { "SourceSystem", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_major_events_AssetId",
                schema: "planning",
                table: "major_events",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_major_events_FunctionalLocationId",
                schema: "planning",
                table: "major_events",
                column: "FunctionalLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_major_events_SourceSystem_SourceId",
                schema: "planning",
                table: "major_events",
                columns: new[] { "SourceSystem", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_events_Status_AvailableAtUtc",
                schema: "planning",
                table: "outbox_events",
                columns: new[] { "Status", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_package_items_WorkOrderId",
                schema: "planning",
                table: "package_items",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_package_items_WorkOrderPackageId_WorkOrderId",
                schema: "planning",
                table: "package_items",
                columns: new[] { "WorkOrderPackageId", "WorkOrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_planner_decisions_WorkOrderId",
                schema: "planning",
                table: "planner_decisions",
                column: "WorkOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_planner_decisions_WorkOrderPackageId",
                schema: "planning",
                table: "planner_decisions",
                column: "WorkOrderPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_planning_runs_RunNumber",
                schema: "planning",
                table: "planning_runs",
                column: "RunNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_packages_PackageNumber",
                schema: "planning",
                table: "work_order_packages",
                column: "PackageNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_order_packages_PlanningRunId",
                schema: "planning",
                table: "work_order_packages",
                column: "PlanningRunId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_AssetId",
                schema: "planning",
                table: "work_orders",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_FunctionalLocationId",
                schema: "planning",
                table: "work_orders",
                column: "FunctionalLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_Readiness",
                schema: "planning",
                table: "work_orders",
                column: "Readiness");

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_SourceSystem_SourceId",
                schema: "planning",
                table: "work_orders",
                columns: new[] { "SourceSystem", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_orders_WorkOrderNumber",
                schema: "planning",
                table: "work_orders",
                column: "WorkOrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_events",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "major_events",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "outbox_events",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "package_items",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "planner_decisions",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "integration_imports",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "work_order_packages",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "work_orders",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "planning_runs",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "assets",
                schema: "planning");

            migrationBuilder.DropTable(
                name: "functional_locations",
                schema: "planning");
        }
    }
}
