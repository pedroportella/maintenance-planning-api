using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace MaintenancePlanning.Api.Security;

public static class ApiAuthorization
{
    public const string AuthenticationScheme = "SyntheticTestBearer";

    public const string PlannerPolicy = "planner";
    public const string ImportsPolicy = "imports";
    public const string OperationsPolicy = "operations";

    public const string PlannerRole = "planner";
    public const string ImportsRole = "imports";
    public const string OperationsRole = "operations";

    public const string PlanningReadScope = "planning:read";
    public const string PlanningWriteScope = "planning:write";
    public const string ImportsWriteScope = "imports:write";
    public const string OperationsReadScope = "operations:read";

    public static void AddApiPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(
            PlannerPolicy,
            policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                HasRole(context, PlannerRole)
                || HasScope(context, PlanningReadScope)
                || HasScope(context, PlanningWriteScope)));

        options.AddPolicy(
            ImportsPolicy,
            policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                HasRole(context, ImportsRole) || HasScope(context, ImportsWriteScope)));

        options.AddPolicy(
            OperationsPolicy,
            policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                HasRole(context, OperationsRole) || HasScope(context, OperationsReadScope)));
    }

    private static bool HasRole(AuthorizationHandlerContext context, string role)
    {
        return context.User.Claims.Any(claim =>
            claim.Type == ClaimTypes.Role && string.Equals(claim.Value, role, StringComparison.Ordinal));
    }

    private static bool HasScope(AuthorizationHandlerContext context, string scope)
    {
        return context.User.Claims
            .Where(claim => claim.Type == "scope" || claim.Type == "scp")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(value => string.Equals(value, scope, StringComparison.Ordinal));
    }
}
