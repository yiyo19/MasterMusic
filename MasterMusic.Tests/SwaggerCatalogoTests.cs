using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterMusic.Tests;

public class SwaggerCatalogoTests
{
    [Fact]
    public async Task Contrato_DistingueAccesoPublicoAdministradorEIntegracion()
    {
        using var baseFactory = new MasterMusicFactory();
        using var fabrica = baseFactory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var cliente = fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var json = JsonDocument.Parse(await cliente.GetStringAsync("/swagger/v1/swagger.json"));
        var raiz = json.RootElement;
        Assert.False(raiz.TryGetProperty("security", out var global) && global.GetArrayLength() > 0);
        var rutas = raiz.GetProperty("paths");
        foreach (var (ruta, verbo) in new[]
        {
            ("/api/v{version}/Instrumentos", "get"),
            ("/api/v{version}/Auth/login", "post"),
            ("/api/v{version}/Auth/registro", "post")
        })
        {
            var op = rutas.GetProperty(ruta).GetProperty(verbo);
            Assert.False(op.TryGetProperty("security", out var seguridad) && seguridad.GetArrayLength() > 0);
        }
        var escritura = rutas.GetProperty("/api/v{version}/Instrumentos").GetProperty("post");
        Assert.True(escritura.GetProperty("security")[0].TryGetProperty("Bearer", out _));
        Assert.False(escritura.GetProperty("security")[0].TryGetProperty("ServiceKey", out _));
        var interno = rutas.GetProperty("/api/v{version}/interno/Movimientos/descuentos").GetProperty("post");
        var requisitos = interno.GetProperty("security");
        Assert.Equal(1, requisitos.GetArrayLength());
        Assert.True(requisitos[0].TryGetProperty("Bearer", out _));
        Assert.True(requisitos[0].TryGetProperty("ServiceKey", out _));
        var clave = raiz.GetProperty("components").GetProperty("securitySchemes").GetProperty("ServiceKey");
        Assert.Equal("apiKey", clave.GetProperty("type").GetString());
        Assert.Equal("X-Service-Key", clave.GetProperty("name").GetString());
        var version = escritura.GetProperty("parameters").EnumerateArray()
            .Single(p => p.GetProperty("name").GetString() == "version");
        Assert.Equal("1.0", version.GetProperty("schema").GetProperty("default").GetString());
    }

    [Fact]
    public async Task Contrato_DescribeCreacionEliminacionYDatosPublicosSinContrasena()
    {
        using var baseFactory = new MasterMusicFactory();
        using var fabrica = baseFactory.WithWebHostBuilder(b => b.UseEnvironment("Development"));
        using var cliente = fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var json = JsonDocument.Parse(await cliente.GetStringAsync("/swagger/v1/swagger.json"));
        var raiz = json.RootElement;
        var rutas = raiz.GetProperty("paths");
        var crear = rutas.GetProperty("/api/v{version}/Instrumentos").GetProperty("post").GetProperty("responses");
        Assert.False(crear.TryGetProperty("200", out _));
        Assert.True(crear.GetProperty("201").GetProperty("headers").TryGetProperty("Location", out _));
        var instrumento = Resolver(raiz, crear.GetProperty("201").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema"));
        Assert.True(instrumento.GetProperty("properties").TryGetProperty("stock", out _));
        var borrar = rutas.GetProperty("/api/v{version}/Instrumentos/{id}").GetProperty("delete").GetProperty("responses");
        Assert.True(borrar.TryGetProperty("204", out var sinContenido));
        Assert.False(sinContenido.TryGetProperty("content", out _));
        Assert.True(borrar.GetProperty("409").GetProperty("content").TryGetProperty("application/problem+json", out _));
        var login = rutas.GetProperty("/api/v{version}/Auth/login").GetProperty("post").GetProperty("responses");
        var acceso = Resolver(raiz, login.GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema")).GetProperty("properties");
        Assert.True(acceso.TryGetProperty("token", out _));
        var usuario = Resolver(raiz, acceso.GetProperty("usuario")).GetProperty("properties");
        Assert.True(usuario.TryGetProperty("rol", out _));
        Assert.False(usuario.TryGetProperty("password", out _));
        Assert.False(usuario.TryGetProperty("clave", out _));
        var registro = rutas.GetProperty("/api/v{version}/Auth/registro").GetProperty("post").GetProperty("responses");
        Assert.True(registro.TryGetProperty("409", out _));
        foreach (var ruta in rutas.EnumerateObject())
        foreach (var accion in ruta.Value.EnumerateObject())
        {
            if (accion.Name is not ("get" or "post" or "put" or "patch" or "delete")) continue;
            foreach (var respuesta in accion.Value.GetProperty("responses").EnumerateObject())
            {
                if (!respuesta.Name.StartsWith('2') || respuesta.Name == "204") continue;
                Assert.True(respuesta.Value.GetProperty("content").GetProperty("application/json")
                    .TryGetProperty("schema", out _), ruta.Name);
            }
        }
    }

    private static JsonElement Resolver(JsonElement raiz, JsonElement esquema)
    {
        if (!esquema.TryGetProperty("$ref", out var referencia)) return esquema;
        var actual = raiz;
        foreach (var segmento in referencia.GetString()![2..].Split('/'))
            actual = actual.GetProperty(segmento.Replace("~1", "/").Replace("~0", "~"));
        return actual;
    }
}
