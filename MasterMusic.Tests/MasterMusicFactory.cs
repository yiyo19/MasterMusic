using MasterMusic.Api.Data;
using MasterMusic.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MasterMusic.Tests
{
    // Arranca la API real dentro de la prueba, pero con una base de datos en memoria
    public class MasterMusicFactory : WebApplicationFactory<Program>
    {
        private readonly string _nombreBd = Guid.NewGuid().ToString();

        public MasterMusicFactory()
        {
            // Program.cs exige esta llave al arrancar; en pruebas usamos una propia
            Environment.SetEnvironmentVariable("Jwt__Key",
                "ClaveSoloParaPruebasMasterMusic_1234567890_abcdef!");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                // Quitamos lo que AddDbContext registró para SQL Server
                var aQuitar = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<MasterMusicContext>) ||
                    (d.ServiceType.IsGenericType &&
                     d.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration") &&
                     d.ServiceType.GenericTypeArguments.Contains(typeof(MasterMusicContext))))
                    .ToList();

                foreach (var d in aQuitar)
                    services.Remove(d);

                // Y registramos el mismo DbContext, pero en memoria
                services.AddDbContext<MasterMusicContext>(options =>
                    options.UseInMemoryDatabase(_nombreBd));
            });
        }

        // Inserta un usuario Administrador directamente en la base en memoria
        public void CrearAdministrador(string correo, string clave)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MasterMusicContext>();

            if (db.Usuarios.Any(u => u.Email == correo))
                return;

            var rol = db.Roles.FirstOrDefault(r => r.Nombre == "Administrador");
            if (rol == null)
            {
                rol = new Rol { Nombre = "Administrador" };
                db.Roles.Add(rol);
                db.SaveChanges();
            }

            db.Usuarios.Add(new Usuario
            {
                Nombre = "Administrador de Pruebas",
                Email = correo,
                Password = BCrypt.Net.BCrypt.HashPassword(clave),
                IdRol = rol.IdRol,
                Activo = true,
                FechaRegistro = DateTime.UtcNow
            });
            db.SaveChanges();
        }
    }
}