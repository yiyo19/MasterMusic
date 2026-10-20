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
                return BadRequest(new { mensaje = "El correo electrónico ya se encuentra registrado." });
            }

  
            string claveHash = BCrypt.Net.BCrypt.HashPassword(dto.Clave);

           
            var rolDefecto = await _context.Roles.FirstOrDefaultAsync(r => r.Nombre == "Cliente")
                             ?? await _context.Roles.FirstOrDefaultAsync();

            
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
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Usuario registrado exitosamente." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Email == dto.Correo);

            if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Clave, usuario.Password))
            {
                return Unauthorized(new { mensaje = "Credenciales incorrectas." });
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