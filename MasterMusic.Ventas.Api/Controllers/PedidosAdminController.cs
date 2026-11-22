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
[Route("api/v1.0/admin/Pedidos")]
[Authorize(Roles = "Administrador")]
[TypeFilter(typeof(ErroresPersistenciaFilter))]
[ProducesResponseType(typeof(ProblemDetails), 400)]
[ProducesResponseType(typeof(ProblemDetails), 401)]
[ProducesResponseType(typeof(ProblemDetails), 403)]
[ProducesResponseType(typeof(ProblemDetails), 503)]
public class PedidosAdminController(
    MasterMusicVentasContext contexto, CambiosPedidosService servicio) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<PedidoRespuesta>), 200)]
    public async Task<IActionResult> Listar(CancellationToken cancelacion)
    {
        var pedidos = await contexto.Pedidos.AsNoTracking().Include(p => p.Detalles)
            .OrderByDescending(p => p.Fecha).ThenBy(p => p.IdPedido).ToListAsync(cancelacion);
        return Ok(pedidos.Select(PedidoRespuesta.Desde).ToList());
    }

    [HttpGet("{idPedido:guid}")]
    [ProducesResponseType(typeof(PedidoRespuesta), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    public async Task<IActionResult> Consultar(Guid idPedido, CancellationToken cancelacion)
    {
        var pedido = await contexto.Pedidos.AsNoTracking().Include(p => p.Detalles)
            .SingleOrDefaultAsync(p => p.IdPedido == idPedido, cancelacion);
        return pedido is null
            ? Problema(new ProblemaPedido(404, "NO_ENCONTRADO", "Pedido no encontrado."))
            : Ok(PedidoRespuesta.Desde(pedido));
    }

    [HttpPost("{idPedido:guid}/confirmacion")]
    [ProducesResponseType(typeof(PedidoRespuesta), 200)]
    [ProducesResponseType(typeof(OperacionRespuesta), 202)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public Task<IActionResult> Confirmar(Guid idPedido,
        [FromHeader(Name = "Idempotency-Key")] string? clave, CancellationToken cancelacion) =>
        EjecutarAsync(idPedido, "Confirmar", clave, null, cancelacion);

    [HttpPost("{idPedido:guid}/cancelacion")]
    [ProducesResponseType(typeof(PedidoRespuesta), 200)]
    [ProducesResponseType(typeof(OperacionRespuesta), 202)]
    [ProducesResponseType(typeof(ProblemDetails), 404)]
    [ProducesResponseType(typeof(ProblemDetails), 409)]
    public Task<IActionResult> Cancelar(Guid idPedido, PedidoCancelar solicitud,
        [FromHeader(Name = "Idempotency-Key")] string? clave, CancellationToken cancelacion) =>
        EjecutarAsync(idPedido, "Cancelar", clave, solicitud.Motivo, cancelacion);

    private async Task<IActionResult> EjecutarAsync(
        Guid idPedido, string tipo, string? clave, string? motivo, CancellationToken cancelacion)
    {
        if (idPedido == Guid.Empty || !Guid.TryParse(clave, out var claveCliente) || claveCliente == Guid.Empty
            || (tipo == "Cancelar" && string.IsNullOrWhiteSpace(motivo)))
            return Problema(new ProblemaPedido(400, "VALIDACION", "Indica una clave UUID, un pedido válido y un motivo al cancelar."));

        var idAdministrador = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var resultado = await servicio.EjecutarAsync(idAdministrador, idPedido, tipo, claveCliente,
            motivo?.Trim(), Request.Headers.Authorization.ToString(), cancelacion);
        if (resultado.Problema is not null)
            return Problema(resultado.Problema);

        var respuesta = CreacionPedidosService.Respuesta(resultado.Operacion!);
        if (respuesta.EstadoOperacion == "Rechazada")
            return Problema(respuesta.Problema!);
        if (respuesta.EstadoOperacion == "Completada")
            return Ok(respuesta.Pedido);
        Response.Headers.RetryAfter = "5";
        return Accepted($"/api/v1.0/Operaciones/{respuesta.IdOperacion}", respuesta);
    }

    private static ObjectResult Problema(ProblemaPedido problema) =>
        new(problema) { StatusCode = problema.Status, ContentTypes = { "application/problem+json" } };
}
