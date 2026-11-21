using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using MasterMusic.Api.Data;
using MasterMusic.Api.Dtos;
using MasterMusic.Api.Entities;
using MasterMusic.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/interno/Movimientos")]
[Authorize(Roles = "Cliente,Administrador")]
[ServiceFilter(typeof(ClaveServicioFilter))]
[TypeFilter(typeof(ErroresPersistenciaFilter))]
public partial class MovimientosController(MasterMusicContext contexto) : ControllerBase
{
    [HttpGet("{idOperacion:guid}")]
    public async Task<IActionResult> Consultar(Guid idOperacion, CancellationToken cancelacion)
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var movimiento = await contexto.MovimientosInventario.AsNoTracking()
            .Include(m => m.Detalles)
            .SingleOrDefaultAsync(m => m.IdOperacion == idOperacion, cancelacion);
        if (movimiento is null || (movimiento.IdCliente != idUsuario && !User.IsInRole("Administrador")))
            return Error(404, "NO_ENCONTRADO", "Movimiento no encontrado.");
        return Ok(Respuesta(movimiento));
    }

    [HttpPost("descuentos")]
    [Authorize(Roles = "Cliente")]
    public async Task<IActionResult> Descontar(DescuentoInventarioDto solicitud, CancellationToken cancelacion)
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (solicitud.IdCliente != idUsuario)
            return Error(403, "PROHIBIDO", "El cliente debe corresponder a la sesión.");
        if (solicitud.IdOperacion == Guid.Empty || solicitud.IdPedido == Guid.Empty
            || solicitud.Lineas.Any(l => l is null)
            || solicitud.Lineas.Select(l => l.IdInstrumento).Distinct().Count() != solicitud.Lineas.Count)
            return Error(400, "VALIDACION", "Indica identificadores válidos e instrumentos sin repetir.");

        long total = 0;
        foreach (var linea in solicitud.Lineas)
        {
            var subtotal = (long)linea.Cantidad * linea.PrecioEsperado;
            if (subtotal > 9_007_199_254_740_991L - total)
                return Error(400, "TOTAL_FUERA_DE_RANGO", "El total supera el límite permitido.");
            total += subtotal;
        }

        var lineas = solicitud.Lineas.OrderBy(l => l.IdInstrumento).ToList();
        var canonico = JsonSerializer.Serialize(new
        {
            solicitud.IdOperacion, solicitud.IdPedido, solicitud.IdCliente, Lineas = lineas
        });
        var huella = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        await BloqueoSqlServer.TomarAsync(contexto, $"movimiento:{solicitud.IdOperacion:N}", cancelacion);
        await BloqueoSqlServer.TomarAsync(contexto, $"inventario-pedido:{solicitud.IdPedido:N}", cancelacion);

        var existente = await contexto.MovimientosInventario.Include(m => m.Detalles)
            .SingleOrDefaultAsync(m => m.IdOperacion == solicitud.IdOperacion, cancelacion);
        if (existente is not null)
        {
            if (existente.IdCliente != idUsuario)
                return Error(404, "NO_ENCONTRADO", "Movimiento no encontrado.");
            if (existente.HuellaSolicitud != huella)
                return Error(409, "IDEMPOTENCIA_CONFLICTO", "La operación ya fue utilizada con otros datos.");
            return Ok(Respuesta(existente));
        }
        if (await contexto.MovimientosInventario.AnyAsync(m => m.IdPedido == solicitud.IdPedido
            && m.Tipo == "Descuento", cancelacion))
            return Error(409, "IDEMPOTENCIA_CONFLICTO", "El pedido ya tiene un descuento de inventario.");

        var instrumentos = new List<Instrumento>();
        foreach (var linea in lineas)
        {
            // Orden fijo de bloqueos y protección contra compras y ediciones concurrentes.
            var instrumento = await contexto.Instrumentos.FromSqlInterpolated($"""
                SELECT * FROM [Instrumento] WITH (UPDLOCK, HOLDLOCK)
                WHERE [id_instrumento] = {linea.IdInstrumento}
                """).SingleOrDefaultAsync(cancelacion);
            if (instrumento is null)
                return Error(404, "NO_ENCONTRADO", "Uno de los instrumentos no existe.");
            instrumentos.Add(instrumento);
        }

        if (instrumentos.Any(i => !i.Activo))
            return Error(409, "INSTRUMENTO_INACTIVO", "Uno de los instrumentos está inactivo.");
        var precios = instrumentos.Zip(lineas)
            .Where(par => par.First.Precio != par.Second.PrecioEsperado)
            .Select(par => new { idInstrumento = par.First.IdInstrumento, precioActual = par.First.Precio })
            .ToList();
        if (precios.Count > 0)
            return Problem(statusCode: 409, type: "about:blank", title: "Precio cambiado",
                detail: "Revisa los precios antes de enviar una nueva solicitud.",
                extensions: new Dictionary<string, object?> { ["codigo"] = "PRECIO_CAMBIADO", ["preciosActualizados"] = precios });
        if (instrumentos.Zip(lineas).Any(par => par.First.Stock < par.Second.Cantidad))
            return Error(409, "STOCK_INSUFICIENTE", "No hay stock suficiente para toda la selección.");

        var movimiento = new MovimientoInventario
        {
            IdOperacion = solicitud.IdOperacion,
            IdPedido = solicitud.IdPedido,
            IdCliente = idUsuario,
            HuellaSolicitud = huella
        };
        foreach (var (instrumento, linea) in instrumentos.Zip(lineas))
        {
            instrumento.Stock -= linea.Cantidad;
            movimiento.Detalles.Add(new DetalleMovimientoInventario
            {
                IdInstrumento = instrumento.IdInstrumento,
                Cantidad = linea.Cantidad,
                NombreInstrumento = instrumento.Nombre,
                PrecioUnitario = instrumento.Precio
            });
        }
        contexto.MovimientosInventario.Add(movimiento);
        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);
        return Created($"/api/v1.0/interno/Movimientos/{movimiento.IdOperacion}", Respuesta(movimiento));
    }

    private ObjectResult Error(int estado, string codigo, string detalle) =>
        Problem(statusCode: estado, type: "about:blank", title: "Solicitud de inventario", detail: detalle,
            extensions: new Dictionary<string, object?> { ["codigo"] = codigo });

    private static MovimientoRespuesta Respuesta(MovimientoInventario movimiento) =>
        new(movimiento.IdOperacion, movimiento.IdPedido, movimiento.IdCliente, movimiento.Tipo,
            movimiento.IdOperacionOrigen, movimiento.Fecha,
            movimiento.Detalles.OrderBy(d => d.IdInstrumento)
                .Select(d => new DetalleMovimientoRespuesta(d.IdInstrumento, d.NombreInstrumento,
                    d.Cantidad, d.PrecioUnitario)).ToList());
}
