namespace MaintenancePlanning.Api.Security;

public static class TestTokenNames
{
    public const string PlannerReadOnly = "local-planner-read-token";
    public const string Planner = "local-planner-token";
    public const string Importer = "local-import-token";
    public const string Operations = "local-operations-token";
    public const string Reviewer = "local-reviewer-token";
}
