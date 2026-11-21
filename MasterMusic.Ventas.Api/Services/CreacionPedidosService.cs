using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MasterMusic.Ventas.Api.Data;
using MasterMusic.Ventas.Api.DTOs;
using MasterMusic.Ventas.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Ventas.Api.Services;

public sealed record ResultadoAlta(
    OperacionPedido Operacion, bool Nueva, ProblemaPedido? Conflicto = null);

public sealed class CreacionPedidosService(
    MasterMusicVentasContext contexto, InventarioClient inventario)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ResultadoAlta> CrearAsync(int idCliente, Guid clave, PedidoCrear datos,
        string autorizacion, CancellationToken cancelacion)
    {
        var lineas = datos.Lineas.OrderBy(l => l.IdInstrumento).ToList();
        var huella = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(lineas, Json))));
        var (operacion, nueva) = await RegistrarAsync(idCliente, clave, huella, lineas, cancelacion);
        if (operacion.HuellaSolicitud != huella)
            return new ResultadoAlta(operacion, nueva,
                new ProblemaPedido(409, "IDEMPOTENCIA_CONFLICTO", "La clave ya fue utilizada con otra selección."));

        var resultado = await RecuperarAsync(operacion.IdOperacion, idCliente, autorizacion, cancelacion);
        return new ResultadoAlta(resultado!, nueva);
    }

    private async Task<(OperacionPedido Operacion, bool Nueva)> RegistrarAsync(
        int idCliente, Guid clave, string huella, IReadOnlyList<LineaPedido> lineas, CancellationToken cancelacion)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        await BloqueoSqlServer.TomarAsync(contexto, $"alta:{idCliente}:{clave:N}", cancelacion);
        var existente = await contexto.Operaciones
            .SingleOrDefaultAsync(o => o.IdUsuario == idCliente && o.Tipo == "Crear"
                && o.ClaveCliente == clave, cancelacion);
        if (existente is not null)
            return (existente, false);

        var operacion = new OperacionPedido
        {
            IdOperacion = Guid.NewGuid(),
            ClaveCliente = clave,
            IdUsuario = idCliente,
            HuellaSolicitud = huella
        };
        var solicitud = new SolicitudDescuento(operacion.IdOperacion, Guid.NewGuid(), idCliente, lineas);
        operacion.SolicitudJson = JsonSerializer.Serialize(solicitud, Json);
        contexto.Operaciones.Add(operacion);
        await contexto.SaveChangesAsync(cancelacion);
        // La intención queda durable antes de cualquier llamada a la otra API.
        await transaccion.CommitAsync(cancelacion);
        return (operacion, true);
    }

    public async Task<OperacionPedido?> RecuperarAsync(Guid idOperacion, int idUsuario,
        string autorizacion, CancellationToken cancelacion)
    {
        contexto.ChangeTracker.Clear();
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        // Serializa los reintentos de una operación, incluso entre instancias de Ventas.
        await BloqueoSqlServer.TomarAsync(contexto, $"operacion:{idOperacion:N}", cancelacion);
        var operacion = await contexto.Operaciones.Include(o => o.Pedido!).ThenInclude(p => p.Detalles)
            .SingleOrDefaultAsync(o => o.IdOperacion == idOperacion && o.IdUsuario == idUsuario
                && o.Tipo == "Crear", cancelacion);
        if (operacion is null || operacion.EstadoOperacion != "EnProceso")
            return operacion;

        var solicitud = JsonSerializer.Deserialize<SolicitudDescuento>(operacion.SolicitudJson, Json)!;
        var resultado = await inventario.ConsultarAsync(operacion.IdOperacion, autorizacion, cancelacion);
        if (resultado.NoEncontrado)
            resultado = await inventario.DescontarAsync(solicitud, autorizacion, cancelacion);

        if (resultado.Movimiento is not null && Corresponde(resultado.Movimiento, solicitud))
        {
            var movimiento = resultado.Movimiento;
            var pedido = new Pedido(solicitud.IdPedido, idUsuario, movimiento.Detalles.Select(d =>
                new DetallePedido(solicitud.IdPedido, d.IdInstrumento, d.NombreInstrumento, d.Cantidad, d.PrecioUnitario)));
            contexto.Pedidos.Add(pedido);
            operacion.Pedido = pedido;
            operacion.IdPedido = pedido.IdPedido;
            operacion.EstadoOperacion = "Completada";
        }
        else if (resultado.Problema is not null)
        {
            operacion.EstadoOperacion = "Rechazada";
            operacion.CodigoResultado = resultado.Problema.Codigo;
            operacion.ProblemaJson = JsonSerializer.Serialize(resultado.Problema, Json);
        }

        // Un resultado incierto mantiene EnProceso y puede retomarse con la misma operación.
        operacion.FechaActualizacion = DateTime.UtcNow;
        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);
        return operacion;
    }

    private static bool Corresponde(MovimientoInventarioRespuesta movimiento, SolicitudDescuento solicitud)
    {
        if (movimiento.IdOperacion != solicitud.IdOperacion || movimiento.IdPedido != solicitud.IdPedido
            || movimiento.IdCliente != solicitud.IdCliente || movimiento.Tipo != "Descuento"
            || movimiento.IdOperacionOrigen is not null || movimiento.Detalles is null
            || movimiento.Detalles.Count != solicitud.Lineas.Count || movimiento.Detalles.Any(d => d is null))
            return false;

        var detalles = movimiento.Detalles.OrderBy(d => d.IdInstrumento).ToList();
        return detalles.Zip(solicitud.Lineas.OrderBy(l => l.IdInstrumento)).All(par =>
            par.First.IdInstrumento == par.Second.IdInstrumento && par.First.Cantidad == par.Second.Cantidad
            && par.First.PrecioUnitario == par.Second.PrecioEsperado
            && !string.IsNullOrWhiteSpace(par.First.NombreInstrumento) && par.First.NombreInstrumento.Length <= 150);
    }

    public static OperacionRespuesta Respuesta(OperacionPedido operacion) =>
        new(operacion.IdOperacion, operacion.ClaveCliente, operacion.Tipo, operacion.EstadoOperacion,
            operacion.IdPedido, operacion.FechaCreacion, operacion.FechaActualizacion,
            operacion.EstadoOperacion is "EnProceso" or "EnCompensacion", operacion.CodigoResultado)
        {
            Pedido = operacion.EstadoOperacion == "Completada" && operacion.Pedido is not null
                ? PedidoRespuesta.Desde(operacion.Pedido) : null,
            Problema = operacion.ProblemaJson is null ? null
                : JsonSerializer.Deserialize<ProblemaPedido>(operacion.ProblemaJson, Json)
        };
}
