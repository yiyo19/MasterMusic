using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MasterMusic.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MasterMusic.Tests
{
    public class AuthValidacionTests
    {
        [Theory]
        [InlineData("", "Cliente123!", "Cliente")]
        [InlineData("   ", "Cliente123!", "Cliente")]
        [InlineData("correo-invalido", "Cliente123!", "Cliente")]
        [InlineData("cliente@prueba.cl", "", "Cliente")]
        [InlineData("cliente@prueba.cl", "   ", "Cliente")]
        [InlineData("cliente@prueba.cl", "Cliente123!", "")]
        [InlineData("cliente@prueba.cl", "Cliente123!", "   ")]
        public async Task Registro_DatosInvalidos_Devuelve400_YNoCreaUsuario(
            string correo, string clave, string nombreCompleto)
        {
            using var factory = new MasterMusicFactory();
            using var client = factory.CreateClient();
            using var scope = factory.Services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<MasterMusicContext>();

            var usuariosAntes = await db.Usuarios.CountAsync();

            var respuesta = await client.PostAsJsonAsync(
                $"{AyudaPruebas.RutaAuth}/registro",
                new { correo, clave, nombreCompleto });

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
            Assert.Equal(usuariosAntes, await db.Usuarios.CountAsync());
        }

        [Fact]
        public async Task Registro_SinRolCliente_CreaUsuarioConRolCliente()
        {
            using var factory = new MasterMusicFactory();
            using var client = factory.CreateClient();

            
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<MasterMusicContext>();

                var otrosRoles = await db.Roles
                    .Where(r => r.Nombre != "Administrador")
                    .ToListAsync();

                db.Roles.RemoveRange(otrosRoles);
                await db.SaveChangesAsync();

                var rolRestante = Assert.Single(await db.Roles.ToListAsync());
                Assert.Equal("Administrador", rolRestante.Nombre);
            }

            var correo = $"nuevo_{Guid.NewGuid():N}@prueba.cl";

            var respuesta = await client.PostAsJsonAsync(
                $"{AyudaPruebas.RutaAuth}/registro",
                new
                {
                    correo,
                    clave = "Cliente123!",
                    nombreCompleto = "Cliente de prueba"
                });

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<MasterMusicContext>();

                var usuario = await db.Usuarios
                    .Include(u => u.Rol)
                    .SingleAsync(u => u.Email == correo);

                Assert.Equal("Cliente", usuario.Rol.Nombre);
            }
        }

        [Fact]
        public async Task Login_UsuarioInactivo_Devuelve401_SinToken()
        {
            using var factory = new MasterMusicFactory();
            using var client = factory.CreateClient();

            var correo = $"inactivo_{Guid.NewGuid():N}@prueba.cl";
            var clave = "Cliente123!";

            var registro = await client.PostAsJsonAsync(
                $"{AyudaPruebas.RutaAuth}/registro",
                new { correo, clave, nombreCompleto = "Cliente inactivo" });

            Assert.Equal(HttpStatusCode.OK, registro.StatusCode);

            
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider
                    .GetRequiredService<MasterMusicContext>();

                var usuario = await db.Usuarios
                    .SingleAsync(u => u.Email == correo);

                usuario.Activo = false;
                await db.SaveChangesAsync();
            }

            var respuesta = await client.PostAsJsonAsync(
                $"{AyudaPruebas.RutaAuth}/login",
                new { correo, clave });

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

            var cuerpo = await respuesta.Content
                .ReadFromJsonAsync<JsonElement>();

            Assert.False(cuerpo.TryGetProperty("token", out _));
        }
    }
}
