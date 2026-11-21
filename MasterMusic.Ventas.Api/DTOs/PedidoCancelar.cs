using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MasterMusic.Ventas.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class PedidoCancelar
{
    [Required, StringLength(500)]
    public string Motivo { get; init; } = string.Empty;
}
