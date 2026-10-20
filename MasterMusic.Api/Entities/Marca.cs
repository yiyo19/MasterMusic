using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterMusic.Api.Entities
{
    [Table("Marca")]
    public class Marca
    {
        [Key]
        [Column("id_marca")]
        public int IdMarca { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(60)]
        [Column("pais")]
        public string? Pais { get; set; }
    }
}