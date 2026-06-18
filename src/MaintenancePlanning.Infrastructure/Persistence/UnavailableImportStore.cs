using MaintenancePlanning.Application.Imports;

namespace MaintenancePlanning.Infrastructure.Persistence;

internal sealed class UnavailableImportStore : IImportStore
{
    public bool IsConfigured => false;

    public Task<StoredImport?> FindImportAsync(
        string sourceSystem,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        Task.FromResult<StoredImport?>(null);

    public Task<StoredImport?> FindLatestImportAsync(CancellationToken cancellationToken) =>
        Task.FromResult<StoredImport?>(null);

    public Task<bool> HasEventIdempotencyKeyAsync(
        string sourceSystem,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> HasEventIdAsync(
        string eventId,
        CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<StoredSourceRecord?> FindWorkOrderAsync(
        string sourceSystem,
        string sourceId,
        CancellationToken cancellationToken) =>
        Task.FromResult<StoredSourceRecord?>(null);

    public Task SaveImportAsync(
        ImportPersistenceBatch batch,
        CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Import persistence is not configured.");
}
