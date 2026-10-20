using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterMusic.Api.Entities
{
    [Table("Resena")]
    public class Resena
    {
        [Key]
        [Column("id_resena")]
        public int IdResena { get; set; }

        [Required]
        [Column("comentario")]
        public string Comentario { get; set; } = string.Empty;

        [Column("valoracion")]
        public int Valoracion { get; set; }

        [Column("fecha")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Column("id_instrumento")]
        public int IdInstrumento { get; set; }

        [Column("id_user")]
        public int IdUser { get; set; }

        [ForeignKey(nameof(IdInstrumento))]
        public Instrumento Instrumento { get; set; } = null!;

        [ForeignKey(nameof(IdUser))]
        public Usuario Usuario { get; set; } = null!;
    }
}
