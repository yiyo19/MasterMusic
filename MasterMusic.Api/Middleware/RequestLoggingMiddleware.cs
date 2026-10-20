using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace MasterMusic.Api.Middlewares
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var watch = Stopwatch.StartNew();

            // Log antes de procesar la petición
            _logger.LogInformation($"[NUEVA PETICIÓN] {context.Request.Method} {context.Request.Path}");

            // Pasar al siguiente middleware (o al controlador)
            await _next(context);

            watch.Stop();

            // Log después de procesar la petición
            _logger.LogInformation($"[RESPUESTA] Status: {context.Response.StatusCode} | Tiempo: {watch.ElapsedMilliseconds}ms");
        }
    }
}