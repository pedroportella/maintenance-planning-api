using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace MaintenancePlanning.Api.Security;

public sealed class TestTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderNames.Authorization, out var authorizationValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!AuthenticationHeaderValue.TryParse(authorizationValues.ToString(), out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return Task.FromResult(AuthenticateResult.Fail("A valid bearer token is required."));
        }

        var principal = CreatePrincipal(authorization.Parameter);

        if (principal is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("The bearer token is not recognised for local review."));
        }

        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static ClaimsPrincipal? CreatePrincipal(string token)
    {
        return token switch
        {
            TestTokenNames.PlannerReadOnly => Principal(
                "local-planner-read",
                roles: Array.Empty<string>(),
                scopes: new[] { ApiAuthorization.PlanningReadScope }),
            TestTokenNames.Planner => Principal(
                "local-planner",
                roles: new[] { ApiAuthorization.PlannerRole },
                scopes: new[] { ApiAuthorization.PlanningReadScope, ApiAuthorization.PlanningWriteScope }),
            TestTokenNames.Importer => Principal(
                "local-importer",
                roles: new[] { ApiAuthorization.ImportsRole },
                scopes: new[] { ApiAuthorization.ImportsWriteScope }),
            TestTokenNames.Operations => Principal(
                "local-operations",
                roles: new[] { ApiAuthorization.OperationsRole },
                scopes: new[] { ApiAuthorization.OperationsReadScope }),
            TestTokenNames.Reviewer => Principal(
                "local-reviewer",
                roles: new[] { ApiAuthorization.PlannerRole, ApiAuthorization.ImportsRole, ApiAuthorization.OperationsRole },
                scopes: new[]
                {
                    ApiAuthorization.PlanningReadScope,
                    ApiAuthorization.PlanningWriteScope,
                    ApiAuthorization.ImportsWriteScope,
                    ApiAuthorization.OperationsReadScope
                }),
            _ => null
        };
    }

    private static ClaimsPrincipal Principal(string subject, IReadOnlyList<string> roles, IReadOnlyList<string> scopes)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subject),
            new(ClaimTypes.Name, subject),
            new("auth_mode", "synthetic-test-token"),
            new("scope", string.Join(' ', scopes))
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, ApiAuthorization.AuthenticationScheme));
    }
}
