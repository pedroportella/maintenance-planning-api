using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MaintenancePlanning.Domain.Planning;

namespace MaintenancePlanning.Application.Planning;

public sealed class PlanningService(IPlanningStore store) : IPlanningService
{
    private const int StatusNotFound = 404;
    private const int StatusConflict = 409;
    private const int StatusUnprocessableEntity = 422;
    private const int StatusServiceUnavailable = 503;
    private const int DefaultHorizonDays = 14;
    private const int MaximumHorizonDays = 90;
    private const int DefaultWorkOrderPageSize = 25;
    private const int MaximumWorkOrderPageSize = 100;

    private static readonly PlanningRecommendationEngine Engine = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };
    private static readonly HashSet<string> DecisionTypes =
        Enum.GetNames<PlannerDecisionType>().ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> WorkOrderSorts = new(StringComparer.Ordinal)
    {
        "dueAtUtc",
        "-dueAtUtc",
        "priority",
        "-priority",
        "requiredStartUtc",
        "-requiredStartUtc",
        "updatedAtUtc",
        "-updatedAtUtc",
        "workOrderNumber",
        "-workOrderNumber"
    };

    public async Task<PlanningProcessingOutcome<PlanningRunResult>> CreatePlanningRunAsync(
        CreatePlanningRunRequest request,
        CancellationToken cancellationToken)
    {
        if (!store.IsConfigured)
        {
            return PlanningProcessingOutcome<PlanningRunResult>.Failed(StoreUnavailable());
        }

        var normalized = NormalizeCreateRequest(request);
        if (normalized.Problem is not null)
        {
            return PlanningProcessingOutcome<PlanningRunResult>.Failed(normalized.Problem);
        }

        var existing = await store.FindPlanningRunByIdempotencyKeyAsync(
            normalized.IdempotencyKey,
            cancellationToken);
        var existingOutcome = TryBuildIdempotencyOutcome(existing, normalized.RequestHash);
        if (existingOutcome is not null)
        {
            return existingOutcome;
        }

        var startedAtUtc = DateTimeOffset.UtcNow;
        var runNumber = BuildRunNumber(startedAtUtc);
        var snapshot = await store.LoadCandidateSnapshotAsync(
            normalized.HorizonStartUtc,
            normalized.HorizonEndUtc,
            cancellationToken);
        var drafts = Engine.BuildPackageDrafts(snapshot, runNumber);
        var planningRun = new PlanningRun
        {
            Id = Guid.NewGuid(),
            RunNumber = runNumber,
            IdempotencyKey = normalized.IdempotencyKey,
            RequestHash = normalized.RequestHash,
            Status = PlanningRunStatus.Completed,
            Horizon = normalized.Horizon,
            HorizonStartUtc = normalized.HorizonStartUtc,
            HorizonEndUtc = normalized.HorizonEndUtc,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            RequestedBy = normalized.RequestedBy
        };
        var packages = drafts.Select(draft => new WorkOrderPackage
        {
            Id = draft.Id,
            PlanningRunId = planningRun.Id,
            PackageNumber = draft.PackageNumber,
            Title = draft.Title,
            Status = PackageStatus.Recommended,
            EstimatedHours = draft.EstimatedHours,
            PlannedStartUtc = draft.PlannedStartUtc,
            PlannedEndUtc = draft.PlannedEndUtc,
            RecommendationRationale = draft.RecommendationRationale
        }).ToArray();
        var items = drafts
            .SelectMany(draft => draft.Items.Select(item => new PackageItem
            {
                Id = item.Id,
                WorkOrderPackageId = draft.Id,
                WorkOrderId = item.WorkOrderId,
                Sequence = item.Sequence,
                FitReason = item.FitReason
            }))
            .ToArray();

        var saveResult = await store.SavePlanningRunAsync(planningRun, packages, items, cancellationToken);
        if (saveResult.ExistingRun is not null)
        {
            return TryBuildIdempotencyOutcome(saveResult.ExistingRun, normalized.RequestHash)
                ?? PlanningProcessingOutcome<PlanningRunResult>.Failed(IdempotencyConflict());
        }

        return PlanningProcessingOutcome<PlanningRunResult>.Success(ToRunResult(planningRun, drafts));
    }

    public async Task<PlanningProcessingOutcome<PlanningRunResult>> GetPlanningRunAsync(
        Guid planningRunId,
        CancellationToken cancellationToken)
    {
        if (!store.IsConfigured)
        {
            return PlanningProcessingOutcome<PlanningRunResult>.Failed(StoreUnavailable());
        }

        var run = await store.FindPlanningRunAsync(planningRunId, cancellationToken);

        return run is null
            ? PlanningProcessingOutcome<PlanningRunResult>.Failed(NotFound("Planning run was not found.", "planning-run-not-found"))
            : PlanningProcessingOutcome<PlanningRunResult>.Success(ToRunResult(run));
    }

    public async Task<PlanningProcessingOutcome<PlanningRecommendationsResult>> GetRecommendationsAsync(
        Guid planningRunId,
        CancellationToken cancellationToken)
    {
        if (!store.IsConfigured)
        {
            return PlanningProcessingOutcome<PlanningRecommendationsResult>.Failed(StoreUnavailable());
        }

        var run = await store.FindPlanningRunWithRecommendationsAsync(planningRunId, cancellationToken);

        return run is null
            ? PlanningProcessingOutcome<PlanningRecommendationsResult>.Failed(NotFound("Planning run was not found.", "planning-run-not-found"))
            : PlanningProcessingOutcome<PlanningRecommendationsResult>.Success(ToRecommendationsResult(run));
    }

    public async Task<PlanningProcessingOutcome<WorkOrderQueryResult>> QueryWorkOrdersAsync(
        WorkOrderQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (!store.IsConfigured)
        {
            return PlanningProcessingOutcome<WorkOrderQueryResult>.Failed(StoreUnavailable());
        }

        var normalized = NormalizeWorkOrderQuery(request);
        if (normalized.Problem is not null || normalized.Spec is null)
        {
            return PlanningProcessingOutcome<WorkOrderQueryResult>.Failed(normalized.Problem ?? ValidationFailed(new()));
        }

        var page = await store.QueryWorkOrdersAsync(normalized.Spec, cancellationToken);

        return PlanningProcessingOutcome<WorkOrderQueryResult>.Success(new WorkOrderQueryResult(
            page.Items.Select(ToWorkOrderSummaryResult).ToArray(),
            page.NextOffset is null ? null : EncodeCursor(page.NextOffset.Value),
            normalized.Spec.PageSize,
            normalized.Sort,
            normalized.AppliedFilters,
            DateTimeOffset.UtcNow));
    }

    public async Task<PlanningProcessingOutcome<WorkOrderDetailResult>> GetWorkOrderAsync(
        Guid workOrderId,
        CancellationToken cancellationToken)
    {
        if (!store.IsConfigured)
        {
            return PlanningProcessingOutcome<WorkOrderDetailResult>.Failed(StoreUnavailable());
        }

        var workOrder = await store.FindWorkOrderAsync(workOrderId, cancellationToken);

        return workOrder is null
            ? PlanningProcessingOutcome<WorkOrderDetailResult>.Failed(NotFound("Work order was not found.", "work-order-not-found"))
            : PlanningProcessingOutcome<WorkOrderDetailResult>.Success(ToWorkOrderDetailResult(workOrder));
    }

    public async Task<PlanningProcessingOutcome<PackageDecisionResult>> RecordPackageDecisionAsync(
        Guid packageId,
        RecordPackageDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (!store.IsConfigured)
        {
            return PlanningProcessingOutcome<PackageDecisionResult>.Failed(StoreUnavailable());
        }

        var validation = ValidateDecisionRequest(request);
        if (validation.Count > 0)
        {
            return PlanningProcessingOutcome<PackageDecisionResult>.Failed(ValidationFailed(validation));
        }

        var package = await store.FindPackageAsync(packageId, cancellationToken);
        if (package is null)
        {
            return PlanningProcessingOutcome<PackageDecisionResult>.Failed(NotFound("Package was not found.", "package-not-found"));
        }

        var requestedWorkOrderIds = request.WorkOrderIds.Distinct().ToArray();
        var packageWorkOrderIds = package.Items.Select(item => item.WorkOrder.Id).ToHashSet();
        if (requestedWorkOrderIds.Length > 0 && requestedWorkOrderIds.Any(item => !packageWorkOrderIds.Contains(item)))
        {
            return PlanningProcessingOutcome<PackageDecisionResult>.Failed(ValidationFailed(new Dictionary<string, List<string>>(StringComparer.Ordinal)
            {
                ["workOrderIds"] = new() { "Every workOrderId must belong to the package." }
            }));
        }

        var decision = Enum.Parse<PlannerDecisionType>(request.Decision.Trim());
        var workOrderIds = requestedWorkOrderIds.Length > 0
            ? requestedWorkOrderIds
            : package.Items.Select(item => item.WorkOrder.Id).ToArray();
        var decisions = await store.SavePackageDecisionAsync(
            packageId,
            decision,
            request.ReasonCode.Trim(),
            CleanOptional(request.Notes),
            CleanOptional(request.DecidedBy) ?? "local-review",
            DateTimeOffset.UtcNow,
            workOrderIds,
            cancellationToken);
        var packageStatus = ToPackageStatus(decision).ToString();

        return PlanningProcessingOutcome<PackageDecisionResult>.Success(new PackageDecisionResult(
            package.Id,
            package.PackageNumber,
            packageStatus,
            decisions.Select(ToDecisionResult).ToArray()));
    }

    private static NormalizedCreatePlanningRunRequest NormalizeCreateRequest(CreatePlanningRunRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var start = request.HorizonStartUtc ?? DateTimeOffset.UtcNow.Date;
        var end = request.HorizonEndUtc ?? start.AddDays(DefaultHorizonDays);
        var horizon = string.IsNullOrWhiteSpace(request.Horizon) ? "two-week" : request.Horizon.Trim();
        var requestedBy = CleanOptional(request.RequestedBy) ?? "local-review";
        var idempotencyKey = CleanOptional(request.IdempotencyKey);

        RequireString(errors, "idempotencyKey", idempotencyKey, 160);
        RequireString(errors, "horizon", horizon, 80);
        RequireString(errors, "requestedBy", requestedBy, 120);

        if (end <= start)
        {
            AddError(errors, "horizonEndUtc", "Must be later than horizonStartUtc.");
        }

        if (end - start > TimeSpan.FromDays(MaximumHorizonDays))
        {
            AddError(errors, "horizonEndUtc", $"Must be within {MaximumHorizonDays.ToString(CultureInfo.InvariantCulture)} days of horizonStartUtc.");
        }

        var requestHash = HashObject(new PlanningRunRequestFingerprint(
            horizon,
            start,
            end,
            requestedBy));

        return errors.Count > 0
            ? new NormalizedCreatePlanningRunRequest(horizon, start, end, requestedBy, idempotencyKey ?? "", requestHash, ValidationFailed(errors))
            : new NormalizedCreatePlanningRunRequest(horizon, start, end, requestedBy, idempotencyKey!, requestHash, null);
    }

    private static Dictionary<string, List<string>> ValidateDecisionRequest(RecordPackageDecisionRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        RequireEnum(errors, "decision", request.Decision, DecisionTypes);
        RequireString(errors, "reasonCode", request.ReasonCode, 80);
        OptionalString(errors, "notes", request.Notes, 500);
        OptionalString(errors, "decidedBy", request.DecidedBy, 120);

        return errors;
    }

    private static NormalizedWorkOrderQueryRequest NormalizeWorkOrderQuery(WorkOrderQueryRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var pageSize = request.PageSize ?? DefaultWorkOrderPageSize;
        if (pageSize < 1 || pageSize > MaximumWorkOrderPageSize)
        {
            AddError(
                errors,
                "pageSize",
                $"Must be between 1 and {MaximumWorkOrderPageSize.ToString(CultureInfo.InvariantCulture)}.");
        }

        var offset = 0;
        if (!string.IsNullOrWhiteSpace(request.Cursor) && !TryDecodeCursor(request.Cursor.Trim(), out offset))
        {
            AddError(errors, "cursor", "Contains an unsupported cursor value.");
        }

        var sort = string.IsNullOrWhiteSpace(request.Sort) ? "dueAtUtc" : request.Sort.Trim();
        if (!WorkOrderSorts.Contains(sort))
        {
            AddError(errors, "sort", "Contains an unsupported sort value.");
        }

        var readiness = ParseOptionalEnum<SourceDataReadiness>(errors, "readiness", request.Readiness);
        var status = ParseOptionalEnum<WorkOrderLifecycleStatus>(errors, "status", request.Status);
        var priority = CleanOptional(request.Priority);
        var functionalLocation = CleanOptional(request.FunctionalLocation);
        OptionalString(errors, "priority", priority, 40);
        OptionalString(errors, "functionalLocation", functionalLocation, 120);

        if (request.UpdatedSinceUtc is not null
            && request.UpdatedBeforeUtc is not null
            && request.UpdatedBeforeUtc <= request.UpdatedSinceUtc)
        {
            AddError(errors, "updatedBeforeUtc", "Must be later than updatedSinceUtc.");
        }

        var appliedFilters = new WorkOrderQueryFilters(
            Backlog: request.Backlog ?? true,
            Priority: priority,
            FunctionalLocation: functionalLocation,
            Readiness: readiness?.ToString(),
            Status: status?.ToString(),
            UpdatedSinceUtc: request.UpdatedSinceUtc,
            UpdatedBeforeUtc: request.UpdatedBeforeUtc);

        if (errors.Count > 0)
        {
            return new NormalizedWorkOrderQueryRequest(null, sort, appliedFilters, ValidationFailed(errors));
        }

        var sortDescending = sort.StartsWith("-", StringComparison.Ordinal);
        var sortField = sortDescending ? sort[1..] : sort;
        var spec = new WorkOrderQuerySpec(
            offset,
            pageSize,
            appliedFilters.Backlog,
            priority,
            functionalLocation,
            readiness,
            status,
            request.UpdatedSinceUtc,
            request.UpdatedBeforeUtc,
            sortField,
            sortDescending);

        return new NormalizedWorkOrderQueryRequest(spec, sort, appliedFilters, null);
    }

    private static PlanningRunResult ToRunResult(PlanningRun run, IReadOnlyList<PlanningPackageDraft> drafts)
    {
        return new PlanningRunResult(
            run.Id,
            run.RunNumber,
            run.Status.ToString(),
            run.Horizon,
            run.HorizonStartUtc,
            run.HorizonEndUtc,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.RequestedBy,
            drafts.Count,
            drafts.Count(item => item.Actionability == "ready-now"),
            drafts.Count(item => item.Actionability == "blocked"));
    }

    private static PlanningRunResult ToRunResult(StoredPlanningRun run)
    {
        var recommendations = run.Packages.Select(ToPackageRecommendation).ToArray();

        return new PlanningRunResult(
            run.Id,
            run.RunNumber,
            run.Status.ToString(),
            run.Horizon,
            run.HorizonStartUtc,
            run.HorizonEndUtc,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.RequestedBy,
            run.Packages.Count,
            recommendations.Count(item => item.Actionability == "ready-now"),
            recommendations.Count(item => item.Actionability == "blocked"));
    }

    private static PlanningRecommendationsResult ToRecommendationsResult(StoredPlanningRun run)
    {
        return new PlanningRecommendationsResult(
            run.Id,
            run.RunNumber,
            run.Status.ToString(),
            run.Packages.Select(ToPackageRecommendation).ToArray());
    }

    private static PackageRecommendationResult ToPackageRecommendation(StoredPlanningPackage package)
    {
        var workOrders = package.Items
            .OrderBy(item => item.Sequence)
            .Select(item => item.WorkOrder)
            .ToArray();
        var profile = Engine.BuildProfile(workOrders, package.PlannedStartUtc);

        return new PackageRecommendationResult(
            package.Id,
            package.PackageNumber,
            package.Title,
            package.Status.ToString(),
            profile.Score,
            profile.Actionability,
            package.EstimatedHours,
            package.PlannedStartUtc,
            package.PlannedEndUtc,
            package.RecommendationRationale,
            profile.SourceDataReadiness,
            profile.Blockers,
            package.Items
                .OrderBy(item => item.Sequence)
                .Select(item => ToWorkOrderResult(item.WorkOrder))
                .ToArray(),
            package.Decisions
                .OrderByDescending(item => item.DecidedAtUtc)
                .Select(ToDecisionResult)
                .ToArray());
    }

    private static WorkOrderSummaryResult ToWorkOrderSummaryResult(PlanningWorkOrderSnapshot workOrder)
    {
        return new WorkOrderSummaryResult(
            workOrder.Id,
            workOrder.SourceSystem,
            workOrder.SourceId,
            workOrder.WorkOrderNumber,
            workOrder.Title,
            workOrder.WorkType,
            workOrder.Priority,
            workOrder.Status.ToString(),
            workOrder.Readiness.ToString(),
            ToIssueSummary(workOrder),
            workOrder.RequiredStartUtc,
            workOrder.DueAtUtc,
            workOrder.ScheduledStartUtc,
            workOrder.EstimatedHours,
            workOrder.SourceUpdatedAtUtc,
            workOrder.ImportedAtUtc,
            workOrder.AssetNumber,
            workOrder.AssetName,
            workOrder.AssetCriticality,
            workOrder.FunctionalLocationCode,
            workOrder.FunctionalLocationName);
    }

    private static WorkOrderDetailResult ToWorkOrderDetailResult(PlanningWorkOrderSnapshot workOrder)
    {
        return new WorkOrderDetailResult(
            workOrder.Id,
            workOrder.SourceSystem,
            workOrder.SourceId,
            workOrder.WorkOrderNumber,
            workOrder.Title,
            workOrder.WorkType,
            workOrder.Priority,
            workOrder.Status.ToString(),
            workOrder.Readiness.ToString(),
            ToIssueSummary(workOrder),
            workOrder.RequiredStartUtc,
            workOrder.DueAtUtc,
            workOrder.ScheduledStartUtc,
            workOrder.EstimatedHours,
            workOrder.SourceUpdatedAtUtc,
            workOrder.ImportedAtUtc,
            workOrder.AssetNumber,
            workOrder.AssetName,
            workOrder.AssetCriticality,
            workOrder.FunctionalLocationCode,
            workOrder.FunctionalLocationName);
    }

    private static RecommendationWorkOrderResult ToWorkOrderResult(PlanningWorkOrderSnapshot workOrder)
    {
        return new RecommendationWorkOrderResult(
            workOrder.Id,
            workOrder.SourceSystem,
            workOrder.SourceId,
            workOrder.WorkOrderNumber,
            workOrder.Title,
            workOrder.WorkType,
            workOrder.Priority,
            workOrder.Status.ToString(),
            workOrder.Readiness.ToString(),
            workOrder.ReadinessIssueCode,
            workOrder.ReadinessIssueDetail,
            workOrder.RequiredStartUtc,
            workOrder.DueAtUtc,
            workOrder.ScheduledStartUtc,
            workOrder.EstimatedHours,
            workOrder.AssetNumber,
            workOrder.AssetName,
            workOrder.FunctionalLocationCode,
            workOrder.FunctionalLocationName);
    }

    private static SourceDataIssueSummary? ToIssueSummary(PlanningWorkOrderSnapshot workOrder)
    {
        if (string.IsNullOrWhiteSpace(workOrder.ReadinessIssueCode)
            && string.IsNullOrWhiteSpace(workOrder.ReadinessIssueDetail))
        {
            return null;
        }

        return new SourceDataIssueSummary(
            CleanOptional(workOrder.ReadinessIssueCode) ?? "source-data-review",
            CleanOptional(workOrder.ReadinessIssueDetail) ?? "Source data requires planner review.");
    }

    private static PlannerDecisionResult ToDecisionResult(StoredPlannerDecision decision)
    {
        return new PlannerDecisionResult(
            decision.Id,
            decision.PackageId,
            decision.WorkOrderId,
            decision.Decision.ToString(),
            decision.ReasonCode,
            decision.Notes,
            decision.DecidedAtUtc,
            decision.DecidedBy);
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

    private static PlanningProblem StoreUnavailable()
    {
        return new PlanningProblem(
            StatusServiceUnavailable,
            "Planning persistence is not configured.",
            "Configure the local database before using planning endpoints.",
            "planning-persistence-not-configured");
    }

    private static PlanningProblem NotFound(string detail, string code)
    {
        return new PlanningProblem(StatusNotFound, "Planning resource was not found.", detail, code);
    }

    private static PlanningProblem IdempotencyConflict()
    {
        return new PlanningProblem(
            StatusConflict,
            "Planning idempotency conflict.",
            "The idempotency key has already been used with a different planning request.",
            "planning-idempotency-conflict");
    }

    private static PlanningProblem ValidationFailed(Dictionary<string, List<string>> errors)
    {
        return new PlanningProblem(
            StatusUnprocessableEntity,
            "Planning request validation failed.",
            "One or more planning fields are invalid.",
            "planning-validation-failed",
            errors.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.Ordinal));
    }

    private static string BuildRunNumber(DateTimeOffset startedAtUtc)
    {
        return $"RUN-{startedAtUtc:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..31];
    }

    private static PlanningProcessingOutcome<PlanningRunResult>? TryBuildIdempotencyOutcome(
        StoredPlanningRun? existing,
        string requestHash)
    {
        if (existing is null)
        {
            return null;
        }

        return string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal)
            ? PlanningProcessingOutcome<PlanningRunResult>.Success(ToRunResult(existing))
            : PlanningProcessingOutcome<PlanningRunResult>.Failed(IdempotencyConflict());
    }

    private static string HashObject<T>(T value)
    {
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions)))
            .ToLowerInvariant();
    }

    private static void RequireString(
        Dictionary<string, List<string>> errors,
        string path,
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, path, "Field is required.");
            return;
        }

        if (value.Trim().Length > maxLength)
        {
            AddError(errors, path, $"Must be {maxLength.ToString(CultureInfo.InvariantCulture)} characters or fewer.");
        }
    }

    private static void RequireEnum(
        Dictionary<string, List<string>> errors,
        string path,
        string? value,
        IReadOnlySet<string> allowedValues)
    {
        RequireString(errors, path, value, 80);

        if (!string.IsNullOrWhiteSpace(value) && !allowedValues.Contains(value.Trim()))
        {
            AddError(errors, path, "Contains an unsupported value.");
        }
    }

    private static T? ParseOptionalEnum<T>(
        Dictionary<string, List<string>> errors,
        string path,
        string? value)
        where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<T>(value.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        AddError(errors, path, "Contains an unsupported value.");
        return null;
    }

    private static void OptionalString(
        Dictionary<string, List<string>> errors,
        string path,
        string? value,
        int maxLength)
    {
        if (value is null || value.Trim().Length <= maxLength)
        {
            return;
        }

        AddError(errors, path, $"Must be {maxLength.ToString(CultureInfo.InvariantCulture)} characters or fewer.");
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static void AddError(Dictionary<string, List<string>> errors, string path, string message)
    {
        if (!errors.TryGetValue(path, out var messages))
        {
            messages = new List<string>();
            errors[path] = messages;
        }

        messages.Add(message);
    }

    private static string EncodeCursor(int offset)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(offset.ToString(CultureInfo.InvariantCulture)))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool TryDecodeCursor(string cursor, out int offset)
    {
        offset = 0;

        var padded = cursor.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');

        try
        {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out offset) && offset >= 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private sealed record NormalizedCreatePlanningRunRequest(
        string Horizon,
        DateTimeOffset HorizonStartUtc,
        DateTimeOffset HorizonEndUtc,
        string RequestedBy,
        string IdempotencyKey,
        string RequestHash,
        PlanningProblem? Problem);

    private sealed record PlanningRunRequestFingerprint(
        string Horizon,
        DateTimeOffset HorizonStartUtc,
        DateTimeOffset HorizonEndUtc,
        string RequestedBy);

    private sealed record NormalizedWorkOrderQueryRequest(
        WorkOrderQuerySpec? Spec,
        string Sort,
        WorkOrderQueryFilters AppliedFilters,
        PlanningProblem? Problem);
}
