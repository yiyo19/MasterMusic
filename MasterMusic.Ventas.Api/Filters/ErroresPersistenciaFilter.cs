using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Ventas.Api.Filters;

public sealed class ErroresPersistenciaFilter(ILogger<ErroresPersistenciaFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext contexto)
    {
        var error = contexto.Exception;
        if (error is not (DbException or DbUpdateException or TimeoutException)
            && error is not InvalidOperationException { InnerException: DbException })
            return;

        logger.LogError(error, "No se pudo completar la operación de persistencia.");
        var problema = new ProblemDetails
        {
            Type = "about:blank",
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Servicio no disponible",
            Detail = "Consulta el estado de la operación antes de reintentar con la misma clave."
        };
        problema.Extensions["codigo"] = "SERVICIO_NO_DISPONIBLE";
        contexto.Result = new ObjectResult(problema)
        {
            StatusCode = problema.Status,
            ContentTypes = { "application/problem+json" }
        };
        contexto.ExceptionHandled = true;
    }
}
