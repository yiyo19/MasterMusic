using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MasterMusic.Tests
{
    // Funciones compartidas por las pruebas
    public static class AyudaPruebas
    {
        public const string RutaAuth = "/api/v1.0/Auth";
        public const string RutaInstrumentos = "/api/v1.0/Instrumentos";

        // Hace login y devuelve el token JWT
        public static async Task<string> ObtenerTokenAsync(HttpClient client, string correo, string clave)
        {
            var respuesta = await client.PostAsJsonAsync($"{RutaAuth}/login", new { correo, clave });
            respuesta.EnsureSuccessStatusCode();

            var json = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("token").GetString()!;
        }

        // Registra un cliente nuevo (el registro le da el rol Cliente) y devuelve su token
        public static async Task<string> ObtenerTokenClienteAsync(HttpClient client)
        {
            var correo = $"cliente_{Guid.NewGuid():N}@prueba.cl";
            const string clave = "Cliente123!";

            var registro = await client.PostAsJsonAsync($"{RutaAuth}/registro",
                new { correo, clave, nombreCompleto = "Cliente de Prueba" });
            registro.EnsureSuccessStatusCode();

            return await ObtenerTokenAsync(client, correo, clave);
        }

        // Agrega el token a las siguientes peticiones del cliente
        public static void UsarToken(HttpClient client, string token)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
}