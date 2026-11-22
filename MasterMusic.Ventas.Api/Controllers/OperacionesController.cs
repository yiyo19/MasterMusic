using System.Security.Claims;
using MasterMusic.Ventas.Api.Data;
using MasterMusic.Ventas.Api.DTOs;
using MasterMusic.Ventas.Api.Filters;
using MasterMusic.Ventas.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Ventas.Api.Controllers;

[ApiController]
[Route("api/v1.0/Operaciones")]
[Authorize(Roles = "Cliente,Administrador")]
[TypeFilter(typeof(ErroresPersistenciaFilter))]
[ProducesResponseType(typeof(ProblemDetails), 400)]
[ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403)]
[ProducesResponseType(typeof(ProblemDetails), 503)]
public class OperacionesController(MasterMusicVentasContext contexto) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(OperacionRespuesta), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Buscar(Guid claveCliente, string tipo, CancellationToken cancelacion)
    {
        if (claveCliente == Guid.Empty || tipo is not ("Crear" or "Confirmar" or "Cancelar"))
            return Problema(400, "VALIDACION", "Indica una clave y un tipo de operación válidos.");
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var operacion = await contexto.Operaciones.AsNoTracking()
            .Include(o => o.Pedido!).ThenInclude(p => p.Detalles)
            .SingleOrDefaultAsync(o => o.IdUsuario == idUsuario && o.Tipo == tipo
                && o.ClaveCliente == claveCliente, cancelacion);
        if (operacion is null)
            return Problema(404, "NO_ENCONTRADO", "Operación no encontrada.");
        return Ok(CreacionPedidosService.Respuesta(operacion));
    }

    [HttpGet("{idOperacion:guid}")]
    [ProducesResponseType(typeof(OperacionRespuesta), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Consultar(Guid idOperacion, CancellationToken cancelacion)
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var operacion = await contexto.Operaciones.AsNoTracking()
            .Include(o => o.Pedido!).ThenInclude(p => p.Detalles)
            .SingleOrDefaultAsync(o => o.IdUsuario == idUsuario && o.IdOperacion == idOperacion, cancelacion);
        if (operacion is null)
            return Problema(404, "NO_ENCONTRADO", "Operación no encontrada.");
        return Ok(CreacionPedidosService.Respuesta(operacion));
    }

    [HttpPost("{idOperacion:guid}/recuperacion")]
    [ProducesResponseType(typeof(OperacionRespuesta), 200)]
    [ProducesResponseType(typeof(OperacionRespuesta), 202)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Recuperar(Guid idOperacion,
        [FromServices] CreacionPedidosService servicio,
        [FromServices] CambiosPedidosService cambios, CancellationToken cancelacion)
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var tipo = await contexto.Operaciones.AsNoTracking()
            .Where(o => o.IdOperacion == idOperacion && o.IdUsuario == idUsuario)
            .Select(o => o.Tipo).SingleOrDefaultAsync(cancelacion);
        if (tipo is null)
            return Problema(404, "NO_ENCONTRADO", "Operación no encontrada.");
        if ((tipo == "Crear" && !User.IsInRole("Cliente"))
            || (tipo != "Crear" && !User.IsInRole("Administrador")))
            return Problema(403, "PROHIBIDO", "Tu rol no permite recuperar esta operación.");
        var operacion = tipo == "Crear"
            ? await servicio.RecuperarAsync(idOperacion, idUsuario, Request.Headers.Authorization.ToString(), cancelacion)
            : await cambios.RecuperarAsync(idOperacion, idUsuario, Request.Headers.Authorization.ToString(), cancelacion);
        if (operacion is null)
            return Problema(404, "NO_ENCONTRADO", "Operación no encontrada.");
        var respuesta = CreacionPedidosService.Respuesta(operacion);
        if (respuesta.Recuperable)
        {
            Response.Headers.RetryAfter = "5";
            return Accepted($"/api/v1.0/Operaciones/{idOperacion}", respuesta);
        }
        return Ok(respuesta);
    }

    private static ObjectResult Problema(int estado, string codigo, string detalle) =>
        new(new ProblemaPedido(estado, codigo, detalle))
        { StatusCode = estado, ContentTypes = { "application/problem+json" } };
}
