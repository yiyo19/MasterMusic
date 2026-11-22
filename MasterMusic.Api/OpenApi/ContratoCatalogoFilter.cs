using System.Text.Json.Nodes;
using MasterMusic.Api.Controllers;
using MasterMusic.Api.Dtos;
using MasterMusic.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MasterMusic.Api.OpenApi;

/// <summary>Documenta los resultados de los controladores sin alterar su ejecución.</summary>
public sealed class ContratoCatalogoFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument documento, DocumentFilterContext contexto)
    {
        foreach (var descripcion in contexto.ApiDescriptions)
        {
            if (descripcion.ActionDescriptor is not ControllerActionDescriptor accion
                || descripcion.HttpMethod is null
                || !documento.Paths.TryGetValue("/" + descripcion.RelativePath, out var ruta)
                || ruta.Operations is null
                || !ruta.Operations.TryGetValue(new HttpMethod(descripcion.HttpMethod), out var operacion))
                continue;

            var controlador = accion.ControllerTypeInfo.AsType();
            var nombre = accion.MethodInfo.Name;
            var metadatos = descripcion.ActionDescriptor.EndpointMetadata;
            var permisos = metadatos.OfType<IAuthorizeData>().ToList();
            operacion.Security = [];
            if (permisos.Count > 0 && !metadatos.OfType<IAllowAnonymous>().Any())
            {
                var requisito = new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", documento)] = []
                };
                if (controlador == typeof(MovimientosController))
                    requisito[new OpenApiSecuritySchemeReference("ServiceKey", documento)] = [];
                // Un solo requisito expresa AND: en la integración se exigen ambas credenciales.
                operacion.Security.Add(requisito);
                var grupos = permisos.Where(p => !string.IsNullOrWhiteSpace(p.Roles))
                    .Select(p => "(" + p.Roles!.Replace(",", " o ") + ")");
                operacion.Description = "Requiere JWT. Roles: " + string.Join(" y ", grupos) + ".";
                if (controlador == typeof(MovimientosController))
                    operacion.Description += " Además requiere X-Service-Key, enviada únicamente entre las APIs.";
            }
            else
                operacion.Description = "Acceso público; no requiere JWT.";

            foreach (var parametro in operacion.Parameters?.OfType<OpenApiParameter>() ?? [])
                if (parametro.Name == "version")
                {
                    parametro.Required = true;
                    parametro.Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.String,
                        Default = JsonValue.Create("1.0"),
                        Enum = [JsonValue.Create("1.0")!]
                    };
                    parametro.Description = "Versión de la API en la URL: 1.0.";
                }

            var respuestas = new OpenApiResponses();
            IOpenApiSchema Esquema(Type tipo) =>
                contexto.SchemaGenerator.GenerateSchema(tipo, contexto.SchemaRepository);
            void Respuesta(string codigo, string detalle, Type? tipo = null, string medio = "application/json")
            {
                var respuesta = new OpenApiResponse { Description = detalle };
                if (tipo is not null)
                    respuesta.Content = new Dictionary<string, OpenApiMediaType>
                    {
                        [medio] = new() { Schema = Esquema(tipo) }
                    };
                if (codigo == "201")
                    respuesta.Headers = new Dictionary<string, IOpenApiHeader>
                    {
                        ["Location"] = new OpenApiHeader
                        {
                            Description = "Ruta del recurso creado.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri-reference" }
                        }
                    };
                respuestas[codigo] = respuesta;
            }
            void Problema(string codigo, string detalle) =>
                Respuesta(codigo, detalle + " El campo codigo identifica el error de negocio.",
                    typeof(ProblemDetails), "application/problem+json");

            if (controlador == typeof(AuthController))
            {
                if (nombre == nameof(AuthController.Registrar))
                {
                    Respuesta("200", "Usuario Cliente registrado.", typeof(RegistroRespuesta));
                    Problema("409", "CORREO_DUPLICADO: el correo ya está registrado.");
                }
                else
                {
                    Respuesta("200", "JWT y datos públicos del usuario.", typeof(AccesoRespuesta));
                    Problema("401", "CREDENCIALES_INVALIDAS: cuenta inexistente, inactiva o clave incorrecta.");
                }
            }
            else if (controlador == typeof(MovimientosController))
            {
                Respuesta("200", nombre == "Consultar" ? "Movimiento encontrado." :
                    "La misma operación ya fue aplicada; se devuelve el resultado sin modificar stock.",
                    typeof(MovimientoRespuesta));
                if (nombre != "Consultar")
                {
                    Respuesta("201", "Movimiento aplicado y guardado de forma atómica.", typeof(MovimientoRespuesta));
                    Problema("409", "Conflicto de idempotencia, precio, stock o movimiento de origen.");
                }
                Problema("404", "Movimiento o instrumento no encontrado, o no accesible para la cuenta.");
            }
            else if (controlador == typeof(InstrumentosController))
            {
                switch (nombre)
                {
                    case "GetAll":
                        Respuesta("200", "Instrumentos activos.", typeof(List<InstrumentoRespuesta>));
                        break;
                    case "GetById":
                        Respuesta("200", "Instrumento activo.", typeof(InstrumentoRespuesta));
                        break;
                    case "Create":
                        Respuesta("201", "Instrumento creado.", typeof(InstrumentoRespuesta));
                        break;
                    default:
                        Respuesta("204", "Operación completada; sin cuerpo.");
                        break;
                }
                if (nombre is "GetById" or "Update" or "Delete")
                    Respuesta("404", "Instrumento no encontrado. Formato heredado: message.", typeof(MensajeCatalogo));
                if (nombre == "Delete")
                    Problema("409", "STOCK_NO_CERO o RECURSO_EN_USO: se conserva el instrumento.");
            }
            else if (controlador == typeof(InstrumentosAdminController))
            {
                if (nombre == "Listar")
                    Respuesta("200", "Instrumentos activos e inactivos.", typeof(List<InstrumentoRespuesta>));
                else if (nombre == "Estado")
                    Respuesta("204", "Estado actualizado; sin cuerpo.");
                else
                    Respuesta("200", "Instrumento consultado o existencias actualizadas.", typeof(InstrumentoRespuesta));
                if (nombre != "Listar") Problema("404", "Instrumento no encontrado.");
                if (nombre == "Existencias") Problema("409", "STOCK_FUERA_DE_RANGO: se supera el máximo permitido.");
            }
            else if (controlador == typeof(CategoriasController) || controlador == typeof(MarcasController))
            {
                var tipo = controlador == typeof(CategoriasController) ? typeof(Categoria) : typeof(Marca);
                if (nombre == "Listar")
                    Respuesta("200", "Listado de recursos.", typeof(List<>).MakeGenericType(tipo));
                else if (nombre == "Consultar")
                    Respuesta("200", "Recurso encontrado.", tipo);
                else if (nombre == "Crear")
                    Respuesta("201", "Recurso creado.", tipo);
                else
                    Respuesta("204", "Operación completada; sin cuerpo.");
                if (nombre is "Consultar" or "Editar" or "Eliminar") Problema("404", "Recurso no encontrado.");
                if (nombre == "Eliminar") Problema("409", "RECURSO_EN_USO: existen instrumentos asociados.");
            }
            else
                continue;

            // Los controladores usan ApiController y el filtro de persistencia.
            Respuesta("400", "VALIDACION: campos o parámetros inválidos.",
                typeof(ValidationProblemDetails), "application/problem+json");
            if (controlador == typeof(InstrumentosController) && nombre is "Create" or "Update")
                respuestas["400"].Content!["application/json"] = new OpenApiMediaType
                {
                    Schema = Esquema(typeof(MensajeCatalogo))
                };
            if (operacion.Security.Count > 0)
            {
                Problema("401", "NO_AUTENTICADO: credenciales ausentes, vencidas o inválidas.");
                Problema("403", "PROHIBIDO: el rol o la identidad no permite la operación.");
            }
            if (controlador != typeof(AuthController)
                && descripcion.HttpMethod is "POST" or "PUT" or "PATCH" or "DELETE")
            {
                if (!respuestas.ContainsKey("409"))
                    Problema("409", "Conflicto de persistencia o actualización concurrente.");
            }
            Problema("503", "SERVICIO_NO_DISPONIBLE: fallo de persistencia o integración sin configurar.");
            operacion.Responses = respuestas;
        }
        documento.Components ??= new OpenApiComponents();
        documento.Components.Schemas = contexto.SchemaRepository.Schemas;
    }

    // Esquemas de las respuestas anónimas que ya emiten los controladores.
    public sealed record RegistroRespuesta(string Mensaje);
    public sealed record AccesoRespuesta(string Token, string Tipo, UsuarioPublico Usuario);
    public sealed record UsuarioPublico(int IdUser, string Nombre, string Email, string? Rol);
    public sealed record MensajeCatalogo(string Message);
}
