using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterMusic.Api.Entities
{
    [Table("DetalleVenta")]
    public class DetalleVenta
    {
        [Key]
        [Column("id_detalle")]
        public int IdDetalle { get; set; }

        [Column("id_venta")]
        public int IdVenta { get; set; }

        [Column("id_instrumento")]
        public int IdInstrumento { get; set; }

        [Column("cantidad")]
        public int Cantidad { get; set; }

        [Column("precio_unitario")]
        public int PrecioUnitario { get; set; }

        [ForeignKey(nameof(IdVenta))]
        public Venta Venta { get; set; } = null!;

        [ForeignKey(nameof(IdInstrumento))]
        public Instrumento Instrumento { get; set; } = null!;
    }
}