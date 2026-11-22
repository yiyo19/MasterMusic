using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterMusic.Ventas.Api.Entities;

[Table("DetallePedido")]
public class DetallePedido
{
    [Key]
    [Column("id_detalle_pedido")]
    public int IdDetallePedido { get; private set; }

    [Column("id_pedido")]
    public Guid IdPedido { get; private set; }

    [Column("id_instrumento")]
    public int IdInstrumento { get; private set; }

    [MaxLength(150)]
    [Column("nombre_instrumento")]
    public string NombreInstrumento { get; private set; } = string.Empty;

    [Column("cantidad")]
    public int Cantidad { get; private set; }

    [Column("precio_unitario")]
    public int PrecioUnitario { get; private set; }

    private DetallePedido() { } // EF Core lo usa al leer una fila.

    public DetallePedido(Guid idPedido, int idInstrumento, string nombreInstrumento,
        int cantidad, int precioUnitario)
    {
        if (idPedido == Guid.Empty)
            throw new ArgumentException("El identificador del pedido es obligatorio.", nameof(idPedido));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idInstrumento);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cantidad);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(precioUnitario);
        ArgumentException.ThrowIfNullOrWhiteSpace(nombreInstrumento);
        if (nombreInstrumento.Length > 150)
            throw new ArgumentException("El nombre permite hasta 150 caracteres.", nameof(nombreInstrumento));

        IdPedido = idPedido;
        IdInstrumento = idInstrumento;
        NombreInstrumento = nombreInstrumento;
        Cantidad = cantidad;
        PrecioUnitario = precioUnitario;
    }

    public long CalcularSubtotal() => (long)Cantidad * PrecioUnitario;
}
