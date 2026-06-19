using System.Security.Cryptography;
using System.Text;
using MaintenancePlanning.Application.Imports;

namespace MaintenancePlanning.Application.Eventing;

public sealed class DeadLetterReplayService(
    IDeadLetterReplayClient replayClient,
    IImportStore importStore)
    : IDeadLetterReplayService
{
    private const int StatusServiceUnavailable = 503;
    private const int StatusUnprocessableEntity = 422;
    private const string SourceSystem = "eventing";
    private const string ImportKind = "dlq-replay";
    private const string CompletedStatus = "Completed";

    public async Task<DeadLetterReplayOutcome> StartReplayAsync(
        StartDeadLetterReplayRequest request,
        CancellationToken cancellationToken)
    {
        if (!importStore.IsConfigured)
        {
            return DeadLetterReplayOutcome.Failed(Unavailable(
                "Replay audit store is not configured.",
                "eventing-audit-not-configured"));
        }

        if (!replayClient.IsConfigured)
        {
            return DeadLetterReplayOutcome.Failed(Unavailable(
                "Dead-letter replay is not configured.",
                "dead-letter-replay-not-configured"));
        }

        var normalized = Normalize(request);
        if (normalized.Problem is not null)
        {
            return DeadLetterReplayOutcome.Failed(normalized.Problem);
        }

        var replayTask = await replayClient.StartReplayAsync(
            normalized.MaxMessagesPerSecond,
            cancellationToken);
        var requestedAtUtc = DateTimeOffset.UtcNow;
        var auditId = Guid.NewGuid();

        await importStore.SaveImportAsync(
            new ImportPersistenceBatch(
                new ImportAuditRecord(
                    auditId,
                    SourceSystem,
                    ImportKind,
                    $"dlq-replay:{auditId:N}",
                    HashText($"{normalized.ReasonCode}|{normalized.RequestedBy}|{normalized.MaxMessagesPerSecond}"),
                    CompletedStatus,
                    0,
                    0,
                    0,
                    0,
                    0,
                    requestedAtUtc,
                    requestedAtUtc,
                    null),
                Array.Empty<IntegrationEventAuditRecord>(),
                Array.Empty<WorkOrderImportRecord>(),
                Array.Empty<MajorEventImportRecord>()),
            cancellationToken);

        return DeadLetterReplayOutcome.Success(new DeadLetterReplayResult(
            auditId,
            "started",
            replayTask.TaskHandle,
            replayTask.SourceQueueArn,
            replayTask.DestinationQueueArn,
            normalized.ReasonCode,
            normalized.RequestedBy,
            requestedAtUtc));
    }

    private static NormalizedReplayRequest Normalize(StartDeadLetterReplayRequest request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var reasonCode = CleanOptional(request.ReasonCode);
        var requestedBy = CleanOptional(request.RequestedBy) ?? "local-review";

        RequireString(errors, "reasonCode", reasonCode, 80);
        RequireString(errors, "requestedBy", requestedBy, 120);

        if (request.MaxMessagesPerSecond is <= 0 or > 500)
        {
            AddError(errors, "maxMessagesPerSecond", "Must be between 1 and 500 when provided.");
        }

        return errors.Count > 0
            ? new NormalizedReplayRequest(
                reasonCode ?? "",
                requestedBy,
                request.MaxMessagesPerSecond,
                ValidationFailed(errors))
            : new NormalizedReplayRequest(reasonCode!, requestedBy, request.MaxMessagesPerSecond, null);
    }

    private static DeadLetterReplayProblem ValidationFailed(Dictionary<string, List<string>> errors)
    {
        return new DeadLetterReplayProblem(
            StatusUnprocessableEntity,
            "Dead-letter replay request is invalid.",
            "Replay could not be started because the request failed validation.",
            "dead-letter-replay-validation-failed",
            errors.ToDictionary(
                item => item.Key,
                item => item.Value.ToArray(),
                StringComparer.Ordinal));
    }

    private static DeadLetterReplayProblem Unavailable(string detail, string code)
    {
        return new DeadLetterReplayProblem(
            StatusServiceUnavailable,
            "Dead-letter replay is unavailable.",
            detail,
            code);
    }

    private static void RequireString(
        Dictionary<string, List<string>> errors,
        string field,
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, field, "Value is required.");
            return;
        }

        if (value.Trim().Length > maxLength)
        {
            AddError(errors, field, $"Must be {maxLength} characters or fewer.");
        }
    }

    private static void AddError(
        Dictionary<string, List<string>> errors,
        string field,
        string message)
    {
        if (!errors.TryGetValue(field, out var fieldErrors))
        {
            fieldErrors = new List<string>();
            errors[field] = fieldErrors;
        }

        fieldErrors.Add(message);
    }

    private static string? CleanOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string HashText(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private sealed record NormalizedReplayRequest(
        string ReasonCode,
        string RequestedBy,
        int? MaxMessagesPerSecond,
        DeadLetterReplayProblem? Problem);
}
