using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MasterMusic.Api.Filters;

public sealed class ClaveServicioFilter(IConfiguration configuracion) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext contexto)
    {
        var esperada = configuracion["Inventario:ServiceKey"];
        if (string.IsNullOrWhiteSpace(esperada))
        {
            contexto.Result = Error(503, "Servicio no disponible", "La integración no está configurada.",
                "SERVICIO_NO_DISPONIBLE");
            return;
        }

        var recibida = contexto.HttpContext.Request.Headers["X-Service-Key"];
        var valida = recibida.Count == 1 && !string.IsNullOrWhiteSpace(recibida[0])
            && CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(esperada)),
                SHA256.HashData(Encoding.UTF8.GetBytes(recibida[0]!)));
        var identidadValida = int.TryParse(
            contexto.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id > 0;
        if (!valida || !identidadValida)
        {
            contexto.HttpContext.Response.Headers.WWWAuthenticate = "Bearer";
            contexto.Result = Error(401, "Autenticación requerida",
                "Se requieren credenciales válidas para la integración.", "NO_AUTENTICADO");
        }
    }

    private static ObjectResult Error(int estado, string titulo, string detalle, string codigo)
    {
        var problema = new ProblemDetails { Type = "about:blank", Status = estado, Title = titulo, Detail = detalle };
        problema.Extensions["codigo"] = codigo;
        return new ObjectResult(problema) { StatusCode = estado, ContentTypes = { "application/problem+json" } };
    }
}
