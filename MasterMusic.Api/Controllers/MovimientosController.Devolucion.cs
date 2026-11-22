using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MasterMusic.Api.Data;
using MasterMusic.Api.Dtos;
using MasterMusic.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Api.Controllers;

public partial class MovimientosController
{
    [HttpPost("devoluciones")]
    public async Task<IActionResult> Devolver(DevolucionInventarioDto solicitud, CancellationToken cancelacion)
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (!User.IsInRole("Administrador") && solicitud.IdCliente != idUsuario)
            return Error(403, "PROHIBIDO", "El cliente debe corresponder a la sesión.");
        if (solicitud.IdOperacion == Guid.Empty || solicitud.IdOperacionOrigen == Guid.Empty
            || solicitud.IdPedido == Guid.Empty || solicitud.IdOperacion == solicitud.IdOperacionOrigen)
            return Error(400, "VALIDACION", "Indica identificadores válidos y una operación de devolución diferente.");

        var huella = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(solicitud))));
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        await BloqueoSqlServer.TomarAsync(contexto, $"movimiento:{solicitud.IdOperacion:N}", cancelacion);
        await BloqueoSqlServer.TomarAsync(contexto, $"inventario-pedido:{solicitud.IdPedido:N}", cancelacion);

        var existente = await contexto.MovimientosInventario.Include(m => m.Detalles)
            .SingleOrDefaultAsync(m => m.IdOperacion == solicitud.IdOperacion, cancelacion);
        if (existente is not null)
        {
            if (!User.IsInRole("Administrador") && existente.IdCliente != idUsuario)
                return Error(404, "NO_ENCONTRADO", "Movimiento no encontrado.");
            if (existente.Tipo != "Devolucion" || existente.HuellaSolicitud != huella)
                return Error(409, "IDEMPOTENCIA_CONFLICTO", "La operación ya fue utilizada con otros datos.");
            return Ok(Respuesta(existente));
        }

        var origen = await contexto.MovimientosInventario.Include(m => m.Detalles)
            .SingleOrDefaultAsync(m => m.IdOperacion == solicitud.IdOperacionOrigen, cancelacion);
        if (origen is null || (!User.IsInRole("Administrador") && origen.IdCliente != idUsuario))
            return Error(404, "NO_ENCONTRADO", "Descuento de origen no encontrado.");
        if (origen.Tipo != "Descuento" || origen.IdPedido != solicitud.IdPedido
            || origen.IdCliente != solicitud.IdCliente)
            return Error(409, "MOVIMIENTO_ORIGEN_INVALIDO", "El descuento no corresponde al pedido y cliente indicados.");
        if (await contexto.MovimientosInventario.AnyAsync(
            m => m.IdOperacionOrigen == origen.IdOperacion, cancelacion))
            return Error(409, "DEVOLUCION_YA_REGISTRADA", "Este descuento ya tiene una devolución registrada.");

        var detalles = origen.Detalles.OrderBy(d => d.IdInstrumento).ToList();
        var instrumentos = new List<Instrumento>();
        foreach (var detalle in detalles)
        {
            var instrumento = await contexto.Instrumentos.FromSqlInterpolated($"""
                SELECT * FROM [Instrumento] WITH (UPDLOCK, HOLDLOCK)
                WHERE [id_instrumento] = {detalle.IdInstrumento}
                """).SingleOrDefaultAsync(cancelacion);
            if (instrumento is null)
                return Error(404, "NO_ENCONTRADO", "Uno de los instrumentos del descuento no existe.");
            if (instrumento.Stock > int.MaxValue - detalle.Cantidad)
                return Error(409, "STOCK_FUERA_DE_RANGO", "La devolución supera el stock máximo permitido.");
            instrumentos.Add(instrumento);
        }

        var devolucion = new MovimientoInventario
        {
            IdOperacion = solicitud.IdOperacion, IdOperacionOrigen = origen.IdOperacion,
            IdPedido = origen.IdPedido, IdCliente = origen.IdCliente,
            Tipo = "Devolucion", HuellaSolicitud = huella
        };
        foreach (var (instrumento, detalle) in instrumentos.Zip(detalles))
        {
            // Cantidades y precios provienen del descuento guardado, incluso si el instrumento está inactivo.
            instrumento.Stock += detalle.Cantidad;
            devolucion.Detalles.Add(new DetalleMovimientoInventario
            {
                IdInstrumento = detalle.IdInstrumento, Cantidad = detalle.Cantidad,
                NombreInstrumento = detalle.NombreInstrumento, PrecioUnitario = detalle.PrecioUnitario
            });
        }
        contexto.MovimientosInventario.Add(devolucion);
        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);
        return Created($"/api/v1.0/interno/Movimientos/{devolucion.IdOperacion}", Respuesta(devolucion));
    }
}
