using System.ComponentModel.DataAnnotations;

namespace MasterMusic.Api.Dtos
{
    public class RegistroDto
    {
        [Required(ErrorMessage = "El correo es obligatorio.")]
        [EmailAddress(ErrorMessage = "Ingresa un correo válido.")]
        [MaxLength(150)]
        public string Correo { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es obligatoria.")]
        public string Clave { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre es obligatorio.")]
        [MaxLength(150)]
        public string NombreCompleto { get; set; } = string.Empty;
    }
}