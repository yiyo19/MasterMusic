using System.Net;
using System.Text.Json;
using MasterMusic.Ventas.Api.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MasterMusic.Tests;

public class SwaggerPedidosTests
{
    [Fact]
    public async Task Swagger_SirvePaginaYConfiguracionDelDocumento()
    {
        using var fabrica = new DocumentacionFactory();
        using var cliente = fabrica.CrearCliente();
        var pagina = await cliente.GetStringAsync("/swagger/index.html");
        Assert.Contains("swagger-ui-bundle.js", pagina);
        Assert.Contains("MasterMusic | API de pedidos", pagina);
        var inicializacion = await cliente.GetStringAsync("/swagger/index.js");
        Assert.Contains("/openapi/v1.json", inicializacion);
    }

    [Fact]
    public async Task OpenApi_DescribeRutasRealesRolesYAutorizacionBearer()
    {
        using var fabrica = new DocumentacionFactory();
        using var cliente = fabrica.CrearCliente();
        using var documento = JsonDocument.Parse(await cliente.GetStringAsync("/openapi/v1.json"));
        var raiz = documento.RootElement;
        Assert.Equal("MasterMusic Ventas API", raiz.GetProperty("info").GetProperty("title").GetString());
        var esquema = raiz.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", esquema.GetProperty("type").GetString());
        Assert.Equal("bearer", esquema.GetProperty("scheme").GetString());

        var rutas = raiz.GetProperty("paths");
        Assert.False(rutas.TryGetProperty("/WeatherForecast", out _));
        var consultaCliente = rutas.GetProperty("/api/v1.0/Pedidos").GetProperty("get");
        var consultaAdmin = rutas.GetProperty("/api/v1.0/admin/Pedidos").GetProperty("get");
        Assert.Contains("Cliente", consultaCliente.GetProperty("description").GetString()!);
        Assert.Contains("Administrador", consultaAdmin.GetProperty("description").GetString()!);
        foreach (var ruta in rutas.EnumerateObject())
        foreach (var accion in ruta.Value.EnumerateObject())
        {
            if (accion.Name is not ("get" or "post")) continue;
            Assert.True(accion.Value.GetProperty("security")[0].TryGetProperty("Bearer", out _));
            Assert.True(accion.Value.GetProperty("responses").TryGetProperty("401", out _));
            Assert.True(accion.Value.GetProperty("responses").TryGetProperty("403", out _));
        }
        using var eliminado = await cliente.GetAsync("/weatherforecast");
        Assert.Equal(HttpStatusCode.NotFound, eliminado.StatusCode);
        using var protegido = await cliente.GetAsync("/api/v1.0/Pedidos");
        Assert.Equal(HttpStatusCode.Unauthorized, protegido.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1.0/Pedidos")]
    [InlineData("/api/v1.0/admin/Pedidos/{idPedido}/confirmacion")]
    [InlineData("/api/v1.0/admin/Pedidos/{idPedido}/cancelacion")]
    public async Task Escrituras_DescribenClaveDeReintentoObligatoria(string ruta)
    {
        using var fabrica = new DocumentacionFactory();
        using var cliente = fabrica.CrearCliente();
        using var documento = JsonDocument.Parse(await cliente.GetStringAsync("/openapi/v1.json"));
        var parametros = documento.RootElement.GetProperty("paths").GetProperty(ruta)
            .GetProperty("post").GetProperty("parameters").EnumerateArray();
        var clave = Assert.Single(parametros, p => p.GetProperty("name").GetString() == "Idempotency-Key");
        Assert.Equal("header", clave.GetProperty("in").GetString());
        Assert.True(clave.GetProperty("required").GetBoolean());
        Assert.Equal("uuid", clave.GetProperty("schema").GetProperty("format").GetString());
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Produccion_NoExponeDocumentacionDeDesarrollo(string ruta)
    {
        using var fabrica = new DocumentacionFactory("Production");
        using var cliente = fabrica.CrearCliente();
        using var respuesta = await cliente.GetAsync(ruta);
        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private sealed class DocumentacionFactory(string entorno = "Development")
        : WebApplicationFactory<PedidosController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(entorno);
            // Estas pruebas solo leen documentación; no abren una conexión SQL.
            builder.UseSetting("ConnectionStrings:MasterMusicVentas",
                "Server=localhost;Database=SoloDocumentacion;Integrated Security=True");
            builder.UseSetting("Jwt:Key", "ClaveSoloPruebasDocumentacion_abcdefghijklmnopqrstuvwxyz_123456789");
            builder.UseSetting("Jwt:Issuer", "PruebasDocumentacion");
            builder.UseSetting("Jwt:Audience", "PruebasDocumentacion");
        }

        public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
    }
}
