using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Api.Filters;

public sealed class ErroresPersistenciaFilter(ILogger<ErroresPersistenciaFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext contexto)
    {
        var error = contexto.Exception;
        if (error is MasterMusic.Api.Data.RecursoEnUsoException)
        {
            contexto.Result = new ObjectResult(new ProblemDetails
            {
                Type = "about:blank", Status = 409, Title = "Recurso en uso",
                Detail = "Existen referencias. Conserva el recurso; puedes desactivar un instrumento.",
                Extensions = { ["codigo"] = "RECURSO_EN_USO" }
            }) { StatusCode = 409, ContentTypes = { "application/problem+json" } };
            contexto.ExceptionHandled = true;
            return;
        }
        if (error is not (DbException or DbUpdateException or TimeoutException)
            && error is not InvalidOperationException { InnerException: DbException })
            return;

        if (error is DbUpdateConcurrencyException)
        {
            contexto.Result = new ObjectResult(new ProblemDetails
            {
                Type = "about:blank", Status = 409, Title = "Actualización concurrente",
                Detail = "El instrumento cambió. Vuelve a consultarlo antes de editar.",
                Extensions = { ["codigo"] = "OPERACION_EN_CURSO" }
            }) { StatusCode = 409, ContentTypes = { "application/problem+json" } };
            contexto.ExceptionHandled = true;
            return;
        }
        if (error is DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 547 } })
        {
            contexto.Result = new ObjectResult(new ProblemDetails
            {
                Type = "about:blank", Status = 409, Title = "Recurso en uso",
                Detail = "El recurso tiene referencias que deben conservarse.",
                Extensions = { ["codigo"] = "RECURSO_EN_USO" }
            }) { StatusCode = 409, ContentTypes = { "application/problem+json" } };
            contexto.ExceptionHandled = true;
            return;
        }
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
