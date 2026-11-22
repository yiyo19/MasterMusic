using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MasterMusic.Ventas.Api.OpenApi;

public sealed class DocumentoPedidosTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument documento,
        OpenApiDocumentTransformerContext contexto, CancellationToken cancelacion)
    {
        documento.Info.Title = "MasterMusic Ventas API";
        documento.Info.Version = "v1";
        documento.Info.Description =
            "Pedidos e integración con inventario. Obtén el JWT en POST " +
            "https://localhost:7052/api/v1.0/Auth/login y pégalo en Authorize. " +
            "Cliente consulta y crea sus pedidos; Administrador consulta, confirma y cancela. " +
            "Crear descuenta stock; confirmar no vuelve a descontarlo; cancelar devuelve las unidades. " +
            "Los importes se expresan en pesos chilenos enteros (CLP).";

        documento.Components ??= new OpenApiComponents();
        documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        documento.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Pega únicamente el token obtenido en Auth/login, sin escribir Bearer."
        };
        return Task.CompletedTask;
    }
}
