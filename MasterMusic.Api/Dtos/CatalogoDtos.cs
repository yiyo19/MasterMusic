using System.ComponentModel.DataAnnotations;
using MasterMusic.Api.Entities;

namespace MasterMusic.Api.Dtos;

public sealed record InstrumentoRespuesta(
    int IdInstrumento, string Nombre, string? Modelo, int Precio, int Stock,
    string? Descripcion, string? Foto, bool Activo, int IdCategoria, int IdMarca,
    string NombreCategoria, string NombreMarca)
{
    public static InstrumentoRespuesta Desde(Instrumento i) => new(
        i.IdInstrumento, i.Nombre, i.Modelo, i.Precio, i.Stock,
        i.Descripcion, i.Foto, i.Activo, i.IdCategoria, i.IdMarca,
        i.Categoria?.Nombre ?? "", i.Marca?.Nombre ?? "");
}

public sealed class CategoriaEntrada
{
    [Required, MaxLength(80)] public string Nombre { get; set; } = "";
    [MaxLength(250)] public string? Descripcion { get; set; }
}
public sealed class MarcaEntrada
{
    [Required, MaxLength(100)] public string Nombre { get; set; } = "";
    [MaxLength(60)] public string? Pais { get; set; }
}
public sealed class EstadoInstrumentoEntrada
{
    [Required] public bool? Activo { get; set; }
}
public sealed class EntradaStock
{
    [Range(1, int.MaxValue)] public int Cantidad { get; set; }
}
