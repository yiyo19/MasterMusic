using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterMusic.Api.Entities
{
    [Table("Venta")]
    public class Venta
    {
        [Key]
        [Column("id_venta")]
        public int IdVenta { get; set; }

        [Column("numero_venta")]
        public int NumeroVenta { get; set; }

        [Required]
        [MaxLength(30)]
        [Column("tipo_pago")]
        public string TipoPago { get; set; } = string.Empty;

        [Column("monto")]
        public int Monto { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("estado")]
        public string Estado { get; set; } = "Pendiente";

        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column("id_cliente")]
        public int IdCliente { get; set; }

        [Column("id_vendedor")]
        public int? IdVendedor { get; set; }

        [ForeignKey(nameof(IdCliente))]
        public Usuario Cliente { get; set; } = null!;

        [ForeignKey(nameof(IdVendedor))]
        public Usuario? Vendedor { get; set; }
    }
}