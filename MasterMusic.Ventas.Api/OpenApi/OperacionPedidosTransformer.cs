using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MasterMusic.Ventas.Api.OpenApi;

public sealed class OperacionPedidosTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(OpenApiOperation operacion,
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
        // Un resultado 202 contiene una operación recuperable, no un pedido confirmado.
        if (operacion.Responses is not null)
        {
            var problema = await contexto.GetOrCreateSchemaAsync(typeof(ProblemDetails),
                cancellationToken: cancelacion);
            foreach (var (codigo, definicion) in operacion.Responses)
            {
                if (definicion is not OpenApiResponse respuesta) continue;
                if (int.TryParse(codigo, out var estado) && estado >= 400)
                {
                    respuesta.Description = codigo switch
                    {
                        "400" => "VALIDACION: datos o parámetros inválidos. Puede incluir errors por campo.",
                        "401" => "NO_AUTENTICADO: se requiere un JWT válido.",
                        "403" => "PROHIBIDO: el rol o la identidad no permite la operación.",
                        "404" => "NO_ENCONTRADO: recurso inexistente o ajeno a la cuenta.",
                        "409" => "Conflicto de stock, precio, idempotencia o estado del pedido; consulta codigo.",
                        "503" => "SERVICIO_NO_DISPONIBLE: consulta la operación antes de reintentar.",
                        _ => respuesta.Description
                    };
                    respuesta.Content = new Dictionary<string, OpenApiMediaType>
                    {
                        ["application/problem+json"] = new() { Schema = problema }
                    };
                }
                if (codigo is "201" or "202")
                {
                    respuesta.Headers ??= new Dictionary<string, IOpenApiHeader>();
                    respuesta.Headers["Location"] = new OpenApiHeader
                    {
                        Description = codigo == "201" ? "Ruta del pedido creado." : "Ruta para consultar la operación.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri-reference" }
                    };
                }
                if (codigo == "201")
                    respuesta.Description = "Pedido creado en estado Pendiente; stock descontado una sola vez.";
                if (codigo == "202")
                {
                    respuesta.Description = "Resultado aún incierto. Conserva la clave y consulta o recupera la operación.";
                    respuesta.Headers!["Retry-After"] = new OpenApiHeader
                    {
                        Description = "Segundos sugeridos antes de consultar de nuevo: 5.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Minimum = "0" }
                    };
                }
            }
        }
    }
}
