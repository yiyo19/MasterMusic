using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MasterMusic.Ventas.Api.Data;
using MasterMusic.Ventas.Api.DTOs;
using MasterMusic.Ventas.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Ventas.Api.Services;

public sealed record ResultadoCambioPedido(OperacionPedido? Operacion, ProblemaPedido? Problema = null);
internal sealed record ComandoCambioPedido(Guid IdPedido, string? Motivo, Guid? IdOperacionOrigen);

public sealed class CambiosPedidosService(MasterMusicVentasContext contexto, InventarioClient inventario)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoCambioPedido> EjecutarAsync(
        int idAdministrador, Guid idPedido, string tipo, Guid clave, string? motivo,
        string autorizacion, CancellationToken cancelacion)
    {
        var registro = await RegistrarAsync(idAdministrador, idPedido, tipo, clave, motivo, cancelacion);
        if (registro.Problema is not null) return registro;
        var operacion = await RecuperarAsync(registro.Operacion!.IdOperacion, idAdministrador, autorizacion, cancelacion);
        return new ResultadoCambioPedido(operacion);
    }

    private async Task<ResultadoCambioPedido> RegistrarAsync(
        int idAdministrador, Guid idPedido, string tipo, Guid clave, string? motivo, CancellationToken cancelacion)
    {
        var huella = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { idPedido, tipo, motivo }, Json))));
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        await BloqueoSqlServer.TomarAsync(contexto, $"cambio-clave:{idAdministrador}:{tipo}:{clave:N}", cancelacion);
        var existente = await contexto.Operaciones.SingleOrDefaultAsync(o =>
            o.IdUsuario == idAdministrador && o.Tipo == tipo && o.ClaveCliente == clave, cancelacion);
        if (existente is not null)
            return existente.HuellaSolicitud == huella
                ? new ResultadoCambioPedido(existente)
                : Error(409, "IDEMPOTENCIA_CONFLICTO", "La clave ya fue utilizada con otro pedido o motivo.");

        await BloqueoSqlServer.TomarAsync(contexto, $"pedido:{idPedido:N}", cancelacion);
        var pedido = await contexto.Pedidos.SingleOrDefaultAsync(p => p.IdPedido == idPedido, cancelacion);
        if (pedido is null)
            return Error(404, "NO_ENCONTRADO", "Pedido no encontrado.");
        if (pedido.Estado != EstadoPedido.Pendiente)
            return Error(409, "ESTADO_PEDIDO_INVALIDO", "Solo se puede confirmar o cancelar un pedido pendiente.");

        // Esta intención durable sigue bloqueando otras transiciones después de un timeout.
        var enCurso = await contexto.Operaciones.AnyAsync(o => o.IdPedido == idPedido
            && (o.Tipo == "Confirmar" || o.Tipo == "Cancelar")
            && (o.EstadoOperacion == "EnProceso" || o.EstadoOperacion == "EnCompensacion"), cancelacion);
        if (enCurso)
            return Error(409, "OPERACION_EN_CURSO", "El pedido tiene una transición en curso; consulta o recupera la operación original.");

        Guid? origen = null;
        if (tipo == "Cancelar")
        {
            origen = await contexto.Operaciones.Where(o => o.IdPedido == idPedido
                && o.Tipo == "Crear" && o.EstadoOperacion == "Completada")
                .Select(o => (Guid?)o.IdOperacion).SingleOrDefaultAsync(cancelacion);
            if (origen is null)
                return Error(409, "MOVIMIENTO_ORIGEN_INVALIDO", "El pedido no tiene un descuento de inventario vinculado.");
        }

        var operacion = new OperacionPedido
        {
            IdOperacion = Guid.NewGuid(), ClaveCliente = clave, IdUsuario = idAdministrador,
            IdPedido = idPedido, Tipo = tipo, HuellaSolicitud = huella,
            SolicitudJson = JsonSerializer.Serialize(new ComandoCambioPedido(idPedido, motivo, origen), Json)
        };
        contexto.Operaciones.Add(operacion);
        await contexto.SaveChangesAsync(cancelacion);
        // Pedido, motivo, origen y clave de devolución quedan persistidos antes de HTTP.
        await transaccion.CommitAsync(cancelacion);
        return new ResultadoCambioPedido(operacion);
    }

    public async Task<OperacionPedido?> RecuperarAsync(
        Guid idOperacion, int idAdministrador, string autorizacion, CancellationToken cancelacion)
    {
        contexto.ChangeTracker.Clear();
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        await BloqueoSqlServer.TomarAsync(contexto, $"operacion:{idOperacion:N}", cancelacion);
        var operacion = await contexto.Operaciones.SingleOrDefaultAsync(o =>
            o.IdOperacion == idOperacion && o.IdUsuario == idAdministrador
            && (o.Tipo == "Confirmar" || o.Tipo == "Cancelar"), cancelacion);
        if (operacion is null) return null;

        await BloqueoSqlServer.TomarAsync(contexto, $"pedido:{operacion.IdPedido:N}", cancelacion);
        var pedido = await contexto.Pedidos.Include(p => p.Detalles)
            .SingleAsync(p => p.IdPedido == operacion.IdPedido, cancelacion);
        operacion.Pedido = pedido;
        if (operacion.EstadoOperacion != "EnProceso") return operacion;

        if (pedido.Estado != EstadoPedido.Pendiente)
        {
            Rechazar(operacion, new ProblemaPedido(409, "ESTADO_PEDIDO_INVALIDO",
                "Solo se puede modificar un pedido pendiente."));
        }
        else if (operacion.Tipo == "Confirmar")
        {
            pedido.Confirmar();
            operacion.EstadoOperacion = "Completada";
        }
        else
        {
            var comando = JsonSerializer.Deserialize<ComandoCambioPedido>(operacion.SolicitudJson, Json)!;
            var solicitud = new SolicitudDevolucion(operacion.IdOperacion,
                comando.IdOperacionOrigen!.Value, pedido.IdPedido, pedido.IdCliente);
            var resultado = await inventario.ConsultarAsync(operacion.IdOperacion, autorizacion, cancelacion);
            if (resultado.NoEncontrado)
                resultado = await inventario.DevolverAsync(solicitud, autorizacion, cancelacion);

            if (resultado.Movimiento is not null && Corresponde(resultado.Movimiento, solicitud, pedido))
            {
                pedido.Cancelar(comando.Motivo!);
                operacion.EstadoOperacion = "Completada";
            }
            else if (resultado.Problema is not null)
            {
                Rechazar(operacion, resultado.Problema);
            }
            // Si no se puede verificar la devolución, conserva Pendiente y la intención EnProceso.
        }

        operacion.FechaActualizacion = DateTime.UtcNow;
        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);
        return operacion;
    }

    private static bool Corresponde(
        MovimientoInventarioRespuesta movimiento, SolicitudDevolucion solicitud, Pedido pedido)
    {
        if (movimiento.IdOperacion != solicitud.IdOperacion || movimiento.Tipo != "Devolucion"
            || movimiento.IdOperacionOrigen != solicitud.IdOperacionOrigen
            || movimiento.IdPedido != pedido.IdPedido || movimiento.IdCliente != pedido.IdCliente
            || movimiento.Detalles is null || movimiento.Detalles.Count != pedido.Detalles.Count
            || movimiento.Detalles.Any(d => d is null))
            return false;
        return movimiento.Detalles.OrderBy(d => d.IdInstrumento)
            .Zip(pedido.Detalles.OrderBy(d => d.IdInstrumento)).All(par =>
                par.First.IdInstrumento == par.Second.IdInstrumento
                && par.First.Cantidad == par.Second.Cantidad
                && par.First.PrecioUnitario == par.Second.PrecioUnitario
                && par.First.NombreInstrumento == par.Second.NombreInstrumento);
    }

    private static void Rechazar(OperacionPedido operacion, ProblemaPedido problema)
    {
        operacion.EstadoOperacion = "Rechazada";
        operacion.CodigoResultado = problema.Codigo;
        operacion.ProblemaJson = JsonSerializer.Serialize(problema, Json);
    }

    private static ResultadoCambioPedido Error(int estado, string codigo, string detalle) =>
        new(null, new ProblemaPedido(estado, codigo, detalle));
}
