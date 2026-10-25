using System.Net;
using System.Net.Http.Json;

namespace MasterMusic.Tests
{
    public class InstrumentosReferenciasTests : IClassFixture<MasterMusicFactory>
    {
        private readonly MasterMusicFactory _factory;
        private readonly HttpClient _client;

        public InstrumentosReferenciasTests(MasterMusicFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        private async Task IniciarSesionComoAdministradorAsync()
        {
            const string correo = "admin.referencias@mastermusic.cl";
            const string clave = "AdminReferencias123!";

            _factory.CrearAdministrador(correo, clave);
            var token = await AyudaPruebas.ObtenerTokenAsync(_client, correo, clave);
            AyudaPruebas.UsarToken(_client, token);
        }

        private static object CrearCuerpo(int id, int idCategoria, int idMarca) => new
        {
            idInstrumento = id,
            nombre = "Instrumento de prueba",
            modelo = "Modelo X",
            precio = 100000,
            stock = 5,
            descripcion = "Prueba",
            foto = "foto.jpg",
            activo = true,
            idCategoria,
            idMarca
        };

        [Fact]
        public async Task Post_CategoriaInexistente_Devuelve400()
        {
            await IniciarSesionComoAdministradorAsync();
            var (_, idMarca) = DatosPrueba.CrearCatalogo(_factory);

            var respuesta = await _client.PostAsJsonAsync(AyudaPruebas.RutaInstrumentos,
                CrearCuerpo(0, 999, idMarca));

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task Post_MarcaInexistente_Devuelve400()
        {
            await IniciarSesionComoAdministradorAsync();
            var (idCategoria, _) = DatosPrueba.CrearCatalogo(_factory);

            var respuesta = await _client.PostAsJsonAsync(AyudaPruebas.RutaInstrumentos,
                CrearCuerpo(0, idCategoria, 999));

            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }

        [Fact]
        public async Task Put_InstrumentoInexistente_Devuelve404()
        {
            await IniciarSesionComoAdministradorAsync();
            var (idCategoria, idMarca) = DatosPrueba.CrearCatalogo(_factory);

            var respuesta = await _client.PutAsJsonAsync($"{AyudaPruebas.RutaInstrumentos}/9999",
                CrearCuerpo(9999, idCategoria, idMarca));

            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }
    }
}