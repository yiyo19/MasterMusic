using System.Net;
using System.Net.Http.Json;

namespace MasterMusic.Tests
{
    public class AuthTests : IClassFixture<MasterMusicFactory>
    {
        private readonly HttpClient _client;

        public AuthTests(MasterMusicFactory factory)
        {
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Registro_UsuarioNuevo_Devuelve200()
        {
            var respuesta = await _client.PostAsJsonAsync($"{AyudaPruebas.RutaAuth}/registro",
                new { correo = "nuevo@prueba.cl", clave = "Nuevo123!", nombreCompleto = "Usuario Nuevo" });

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        [Fact]
        public async Task Login_CredencialesCorrectas_DevuelveToken()
        {
            var token = await AyudaPruebas.ObtenerTokenClienteAsync(_client);

            Assert.False(string.IsNullOrWhiteSpace(token));
        }

        [Fact]
        public async Task Login_ClaveIncorrecta_Devuelve401()
        {
            // Primero existe el usuario...
            var correo = $"cliente_{Guid.NewGuid():N}@prueba.cl";
            await _client.PostAsJsonAsync($"{AyudaPruebas.RutaAuth}/registro",
                new { correo, clave = "Cliente123!", nombreCompleto = "Cliente" });

            // ...pero entra con otra clave
            var respuesta = await _client.PostAsJsonAsync($"{AyudaPruebas.RutaAuth}/login",
                new { correo, clave = "ClaveEquivocada9!" });

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }
    }
}