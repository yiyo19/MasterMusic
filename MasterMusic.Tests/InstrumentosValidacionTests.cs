using System.Net;
using System.Net.Http.Json;

namespace MasterMusic.Tests
{
    public class InstrumentosValidacionTests : IClassFixture<MasterMusicFactory>
    {
        private readonly MasterMusicFactory _factory;
        private readonly HttpClient _client;

        public InstrumentosValidacionTests(MasterMusicFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        
        private async Task<HttpResponseMessage> EnviarPostAsync(string nombre, int precio, int stock)
        {
            const string correo = "admin.validacion@mastermusic.cl";
            const string clave = "AdminValidacion123!";

            _factory.CrearAdministrador(correo, clave);
            var token = await AyudaPruebas.ObtenerTokenAsync(_client, correo, clave);
            AyudaPruebas.UsarToken(_client, token);

            var (idCategoria, idMarca) = DatosPrueba.CrearCatalogo(_factory);

            return await _client.PostAsJsonAsync(AyudaPruebas.RutaInstrumentos, new
            {
                nombre,
                modelo = "Modelo de prueba",
                precio,
                stock,
                descripcion = "Instrumento de prueba",
                foto = "foto.jpg",
                activo = true,
                idCategoria,
                idMarca
            });
        }

        [Fact]
        public async Task Post_NombreVacio_Devuelve400()
        {
            var respuesta = await EnviarPostAsync("", 100000, 5);

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task Post_PrecioNegativo_Devuelve400()
        {
            var respuesta = await EnviarPostAsync("Guitarra de prueba", -5, 5);

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task Post_StockNegativo_Devuelve400()
        {
            var respuesta = await EnviarPostAsync("Guitarra de prueba", 100000, -1);

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }
    }
}