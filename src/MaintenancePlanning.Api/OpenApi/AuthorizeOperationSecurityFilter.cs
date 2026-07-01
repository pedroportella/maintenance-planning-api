using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MaintenancePlanning.Api.OpenApi;

public sealed class AuthorizeOperationSecurityFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var endpointMetadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
        var allowsAnonymous = endpointMetadata.OfType<IAllowAnonymous>().Any();
        var requiresAuthorization = endpointMetadata.OfType<IAuthorizeData>().Any();

        if (allowsAnonymous || !requiresAuthorization)
        {
            return;
        }

        operation.Security ??= new List<OpenApiSecurityRequirement>();

        if (operation.Security.Any(RequiresBearerSecurityScheme))
        {
            return;
        }

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = OpenApiDocumentMetadata.BearerSecuritySchemeId
                }
            }] = Array.Empty<string>()
        });
    }

    private static bool RequiresBearerSecurityScheme(OpenApiSecurityRequirement requirement)
    {
        return requirement.Keys.Any(scheme =>
            string.Equals(
                scheme.Reference?.Id,
                OpenApiDocumentMetadata.BearerSecuritySchemeId,
                StringComparison.Ordinal));
    }
}
