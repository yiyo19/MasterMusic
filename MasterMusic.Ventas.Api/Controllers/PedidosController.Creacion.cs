using System.Security.Claims;
using MasterMusic.Ventas.Api.DTOs;
using MasterMusic.Ventas.Api.Entities;
using MasterMusic.Ventas.Api.Filters;
using MasterMusic.Ventas.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Ventas.Api.Controllers;

[TypeFilter(typeof(ErroresPersistenciaFilter))]
public partial class PedidosController
{
    [HttpGet("{idPedido:guid}")]
    public async Task<IActionResult> ConsultarPedido(Guid idPedido, CancellationToken cancelacion)
    {
        var idCliente = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var pedido = await contexto.Pedidos.AsNoTracking().Include(p => p.Detalles)
            .SingleOrDefaultAsync(p => p.IdPedido == idPedido && p.IdCliente == idCliente, cancelacion);
        if (pedido is null)
            return Problema(new ProblemaPedido(404, "NO_ENCONTRADO", "Pedido no encontrado."));
        return Ok(PedidoRespuesta.Desde(pedido));
    }

    [HttpPost]
    public async Task<IActionResult> CrearPedido(PedidoCrear solicitud,
        [FromHeader(Name = "Idempotency-Key")] string? clave,
        [FromServices] CreacionPedidosService servicio, CancellationToken cancelacion)
    {
        if (!Guid.TryParse(clave, out var claveCliente) || claveCliente == Guid.Empty
            || solicitud.Lineas.Any(l => l is null)
            || solicitud.Lineas.Select(l => l.IdInstrumento).Distinct().Count() != solicitud.Lineas.Count)
            return Problema(new ProblemaPedido(400, "VALIDACION",
                "Indica una clave UUID e instrumentos sin repetir."));

        long total = 0;
        foreach (var linea in solicitud.Lineas)
        {
            var subtotal = (long)linea.Cantidad * linea.PrecioEsperado;
            if (subtotal > Pedido.TotalMaximo - total)
                return Problema(new ProblemaPedido(400, "TOTAL_FUERA_DE_RANGO", "El total supera el límite permitido."));
            total += subtotal;
        }

        var idCliente = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var resultado = await servicio.CrearAsync(idCliente, claveCliente, solicitud,
            Request.Headers.Authorization.ToString(), cancelacion);
        if (resultado.Conflicto is not null)
            return Problema(resultado.Conflicto);

        var operacion = CreacionPedidosService.Respuesta(resultado.Operacion);
        if (operacion.EstadoOperacion == "Rechazada")
            return Problema(operacion.Problema!);
        if (operacion.Pedido is not null)
        {
            if (resultado.Nueva)
                return Created($"/api/v1.0/Pedidos/{operacion.IdPedido}", operacion.Pedido);
            return Ok(operacion.Pedido);
        }
        Response.Headers.RetryAfter = "5";
        return Accepted($"/api/v1.0/Operaciones/{operacion.IdOperacion}", operacion);
    }

    private static ObjectResult Problema(ProblemaPedido problema) =>
        new(problema) { StatusCode = problema.Status, ContentTypes = { "application/problem+json" } };
}
