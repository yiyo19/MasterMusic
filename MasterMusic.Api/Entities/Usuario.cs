using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterMusic.Api.Entities
{
    [Table("Usuario")]
    public class Usuario
    {
        [Key]
        [Column("id_user")]
        public int IdUser { get; set; }

        [Required]
        [MaxLength(150)]
        [Column("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Column("id_rol")]
        public int IdRol { get; set; }

        [Column("activo")]
        public bool Activo { get; set; } = true;

        [Column("fecha_registro")]
        public DateTime FechaRegistro { get; set; } = DateTime.Now;

        [ForeignKey(nameof(IdRol))]
        public Rol Rol { get; set; } = null!;
    }
}