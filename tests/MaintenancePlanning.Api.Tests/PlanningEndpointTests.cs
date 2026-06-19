using System.Net;
using System.Net.Http.Json;
using MaintenancePlanning.Application.Planning;
using MaintenancePlanning.Domain.Planning;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MaintenancePlanning.Api.Tests;

public sealed class PlanningEndpointTests
{
    private static readonly DateTimeOffset ReferenceTime = new(2026, 01, 15, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PlanningRuns_CreateRunAndExposeRecommendations()
    {
        var store = InMemoryPlanningStore.WithDefaultCandidates();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IPlanningStore>(store);
        });

        var response = await host.Client.PostAsJsonAsync(
            "/api/v1/planning-runs",
            new CreatePlanningRunRequest
            {
                Horizon = "two-week",
                HorizonStartUtc = ReferenceTime,
                HorizonEndUtc = ReferenceTime.AddDays(14),
                RequestedBy = "planner-review"
            });
        var run = await response.Content.ReadFromJsonAsync<PlanningRunResult>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(run);
        Assert.Equal($"/api/v1/planning-runs/{run.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Completed", run.Status);
        Assert.Equal(2, run.RecommendationCount);
        Assert.Equal(1, run.ReadyRecommendationCount);
        Assert.Equal(1, run.BlockedRecommendationCount);

        var recommendationsResponse = await host.Client.GetAsync($"/api/v1/planning-runs/{run.Id}/recommendations");
        var recommendations = await recommendationsResponse.Content.ReadFromJsonAsync<PlanningRecommendationsResult>();

