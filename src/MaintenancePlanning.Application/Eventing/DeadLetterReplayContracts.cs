namespace MaintenancePlanning.Application.Eventing;

public interface IDeadLetterReplayService
{
    Task<DeadLetterReplayOutcome> StartReplayAsync(
        StartDeadLetterReplayRequest request,
        CancellationToken cancellationToken);
}

public interface IDeadLetterReplayClient
{
    bool IsConfigured { get; }

    Task<DeadLetterMoveTask> StartReplayAsync(
        int? maxMessagesPerSecond,
        CancellationToken cancellationToken);
}

public sealed class StartDeadLetterReplayRequest
{
    public string ReasonCode { get; init; } = "";

    public string? RequestedBy { get; init; }

    public int? MaxMessagesPerSecond { get; init; }
}

public sealed record DeadLetterReplayResult(
    Guid AuditId,
    string Status,
    string ReplayTaskHandle,
    string SourceQueueArn,
    string DestinationQueueArn,
    string ReasonCode,
    string RequestedBy,
    DateTimeOffset RequestedAtUtc);

public sealed record DeadLetterMoveTask(
    string TaskHandle,
    string SourceQueueArn,
    string DestinationQueueArn);

public sealed record DeadLetterReplayProblem(
    int StatusCode,
    string Title,
    string Detail,
    string Code,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record DeadLetterReplayOutcome(
    DeadLetterReplayResult? Result,
    DeadLetterReplayProblem? Problem)
{
    public static DeadLetterReplayOutcome Success(DeadLetterReplayResult result) => new(result, null);

    public static DeadLetterReplayOutcome Failed(DeadLetterReplayProblem problem) => new(null, problem);
}
