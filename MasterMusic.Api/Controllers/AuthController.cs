using Asp.Versioning;
using MasterMusic.Api.Data;
using MasterMusic.Api.Dtos;
using MasterMusic.Api.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MasterMusic.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [TypeFilter(typeof(MasterMusic.Api.Filters.ErroresPersistenciaFilter))]
    public class AuthController : ControllerBase
    {
        private readonly MasterMusicContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(MasterMusicContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("registro")]
        public async Task<IActionResult> Registrar([FromBody] RegistroDto dto)
        {
            if (await _context.Usuarios.AnyAsync(u => u.Email == dto.Correo))
            {
                return CorreoDuplicado();
            }

  
            string claveHash = BCrypt.Net.BCrypt.HashPassword(dto.Clave);


            var rolDefecto = await _context.Roles
           .FirstOrDefaultAsync(r => r.Nombre == "Cliente");


            if (rolDefecto == null)
            {
                rolDefecto = new Rol { Nombre = "Cliente" };
                _context.Roles.Add(rolDefecto);
                await _context.SaveChangesAsync();
            }

            var usuario = new Usuario
            {
                Email = dto.Correo,
                Password = claveHash,
                Nombre = dto.NombreCompleto,
                IdRol = rolDefecto.IdRol,
                Activo = true,
                FechaRegistro = DateTime.UtcNow
            };

            _context.Usuarios.Add(usuario);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException error) when (
                error.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
            {
                // Otra solicitud pudo insertar el correo después de la consulta inicial.
                return CorreoDuplicado();
            }

            return Ok(new { mensaje = "Usuario registrado exitosamente." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Email == dto.Correo);
            if (usuario == null ||
    !usuario.Activo ||
    !BCrypt.Net.BCrypt.Verify(dto.Clave, usuario.Password))
            {
                return Problem(statusCode: 401, type: "about:blank",
                    title: "Credenciales no válidas", detail: "El correo o la contraseña no son válidos.",
                    extensions: new Dictionary<string, object?> { ["codigo"] = "CREDENCIALES_INVALIDAS" });
            }


            string token = GenerarTokenJwt(usuario);

            return Ok(new
            {
                token,
                tipo = "Bearer",
                usuario = new
                {
                    usuario.IdUser,
                    usuario.Nombre,
                    usuario.Email,
                    Rol = usuario.Rol?.Nombre
                }
            });
        }

        private ObjectResult CorreoDuplicado() =>
            Problem(statusCode: 409, type: "about:blank",
                title: "Correo ya registrado", detail: "El correo electrónico ya se encuentra registrado.",
                extensions: new Dictionary<string, object?> { ["codigo"] = "CORREO_DUPLICADO" });

        private string GenerarTokenJwt(Usuario usuario)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.IdUser.ToString()),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Rol?.Nombre ?? "Cliente")
            };

            var expireMinutes = double.Parse(_configuration["Jwt:ExpireMinutes"] ?? "60");

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}




