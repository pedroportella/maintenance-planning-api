using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MaintenancePlanning.Api.OpenApi;

public sealed class OpenApiTagDescriptionsDocumentFilter : IDocumentFilter
{
    private static readonly IReadOnlyDictionary<string, string> TagDescriptions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Health"] = "Public startup, liveness and readiness probes for local review and deployment checks.",
            ["Imports"] = "Protected synthetic source-system-shaped import feeds for work orders and maintenance events.",
            ["Operations"] = "Protected operational readiness, posture and dead-letter replay controls.",
            ["Planning"] = "Protected planning runs, package recommendations and audited package decisions.",
            ["Work Orders"] = "Protected planner-facing work-order backlog and detail queries."
        };

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Tags = TagDescriptions
            .Select(item => new OpenApiTag
            {
                Name = item.Key,
                Description = item.Value
            })
            .ToList();
    }
}
