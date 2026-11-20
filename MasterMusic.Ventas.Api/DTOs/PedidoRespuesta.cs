namespace MasterMusic.Ventas.Api.DTOs;

public sealed record PedidoRespuesta(
    Guid IdPedido,
    int IdCliente,
    DateTime Fecha,
    string Estado,
    long Total,
    string? MotivoCancelacion,
    IReadOnlyList<DetallePedidoRespuesta> Detalles);

public sealed record DetallePedidoRespuesta(
    int IdInstrumento,
    string NombreInstrumento,
    int Cantidad,
    int PrecioUnitario);
