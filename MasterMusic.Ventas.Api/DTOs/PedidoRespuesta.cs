namespace MasterMusic.Ventas.Api.DTOs;

public sealed record PedidoRespuesta(
    Guid IdPedido,
    int IdCliente,
    DateTime Fecha,
    string Estado,
    long Total,
    string? MotivoCancelacion,
    IReadOnlyList<DetallePedidoRespuesta> Detalles)
{
    public static PedidoRespuesta Desde(MasterMusic.Ventas.Api.Entities.Pedido pedido) =>
        new(pedido.IdPedido, pedido.IdCliente, pedido.Fecha, pedido.Estado.ToString(),
            pedido.Total, pedido.MotivoCancelacion,
            pedido.Detalles.OrderBy(d => d.IdInstrumento)
                .Select(d => new DetallePedidoRespuesta(d.IdInstrumento, d.NombreInstrumento,
                    d.Cantidad, d.PrecioUnitario)).ToList());
}

public sealed record DetallePedidoRespuesta(
    int IdInstrumento,
    string NombreInstrumento,
    int Cantidad,
    int PrecioUnitario);
