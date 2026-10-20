using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterMusic.Api.Entities
{
    [Table("Rol")]
    public class Rol
    {
        [Key]
        [Column("id_rol")]
        public int IdRol { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;
    }
}
