using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MasterMusic.Ventas.Api.OpenApi;

public sealed class OperacionPedidosTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operacion,
        OpenApiOperationTransformerContext contexto, CancellationToken cancelacion)
    {
        var metadatos = contexto.Description.ActionDescriptor.EndpointMetadata;
        var autorizaciones = metadatos.OfType<IAuthorizeData>().ToList();
        if (autorizaciones.Count > 0 && !metadatos.OfType<IAllowAnonymous>().Any())
        {
            operacion.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", contexto.Document)] = []
                }
            ];
            var roles = autorizaciones.Where(a => !string.IsNullOrWhiteSpace(a.Roles))
                .Select(a => a.Roles);
            operacion.Description = $"{operacion.Description}\nRequiere JWT. Roles: {string.Join("; ", roles)}.".Trim();
            operacion.Responses ??= new OpenApiResponses();
            operacion.Responses.TryAdd("401", new OpenApiResponse
            {
                Description = "NO_AUTENTICADO: falta un JWT válido o ha vencido."
            });
            operacion.Responses.TryAdd("403", new OpenApiResponse
            {
                Description = "PROHIBIDO: el rol no permite realizar esta operación."
            });
        }

        var clave = operacion.Parameters?.OfType<OpenApiParameter>()
            .SingleOrDefault(p => p.In == ParameterLocation.Header
                && string.Equals(p.Name, "Idempotency-Key", StringComparison.OrdinalIgnoreCase));
        if (clave is not null)
        {
            clave.Required = true;
            clave.Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" };
            clave.Description = "UUID nuevo para cada operación. Ante un reintento, conserva " +
                "la misma clave y exactamente los mismos datos. No uses el UUID vacío.";
        }
        return Task.CompletedTask;
    }
}
