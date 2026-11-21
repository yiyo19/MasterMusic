using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MasterMusic.Ventas.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PedidoCrear
{
    [Required, MinLength(1)]
    public List<LineaPedido> Lineas { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LineaPedido
{
    [Range(1, int.MaxValue)]
    public int IdInstrumento { get; init; }
    [Range(1, int.MaxValue)]
    public int Cantidad { get; init; }
    [Range(1, int.MaxValue)]
    public int PrecioEsperado { get; init; }
}

public sealed record ProblemaPedido(
    int Status, string Codigo, string Detail,
    string Title = "Solicitud de pedido", string Type = "about:blank")
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<PrecioActualizado>? PreciosActualizados { get; init; }
}

public sealed record PrecioActualizado(int IdInstrumento, int PrecioActual);

public sealed record OperacionRespuesta(
    Guid IdOperacion, Guid ClaveCliente, string Tipo, string EstadoOperacion,
    Guid? IdPedido, DateTime FechaCreacion, DateTime FechaActualizacion,
    bool Recuperable, string? CodigoResultado)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PedidoRespuesta? Pedido { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ProblemaPedido? Problema { get; init; }
}
