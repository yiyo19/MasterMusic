using System.Net;
using System.Net.Http.Json;

namespace MasterMusic.Tests
{
    public class InstrumentosTests : IClassFixture<MasterMusicFactory>
    {
        private readonly MasterMusicFactory _factory;
        private readonly HttpClient _client;

        public InstrumentosTests(MasterMusicFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task GetInstrumentos_SinToken_Devuelve200()
        {
            var respuesta = await _client.GetAsync(AyudaPruebas.RutaInstrumentos);

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        [Fact]
        public async Task PostInstrumentos_SinToken_Devuelve401()
        {
            var respuesta = await _client.PostAsJsonAsync(AyudaPruebas.RutaInstrumentos,
                new { nombre = "Guitarra de prueba" });

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }

        [Fact]
        public async Task DeleteInstrumentos_ComoCliente_Devuelve403()
        {
            var token = await AyudaPruebas.ObtenerTokenClienteAsync(_client);
            AyudaPruebas.UsarToken(_client, token);

            var respuesta = await _client.DeleteAsync($"{AyudaPruebas.RutaInstrumentos}/9999");

            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }

        [Fact]
        public async Task DeleteInstrumentos_ComoAdministrador_Inexistente_Devuelve404()
        {
            _factory.CrearAdministrador("admin.pruebas@mastermusic.cl", "AdminPruebas123!");
            var token = await AyudaPruebas.ObtenerTokenAsync(_client,
                "admin.pruebas@mastermusic.cl", "AdminPruebas123!");
            AyudaPruebas.UsarToken(_client, token);

            var respuesta = await _client.DeleteAsync($"{AyudaPruebas.RutaInstrumentos}/9999");

            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }
    }
}