namespace MaintenancePlanning.Domain.Planning;

public enum IntegrationEventStatus
{
    Received = 0,
    Accepted = 1,
    Rejected = 2
}

public enum IntegrationImportStatus
{
    Received = 0,
    Completed = 1,
    Failed = 2
}

public enum OutboxEventStatus
{
    Pending = 0,
    Published = 1,
    Failed = 2
}

public enum PackageStatus
{
    Recommended = 0,
    Accepted = 1,
    Rejected = 2,
    Deferred = 3
}

public enum PlannerDecisionType
{
    Accepted = 0,
    Rejected = 1,
    Deferred = 2
}

public enum PlanningRunStatus
{
    Started = 0,
    Completed = 1,
    Failed = 2
}

public enum SourceDataReadiness
{
    Ready = 0,
    NeedsReview = 1,
    Blocked = 2
}

public enum WorkOrderLifecycleStatus
{
    Imported = 0,
    ReadyForPlanning = 1,
    Packaged = 2,
    DecisionRecorded = 3,
    Deferred = 4,
    Closed = 5
}
