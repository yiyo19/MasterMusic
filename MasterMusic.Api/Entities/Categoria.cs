using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterMusic.Api.Entities
{
    [Table("Categoria")]
    public class Categoria
    {
        [Key]
        [Column("id_categoria")]
        public int IdCategoria { get; set; }

        [Required]
        [MaxLength(80)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(250)]
        [Column("descripcion")]
        public string? Descripcion { get; set; }
    }
}