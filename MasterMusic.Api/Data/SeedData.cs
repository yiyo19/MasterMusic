using MasterMusic.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Api.Data
{
    public static class SeedData
    {
        public static async Task InicializarAsync(MasterMusicContext db, IConfiguration config)
        {
            
            foreach (var nombre in new[] { "Cliente", "Administrador" })
            {
                if (!await db.Roles.AnyAsync(r => r.Nombre == nombre))
                    db.Roles.Add(new Rol { Nombre = nombre });
            }
            await db.SaveChangesAsync();

            
            var email = config["Admin:Email"];
            var clave = config["Admin:Password"];

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(clave))
                return;

            if (await db.Usuarios.AnyAsync(u => u.Email == email))
                return;

            var rolAdmin = await db.Roles.FirstAsync(r => r.Nombre == "Administrador");

            db.Usuarios.Add(new Usuario
            {
                Nombre = "Administrador",
                Email = email,
                Password = BCrypt.Net.BCrypt.HashPassword(clave),
                IdRol = rolAdmin.IdRol,
                Activo = true,
                FechaRegistro = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }
    }
}