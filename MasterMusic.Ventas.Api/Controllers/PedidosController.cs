using System.Data.Common;
using System.Globalization;
using System.Security.Claims;
using MasterMusic.Ventas.Api.Data;
using MasterMusic.Ventas.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Ventas.Api.Controllers;

[ApiController]
[Route("api/v1.0/Pedidos")]
[Authorize(Roles = "Cliente")]
public partial class PedidosController(
    MasterMusicVentasContext contexto,
    ILogger<PedidosController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(List<PedidoRespuesta>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<List<PedidoRespuesta>>> ListarMisPedidos(
        CancellationToken cancellationToken)
    {
        // La autenticación ya comprobó este identificador en el JWT firmado.
        var idCliente = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            CultureInfo.InvariantCulture);

        try
        {
            var pedidos = await contexto.Pedidos
                .AsNoTracking()
                .Where(pedido => pedido.IdCliente == idCliente)
                .OrderByDescending(pedido => pedido.Fecha)
                .ThenBy(pedido => pedido.IdPedido)
                .Include(pedido => pedido.Detalles)
                .ToListAsync(cancellationToken);

            var respuesta = pedidos.Select(PedidoRespuesta.Desde).ToList();
            return Ok(respuesta);
        }
        catch (Exception error) when (error is DbException or TimeoutException
            || error is InvalidOperationException { InnerException: DbException })
        {
            logger.LogError(error, "No fue posible consultar los pedidos del cliente.");
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Servicio no disponible",
                detail: "No pudimos consultar tus pedidos. Inténtalo nuevamente.",
                type: "about:blank",
                extensions: new Dictionary<string, object?>
                {
                    ["codigo"] = "SERVICIO_NO_DISPONIBLE"
                });
        }
    }
}
