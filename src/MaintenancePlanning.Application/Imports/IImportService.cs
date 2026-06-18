namespace MaintenancePlanning.Application.Imports;

public interface IImportService
{
    Task<ImportProcessingOutcome> ImportSourceWorkOrdersAsync(
        SourceWorkOrderImportRequest request,
        CancellationToken cancellationToken);

    Task<ImportProcessingOutcome> ImportMaintenanceEventsAsync(
        MaintenanceEventImportRequest request,
        CancellationToken cancellationToken);
}

public sealed record ImportProcessingOutcome(
    ImportResult? Result,
    ImportProblem? Problem)
{
    public static ImportProcessingOutcome Success(ImportResult result) => new(result, null);

    public static ImportProcessingOutcome Failed(ImportProblem problem) => new(null, problem);
}

public sealed record ImportProblem(
    int StatusCode,
    string Title,
    string Detail,
    string Code,
    IReadOnlyDictionary<string, string[]>? Errors = null);
