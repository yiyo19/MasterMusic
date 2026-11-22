using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MasterMusic.Api.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class DevolucionInventarioDto
{
    public Guid IdOperacion { get; init; }
    public Guid IdOperacionOrigen { get; init; }
    public Guid IdPedido { get; init; }
    [Range(1, int.MaxValue)]
    public int IdCliente { get; init; }
}
