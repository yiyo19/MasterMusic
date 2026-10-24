using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MasterMusic.Tests
{
    public class InstrumentosCrudTests : IClassFixture<MasterMusicFactory>
    {
        private readonly MasterMusicFactory _factory;
        private readonly HttpClient _client;

        public InstrumentosCrudTests(MasterMusicFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        
        private async Task IniciarSesionComoAdministradorAsync()
        {
            const string correo = "admin.crud@mastermusic.cl";
            const string clave = "AdminCrud123!";

            _factory.CrearAdministrador(correo, clave);
            var token = await AyudaPruebas.ObtenerTokenAsync(_client, correo, clave);
            AyudaPruebas.UsarToken(_client, token);
        }

        [Fact]
        public async Task Post_ComoAdministrador_DatosValidos_Devuelve201()
        {
            await IniciarSesionComoAdministradorAsync();
            var (idCategoria, idMarca) = DatosPrueba.CrearCatalogo(_factory);

            var respuesta = await _client.PostAsJsonAsync(AyudaPruebas.RutaInstrumentos, new
            {
                nombre = "Guitarra Acústica CD-60S",
                modelo = "CD-60S",
                precio = 249990,
                stock = 8,
                descripcion = "Guitarra acústica con tapa de abeto",
                foto = "acustica.jpg",
                activo = true,
                idCategoria,
                idMarca
            });

            var cuerpo = await respuesta.Content.ReadAsStringAsync();
            Assert.True(respuesta.StatusCode == HttpStatusCode.Created,
                $"Se esperaba 201 y llegó {(int)respuesta.StatusCode}. Respuesta: {cuerpo}");
        }

        [Fact]
        public async Task GetPorId_Existente_Devuelve200()
        {
            var id = DatosPrueba.CrearInstrumento(_factory);

            var respuesta = await _client.GetAsync($"{AyudaPruebas.RutaInstrumentos}/{id}");

            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }

        [Fact]
        public async Task GetPorId_Inexistente_Devuelve404()
        {
            var respuesta = await _client.GetAsync($"{AyudaPruebas.RutaInstrumentos}/9999");

            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }

        [Fact]
        public async Task Put_ComoAdministrador_Devuelve204_YCambiaElDato()
        {
            await IniciarSesionComoAdministradorAsync();
            var id = DatosPrueba.CrearInstrumento(_factory);
            var (idCategoria, idMarca) = DatosPrueba.CrearCatalogo(_factory);

            var respuesta = await _client.PutAsJsonAsync($"{AyudaPruebas.RutaInstrumentos}/{id}", new
            {
                idInstrumento = id,
                nombre = "Guitarra Modificada",
                modelo = "Player Series",
                precio = 429990,
                stock = 12,
                descripcion = "Instrumento modificado",
                foto = "guitarra.jpg",
                activo = true,
                idCategoria,
                idMarca
            });

            var cuerpo = await respuesta.Content.ReadAsStringAsync();
            Assert.True(respuesta.StatusCode == HttpStatusCode.NoContent,
                $"Se esperaba 204 y llegó {(int)respuesta.StatusCode}. Respuesta: {cuerpo}");

            
            var consulta = await _client.GetAsync($"{AyudaPruebas.RutaInstrumentos}/{id}");
            var json = await consulta.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Guitarra Modificada", json.GetProperty("nombre").GetString());
        }

        [Fact]
        public async Task Delete_ComoAdministrador_Existente_Devuelve204_YDespuesNoExiste()
        {
            await IniciarSesionComoAdministradorAsync();
            var id = DatosPrueba.CrearInstrumento(_factory);

            var respuesta = await _client.DeleteAsync($"{AyudaPruebas.RutaInstrumentos}/{id}");
            Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

            var consulta = await _client.GetAsync($"{AyudaPruebas.RutaInstrumentos}/{id}");
            Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);
        }
    }
}