        Assert.Equal(HttpStatusCode.OK, recommendationsResponse.StatusCode);
        Assert.NotNull(recommendations);
        Assert.Equal(run.Id, recommendations.PlanningRunId);
        Assert.Contains(recommendations.Recommendations, item => item.Actionability == "ready-now");
        Assert.Contains(recommendations.Recommendations, item =>
            item.Actionability == "blocked"
            && item.SourceDataReadiness.OverallStatus == nameof(SourceDataReadiness.Blocked)
            && item.Blockers.Any(blocker => blocker.Category == "parts"));
    }

    [Fact]
    public async Task PackageDecisions_PersistDecisionAuditAndUpdatePackageStatus()
    {
        var store = InMemoryPlanningStore.WithDefaultCandidates();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IPlanningStore>(store);
        });
        var createResponse = await host.Client.PostAsJsonAsync(
            "/api/v1/planning-runs",
            new CreatePlanningRunRequest
            {
                HorizonStartUtc = ReferenceTime,
                HorizonEndUtc = ReferenceTime.AddDays(14)
            });
        var run = await createResponse.Content.ReadFromJsonAsync<PlanningRunResult>();
        Assert.NotNull(run);
        var recommendations = await host.Client.GetFromJsonAsync<PlanningRecommendationsResult>(
            $"/api/v1/planning-runs/{run.Id}/recommendations");
        Assert.NotNull(recommendations);
        var readyPackage = recommendations.Recommendations.Single(item => item.Actionability == "ready-now");

        var decisionResponse = await host.Client.PostAsJsonAsync(
            $"/api/v1/packages/{readyPackage.PackageId}/decisions",
            new RecordPackageDecisionRequest
            {
                Decision = "Accepted",
                ReasonCode = "ready-for-weekly-plan",
                Notes = "Synthetic planner acceptance.",
                DecidedBy = "planner-review"
            });
        var decision = await decisionResponse.Content.ReadFromJsonAsync<PackageDecisionResult>();

        Assert.Equal(HttpStatusCode.OK, decisionResponse.StatusCode);
        Assert.NotNull(decision);
        Assert.Equal("Accepted", decision.PackageStatus);
        Assert.Single(decision.Decisions);
        Assert.Equal("ready-for-weekly-plan", decision.Decisions[0].ReasonCode);

        var updatedRecommendations = await host.Client.GetFromJsonAsync<PlanningRecommendationsResult>(
            $"/api/v1/planning-runs/{run.Id}/recommendations");
        Assert.NotNull(updatedRecommendations);
        var updatedPackage = updatedRecommendations.Recommendations.Single(item => item.PackageId == readyPackage.PackageId);

        Assert.Equal("Accepted", updatedPackage.Status);
        Assert.Contains(updatedPackage.Decisions, item => item.Decision == "Accepted" && item.DecidedBy == "planner-review");
    }

    [Fact]
    public async Task PackageDecisions_ReturnValidationProblem_WhenDecisionIsUnsupported()
    {
        var store = InMemoryPlanningStore.WithDefaultCandidates();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IPlanningStore>(store);
        });
        var createResponse = await host.Client.PostAsJsonAsync(
            "/api/v1/planning-runs",
            new CreatePlanningRunRequest
            {
                HorizonStartUtc = ReferenceTime,
                HorizonEndUtc = ReferenceTime.AddDays(14)
            });
        var run = await createResponse.Content.ReadFromJsonAsync<PlanningRunResult>();
        Assert.NotNull(run);
        var recommendations = await host.Client.GetFromJsonAsync<PlanningRecommendationsResult>(
            $"/api/v1/planning-runs/{run.Id}/recommendations");
        Assert.NotNull(recommendations);

        var response = await host.Client.PostAsJsonAsync(
            $"/api/v1/packages/{recommendations.Recommendations[0].PackageId}/decisions",
            new RecordPackageDecisionRequest
            {
                Decision = "Escalated",
                ReasonCode = "unsupported"
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"planning-validation-failed\"", body, StringComparison.Ordinal);
        Assert.Contains("decision", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlanningRuns_ReturnServiceUnavailable_WhenPersistenceIsNotConfigured()
    {
        await using var host = await TestApiHost.StartAsync();

        var response = await host.Client.PostAsJsonAsync(
            "/api/v1/planning-runs",
            new CreatePlanningRunRequest
            {
                HorizonStartUtc = ReferenceTime,
                HorizonEndUtc = ReferenceTime.AddDays(14)
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"planning-persistence-not-configured\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkOrders_QuerySupportsAllowListedFiltersAndCursorPagination()
    {
        var store = InMemoryPlanningStore.WithDefaultCandidates();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IPlanningStore>(store);
        });

        var firstResponse = await host.Client.GetAsync("/api/v1/work-orders?pageSize=1&sort=workOrderNumber");
        var firstPage = await firstResponse.Content.ReadFromJsonAsync<WorkOrderQueryResult>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPage);
        Assert.Single(firstPage.Items);
        Assert.Equal("WO-6100", firstPage.Items[0].WorkOrderNumber);
        Assert.Equal("workOrderNumber", firstPage.Sort);
        Assert.True(firstPage.AppliedFilters.Backlog);
        Assert.NotNull(firstPage.NextCursor);

        var secondResponse = await host.Client.GetAsync(
            $"/api/v1/work-orders?pageSize=1&sort=workOrderNumber&cursor={Uri.EscapeDataString(firstPage.NextCursor)}");
        var secondPage = await secondResponse.Content.ReadFromJsonAsync<WorkOrderQueryResult>();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPage);
        Assert.Single(secondPage.Items);
        Assert.Equal("WO-6200", secondPage.Items[0].WorkOrderNumber);
        Assert.Equal("Blocked", secondPage.Items[0].Readiness);
        Assert.Equal("parts-unavailable", secondPage.Items[0].SourceDataIssue?.Code);
        Assert.Null(secondPage.NextCursor);

        var filtered = await host.Client.GetFromJsonAsync<WorkOrderQueryResult>(
            "/api/v1/work-orders?readiness=Ready&priority=high&functionalLocation=AREA-6100");

        Assert.NotNull(filtered);
        Assert.Single(filtered.Items);
        Assert.Equal("Ready", filtered.AppliedFilters.Readiness);
        Assert.Equal("high", filtered.AppliedFilters.Priority);
        Assert.Equal("AREA-6100", filtered.AppliedFilters.FunctionalLocation);
    }

    [Fact]
    public async Task WorkOrders_ReturnsDetailForPlannerBacklogItem()
    {
        var store = InMemoryPlanningStore.WithDefaultCandidates();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IPlanningStore>(store);
        });
        var query = await host.Client.GetFromJsonAsync<WorkOrderQueryResult>("/api/v1/work-orders?readiness=Blocked");
        Assert.NotNull(query);

        var detailResponse = await host.Client.GetAsync($"/api/v1/work-orders/{query.Items[0].Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<WorkOrderDetailResult>();

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal("WO-6200", detail.WorkOrderNumber);
        Assert.Equal("Synthetic parts readiness issue.", detail.SourceDataIssue?.Detail);
    }

    [Fact]
    public async Task WorkOrders_ReturnsValidationProblem_WhenSortIsUnsupported()
    {
        var store = InMemoryPlanningStore.WithDefaultCandidates();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IPlanningStore>(store);
        });

        var response = await host.Client.GetAsync("/api/v1/work-orders?sort=unsupported");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("application/problem+json", response.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"planning-validation-failed\"", body, StringComparison.Ordinal);
        Assert.Contains("sort", body, StringComparison.Ordinal);
    }

    private sealed class InMemoryPlanningStore : IPlanningStore
    {
        private readonly List<PlanningWorkOrderSnapshot> _workOrders;
        private readonly List<PlanningMajorEventSnapshot> _majorEvents;
        private readonly Dictionary<Guid, StoredPlanningRun> _runs = new();
        private readonly Dictionary<Guid, StoredPlanningPackage> _packages = new();

        private InMemoryPlanningStore(
            IReadOnlyList<PlanningWorkOrderSnapshot> workOrders,
            IReadOnlyList<PlanningMajorEventSnapshot> majorEvents)
        {
            _workOrders = workOrders.ToList();
            _majorEvents = majorEvents.ToList();
        }

        public bool IsConfigured => true;

        public static InMemoryPlanningStore WithDefaultCandidates()
        {
            var readyLocationId = Guid.NewGuid();
            var blockedLocationId = Guid.NewGuid();
            var readyWorkOrder = CreateWorkOrder(
                "WO-6100",
                SourceDataReadiness.Ready,
                readyLocationId,
                "AREA-6100",
                estimatedHours: 8m,
                priority: "high");
            var blockedWorkOrder = CreateWorkOrder(
                "WO-6200",
                SourceDataReadiness.Blocked,
                blockedLocationId,
                "AREA-6200",
                estimatedHours: null,
                priority: "medium",
                readinessIssueCode: "parts-unavailable",
                readinessIssueDetail: "Synthetic parts readiness issue.");

            return new InMemoryPlanningStore(
                new[] { readyWorkOrder, blockedWorkOrder },
                new[]
                {
                    new PlanningMajorEventSnapshot(
                        Guid.NewGuid(),
                        null,
                        readyLocationId,
                        "access-window",
                        "Shared access window",
                        "medium",
                        ReferenceTime.AddDays(3),
                        ReferenceTime.AddDays(4),
                        null),
                    new PlanningMajorEventSnapshot(
                        Guid.NewGuid(),
                        null,
                        blockedLocationId,
                        "access-window",
                        "Blocked package window",
                        "medium",
                        ReferenceTime.AddDays(5),
                        ReferenceTime.AddDays(6),
                        null)
                });
        }

        public Task<PlanningCandidateSnapshot> LoadCandidateSnapshotAsync(
            DateTimeOffset horizonStartUtc,
            DateTimeOffset horizonEndUtc,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new PlanningCandidateSnapshot(
                horizonStartUtc,
                horizonEndUtc,
                _workOrders,
                _majorEvents));
        }

        public Task SavePlanningRunAsync(
            PlanningRun planningRun,
            IReadOnlyList<WorkOrderPackage> packages,
            IReadOnlyList<PackageItem> packageItems,
            CancellationToken cancellationToken)
        {
            var storedPackages = packages
                .OrderBy(item => item.PackageNumber, StringComparer.Ordinal)
                .Select(package => ToStoredPackage(package, packageItems))
                .ToArray();
            var storedRun = new StoredPlanningRun(
                planningRun.Id,
                planningRun.RunNumber,
                planningRun.Status,
                planningRun.Horizon,
                planningRun.HorizonStartUtc,
                planningRun.HorizonEndUtc,
                planningRun.StartedAtUtc,
                planningRun.CompletedAtUtc,
                planningRun.RequestedBy,
                storedPackages);

            _runs[storedRun.Id] = storedRun;
            foreach (var package in storedPackages)
            {
                _packages[package.Id] = package;
            }

            return Task.CompletedTask;
        }

        public Task<StoredPlanningRun?> FindPlanningRunAsync(
            Guid planningRunId,
            CancellationToken cancellationToken)
        {
            _runs.TryGetValue(planningRunId, out var run);
            return Task.FromResult(run);
        }

        public Task<StoredPlanningRun?> FindPlanningRunWithRecommendationsAsync(
            Guid planningRunId,
            CancellationToken cancellationToken)
        {
            _runs.TryGetValue(planningRunId, out var run);
            return Task.FromResult(run);
        }

        public Task<StoredPlanningPackage?> FindPackageAsync(
            Guid packageId,
            CancellationToken cancellationToken)
        {
            _packages.TryGetValue(packageId, out var package);
            return Task.FromResult(package);
        }

        public Task<WorkOrderQueryPage> QueryWorkOrdersAsync(
            WorkOrderQuerySpec query,
            CancellationToken cancellationToken)
        {
            IEnumerable<PlanningWorkOrderSnapshot> workOrders = _workOrders;

            if (query.Backlog)
            {
                workOrders = workOrders.Where(item =>
                    item.Status is WorkOrderLifecycleStatus.Imported
                        or WorkOrderLifecycleStatus.ReadyForPlanning
                        or WorkOrderLifecycleStatus.Deferred);
            }

            if (!string.IsNullOrWhiteSpace(query.Priority))
            {
                workOrders = workOrders.Where(item => item.Priority == query.Priority);
            }

            if (!string.IsNullOrWhiteSpace(query.FunctionalLocation))
            {
                workOrders = workOrders.Where(item => item.FunctionalLocationCode == query.FunctionalLocation);
            }

            if (query.Readiness is not null)
            {
                workOrders = workOrders.Where(item => item.Readiness == query.Readiness.Value);
            }

            if (query.Status is not null)
            {
                workOrders = workOrders.Where(item => item.Status == query.Status.Value);
            }

            if (query.UpdatedSinceUtc is not null)
            {
                workOrders = workOrders.Where(item => item.SourceUpdatedAtUtc >= query.UpdatedSinceUtc.Value);
            }

            if (query.UpdatedBeforeUtc is not null)
            {
                workOrders = workOrders.Where(item => item.SourceUpdatedAtUtc < query.UpdatedBeforeUtc.Value);
            }

            workOrders = ApplySort(workOrders, query.SortField, query.SortDescending);

            var rows = workOrders.Skip(query.Offset).Take(query.PageSize + 1).ToArray();
            var items = rows.Take(query.PageSize).ToArray();
            var nextOffset = rows.Length > query.PageSize ? query.Offset + items.Length : (int?)null;

            return Task.FromResult(new WorkOrderQueryPage(items, nextOffset));
        }

        public Task<PlanningWorkOrderSnapshot?> FindWorkOrderAsync(
            Guid workOrderId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_workOrders.SingleOrDefault(item => item.Id == workOrderId));
        }

        public Task<IReadOnlyList<StoredPlannerDecision>> SavePackageDecisionAsync(
            Guid packageId,
            PlannerDecisionType decision,
            string reasonCode,
            string? notes,
            string decidedBy,
            DateTimeOffset decidedAtUtc,
            IReadOnlyList<Guid> workOrderIds,
            CancellationToken cancellationToken)
        {
            var package = _packages[packageId];
            var newDecisions = workOrderIds.Count == 0
                ? new[]
                {
                    CreateDecision(packageId, null, decision, reasonCode, notes, decidedBy, decidedAtUtc)
                }
                : workOrderIds
                    .OrderBy(item => item)
                    .Select(item => CreateDecision(packageId, item, decision, reasonCode, notes, decidedBy, decidedAtUtc))
                    .ToArray();
            var selectedWorkOrderIds = workOrderIds.ToHashSet();
            var updatedItems = package.Items
                .Select(item => selectedWorkOrderIds.Contains(item.WorkOrder.Id)
                    ? item with
                    {
                        WorkOrder = item.WorkOrder with
                        {
                            Status = decision == PlannerDecisionType.Deferred
                                ? WorkOrderLifecycleStatus.Deferred
                                : WorkOrderLifecycleStatus.DecisionRecorded
                        }
                    }
                    : item)
                .ToArray();
            var updatedPackage = package with
            {
                Status = ToPackageStatus(decision),
                Items = updatedItems,
                Decisions = package.Decisions.Concat(newDecisions).ToArray()
            };

            _packages[packageId] = updatedPackage;
            foreach (var run in _runs.Values.ToArray())
            {
                if (run.Packages.All(item => item.Id != packageId))
                {
                    continue;
                }

                _runs[run.Id] = run with
                {
                    Packages = run.Packages.Select(item => item.Id == packageId ? updatedPackage : item).ToArray()
                };
            }

            return Task.FromResult<IReadOnlyList<StoredPlannerDecision>>(newDecisions);
        }

        private StoredPlanningPackage ToStoredPackage(
            WorkOrderPackage package,
            IReadOnlyList<PackageItem> packageItems)
        {
            return new StoredPlanningPackage(
                package.Id,
                package.PlanningRunId,
                package.PackageNumber,
                package.Title,
                package.Status,
                package.EstimatedHours,
                package.PlannedStartUtc,
                package.PlannedEndUtc,
                package.RecommendationRationale,
                packageItems
                    .Where(item => item.WorkOrderPackageId == package.Id)
                    .OrderBy(item => item.Sequence)
                    .Select(item => new StoredPlanningPackageItem(
                        item.Id,
                        item.WorkOrderPackageId,
                        item.Sequence,
                        item.FitReason,
                        _workOrders.Single(workOrder => workOrder.Id == item.WorkOrderId)))
                    .ToArray(),
                Array.Empty<StoredPlannerDecision>());
        }

        private static IOrderedEnumerable<PlanningWorkOrderSnapshot> ApplySort(
            IEnumerable<PlanningWorkOrderSnapshot> query,
            string sortField,
            bool descending)
        {
            return sortField switch
            {
                "priority" => descending
                    ? query.OrderByDescending(item => item.Priority).ThenBy(item => item.WorkOrderNumber)
                    : query.OrderBy(item => item.Priority).ThenBy(item => item.WorkOrderNumber),
                "requiredStartUtc" => descending
                    ? query.OrderByDescending(item => item.RequiredStartUtc ?? DateTimeOffset.MaxValue).ThenBy(item => item.WorkOrderNumber)
                    : query.OrderBy(item => item.RequiredStartUtc ?? DateTimeOffset.MaxValue).ThenBy(item => item.WorkOrderNumber),
                "updatedAtUtc" => descending
                    ? query.OrderByDescending(item => item.SourceUpdatedAtUtc).ThenBy(item => item.WorkOrderNumber)
                    : query.OrderBy(item => item.SourceUpdatedAtUtc).ThenBy(item => item.WorkOrderNumber),
                "workOrderNumber" => descending
                    ? query.OrderByDescending(item => item.WorkOrderNumber)
                    : query.OrderBy(item => item.WorkOrderNumber),
                _ => descending
                    ? query.OrderByDescending(item => item.DueAtUtc ?? DateTimeOffset.MaxValue).ThenBy(item => item.WorkOrderNumber)
                    : query.OrderBy(item => item.DueAtUtc ?? DateTimeOffset.MaxValue).ThenBy(item => item.WorkOrderNumber)
            };
        }

        private static PlanningWorkOrderSnapshot CreateWorkOrder(
            string workOrderNumber,
            SourceDataReadiness readiness,
            Guid functionalLocationId,
            string functionalLocationCode,
            decimal? estimatedHours,
            string priority,
            string? readinessIssueCode = null,
            string? readinessIssueDetail = null)
        {
            return new PlanningWorkOrderSnapshot(
                Guid.NewGuid(),
                "synthetic-source",
                workOrderNumber,
                workOrderNumber,
                $"Synthetic work {workOrderNumber}",
                "corrective",
                priority,
                readiness == SourceDataReadiness.Ready ? WorkOrderLifecycleStatus.ReadyForPlanning : WorkOrderLifecycleStatus.Imported,
                readiness,
                readinessIssueCode,
                readinessIssueDetail,
                ReferenceTime.AddDays(2),
                ReferenceTime.AddDays(7),
                null,
                estimatedHours,
                ReferenceTime,
                ReferenceTime.AddMinutes(1),
                null,
                null,
                null,
                null,
                functionalLocationId,
                functionalLocationCode,
                $"Area {functionalLocationCode}");
        }

        private static StoredPlannerDecision CreateDecision(
            Guid packageId,
            Guid? workOrderId,
            PlannerDecisionType decision,
            string reasonCode,
            string? notes,
            string decidedBy,
            DateTimeOffset decidedAtUtc)
        {
            return new StoredPlannerDecision(
                Guid.NewGuid(),
                packageId,
                workOrderId,
                decision,
                reasonCode,
                notes,
                decidedAtUtc,
                decidedBy);
        }

        private static PackageStatus ToPackageStatus(PlannerDecisionType decision)
        {
            return decision switch
            {
                PlannerDecisionType.Accepted => PackageStatus.Accepted,
                PlannerDecisionType.Rejected => PackageStatus.Rejected,
                PlannerDecisionType.Deferred => PackageStatus.Deferred,
                _ => PackageStatus.Recommended
            };
        }
    }
}
