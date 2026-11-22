using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MasterMusic.Api.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class DescuentoInventarioDto
{
    public Guid IdOperacion { get; init; }
    public Guid IdPedido { get; init; }
    [Range(1, int.MaxValue)]
    public int IdCliente { get; init; }
    [Required, MinLength(1)]
    public List<LineaInventarioDto> Lineas { get; init; } = [];
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LineaInventarioDto
{
    [Range(1, int.MaxValue)]
    public int IdInstrumento { get; init; }
    [Range(1, int.MaxValue)]
    public int Cantidad { get; init; }
    [Range(1, int.MaxValue)]
    public int PrecioEsperado { get; init; }
}

public sealed record MovimientoRespuesta(
    Guid IdOperacion, Guid IdPedido, int IdCliente, string Tipo,
    Guid? IdOperacionOrigen, DateTime Fecha, IReadOnlyList<DetalleMovimientoRespuesta> Detalles);

public sealed record DetalleMovimientoRespuesta(
    int IdInstrumento, string NombreInstrumento, int Cantidad, int PrecioUnitario);
