using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterMusic.Api.Entities
{
    [Table("Instrumento")]
    public class Instrumento
    {
        [Key]
        [Column("id_instrumento")]
        public int IdInstrumento { get; set; }

        [Required]
        [MaxLength(150)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("modelo")]
        public string? Modelo { get; set; }

        [Column("precio")]
        public int Precio { get; set; }

        [Column("stock")]
        public int Stock { get; set; }

        [Column("descripcion")]
        public string? Descripcion { get; set; }

        [Column("foto")]
        public string? Foto { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("id_categoria")]
        public int IdCategoria { get; set; }

        [Column("id_marca")]
        public int IdMarca { get; set; }

        [ForeignKey(nameof(IdCategoria))]
        public Categoria? Categoria { get; set; }

        [ForeignKey(nameof(IdMarca))]
        public Marca? Marca { get; set; }
    }
}