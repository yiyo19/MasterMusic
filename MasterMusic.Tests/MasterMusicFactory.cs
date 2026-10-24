using MasterMusic.Api.Data;
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

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Program.cs exige esta llave al arrancar; en pruebas usamos una propia
            builder.UseSetting("Jwt:Key", "ClaveSoloParaPruebasMasterMusic_1234567890_abcdef!");

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
    }
}