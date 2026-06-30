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
                IdempotencyKey = "planning-run-create-recommendations",
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
    public async Task PlanningRuns_ReplayDuplicateRequestWithSameIdempotencyKey()
    {
        var store = InMemoryPlanningStore.WithDefaultCandidates();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IPlanningStore>(store);
        });
        var request = new CreatePlanningRunRequest
        {
            IdempotencyKey = "planning-run-replay",
            Horizon = "two-week",
            HorizonStartUtc = ReferenceTime,
            HorizonEndUtc = ReferenceTime.AddDays(14),
            RequestedBy = "planner-review"
        };

        var firstResponse = await host.Client.PostAsJsonAsync("/api/v1/planning-runs", request);
        var secondResponse = await host.Client.PostAsJsonAsync("/api/v1/planning-runs", request);
        var first = await firstResponse.Content.ReadFromJsonAsync<PlanningRunResult>();
        var second = await secondResponse.Content.ReadFromJsonAsync<PlanningRunResult>();

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, secondResponse.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(first.RunNumber, second.RunNumber);
        Assert.Equal(1, store.PlanningRunCount);
        Assert.Equal(1, store.PlanningRunCompletedOutboxEventCount);
    }

    [Fact]
    public async Task PlanningRuns_ReturnConflict_WhenIdempotencyKeyHasDifferentRequest()
    {
        var store = InMemoryPlanningStore.WithDefaultCandidates();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IPlanningStore>(store);
        });
        var firstRequest = new CreatePlanningRunRequest
        {
            IdempotencyKey = "planning-run-conflict",
            HorizonStartUtc = ReferenceTime,
            HorizonEndUtc = ReferenceTime.AddDays(14)
        };
        var secondRequest = new CreatePlanningRunRequest
        {
            IdempotencyKey = "planning-run-conflict",
            HorizonStartUtc = ReferenceTime,
            HorizonEndUtc = ReferenceTime.AddDays(21)
        };

        var firstResponse = await host.Client.PostAsJsonAsync("/api/v1/planning-runs", firstRequest);
        var secondResponse = await host.Client.PostAsJsonAsync("/api/v1/planning-runs", secondRequest);
        var body = await secondResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.Contains("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"planning-idempotency-conflict\"", body, StringComparison.Ordinal);
        Assert.Equal(1, store.PlanningRunCount);
        Assert.Equal(1, store.PlanningRunCompletedOutboxEventCount);
    }

    [Fact]
    public async Task PlanningRuns_CoalesceRapidDuplicateRequests()
    {
        var store = InMemoryPlanningStore.WithDefaultCandidates();
        await using var host = await TestApiHost.StartAsync(builder =>
        {
            builder.Services.AddSingleton<IPlanningStore>(store);
        });
        var request = new CreatePlanningRunRequest
        {
            IdempotencyKey = "planning-run-concurrent",
            Horizon = "two-week",
            HorizonStartUtc = ReferenceTime,
            HorizonEndUtc = ReferenceTime.AddDays(14),
            RequestedBy = "planner-review"
        };

        var responses = await Task.WhenAll(Enumerable
            .Range(0, 8)
            .Select(_ => host.Client.PostAsJsonAsync("/api/v1/planning-runs", request)));
        var runs = await Task.WhenAll(responses.Select(item => item.Content.ReadFromJsonAsync<PlanningRunResult>()));

        Assert.All(responses, item => Assert.Equal(HttpStatusCode.Accepted, item.StatusCode));
        Assert.All(runs, Assert.NotNull);
        var runIds = runs.Select(item => item!.Id).Distinct().ToArray();
        Assert.Single(runIds);
        Assert.Equal(1, store.PlanningRunCount);
        Assert.Equal(1, store.PlanningRunCompletedOutboxEventCount);
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
                IdempotencyKey = "planning-run-package-decision",
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
                IdempotencyKey = "planning-run-invalid-decision",
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
                IdempotencyKey = "planning-run-persistence-unconfigured",
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
        private readonly object _sync = new();
        private readonly Dictionary<Guid, StoredPlanningRun> _runs = new();
        private readonly Dictionary<Guid, StoredPlanningPackage> _packages = new();
        private readonly Dictionary<string, Guid> _runIdsByIdempotencyKey = new(StringComparer.Ordinal);
        private int _planningRunCompletedOutboxEventCount;

        private InMemoryPlanningStore(
            IReadOnlyList<PlanningWorkOrderSnapshot> workOrders,
            IReadOnlyList<PlanningMajorEventSnapshot> majorEvents)
        {
            _workOrders = workOrders.ToList();
            _majorEvents = majorEvents.ToList();
        }

        public bool IsConfigured => true;

        public int PlanningRunCount
        {
            get
            {
                lock (_sync)
                {
                    return _runs.Count;
                }
            }
        }

        public int PlanningRunCompletedOutboxEventCount
        {
            get
            {
                lock (_sync)
                {
                    return _planningRunCompletedOutboxEventCount;
                }
            }
        }

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

        public Task<PlanningRunSaveResult> SavePlanningRunAsync(
            PlanningRun planningRun,
            IReadOnlyList<WorkOrderPackage> packages,
            IReadOnlyList<PackageItem> packageItems,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (!string.IsNullOrWhiteSpace(planningRun.IdempotencyKey)
                    && _runIdsByIdempotencyKey.TryGetValue(planningRun.IdempotencyKey, out var existingRunId))
                {
                    return Task.FromResult(PlanningRunSaveResult.Existing(_runs[existingRunId]));
                }

                var storedPackages = packages
                    .OrderBy(item => item.PackageNumber, StringComparer.Ordinal)
                    .Select(package => ToStoredPackage(package, packageItems))
                    .ToArray();
                var storedRun = new StoredPlanningRun(
                    planningRun.Id,
                    planningRun.RunNumber,
                    planningRun.IdempotencyKey,
                    planningRun.RequestHash,
                    planningRun.Status,
                    planningRun.Horizon,
                    planningRun.HorizonStartUtc,
                    planningRun.HorizonEndUtc,
                    planningRun.StartedAtUtc,
                    planningRun.CompletedAtUtc,
                    planningRun.RequestedBy,
                    storedPackages);

                _runs[storedRun.Id] = storedRun;
                if (!string.IsNullOrWhiteSpace(storedRun.IdempotencyKey))
                {
                    _runIdsByIdempotencyKey[storedRun.IdempotencyKey] = storedRun.Id;
                }

                foreach (var package in storedPackages)
                {
                    _packages[package.Id] = package;
                }

                _planningRunCompletedOutboxEventCount++;
                return Task.FromResult(PlanningRunSaveResult.CreatedRun());
            }
        }

        public Task<StoredPlanningRun?> FindPlanningRunByIdempotencyKeyAsync(
            string idempotencyKey,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                if (!_runIdsByIdempotencyKey.TryGetValue(idempotencyKey, out var runId))
                {
                    return Task.FromResult<StoredPlanningRun?>(null);
                }

                return Task.FromResult<StoredPlanningRun?>(_runs[runId]);
            }
        }

        public Task<StoredPlanningRun?> FindPlanningRunAsync(
            Guid planningRunId,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                _runs.TryGetValue(planningRunId, out var run);
                return Task.FromResult(run);
            }
        }

        public Task<StoredPlanningRun?> FindPlanningRunWithRecommendationsAsync(
            Guid planningRunId,
            CancellationToken cancellationToken)
        {
            lock (_sync)
            {
                _runs.TryGetValue(planningRunId, out var run);
                return Task.FromResult(run);
            }
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
