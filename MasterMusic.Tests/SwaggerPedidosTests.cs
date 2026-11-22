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


    [Fact]
    public async Task Contrato_CrearDistinguePedidoNuevoReintentoYResultadoIncierto()
    {
        using var fabrica = new DocumentacionFactory();
        using var cliente = fabrica.CrearCliente();
        using var json = JsonDocument.Parse(await cliente.GetStringAsync("/openapi/v1.json"));
        var raiz = json.RootElement;
        var respuestas = raiz.GetProperty("paths").GetProperty("/api/v1.0/Pedidos")
            .GetProperty("post").GetProperty("responses");
        foreach (var estado in new[] { "200", "201", "202", "400", "401", "403", "404", "409", "503" })
            Assert.True(respuestas.TryGetProperty(estado, out _), estado);
        foreach (var estado in new[] { "200", "201" })
        {
            var pedido = EsquemaRespuesta(raiz, respuestas.GetProperty(estado)).GetProperty("properties");
            Assert.True(pedido.TryGetProperty("idPedido", out _));
            Assert.True(pedido.TryGetProperty("detalles", out _));
            Assert.False(pedido.TryGetProperty("idOperacion", out _));
        }
        var pendiente = respuestas.GetProperty("202");
        var operacion = EsquemaRespuesta(raiz, pendiente).GetProperty("properties");
        Assert.True(operacion.TryGetProperty("idOperacion", out _));
        Assert.True(operacion.TryGetProperty("recuperable", out _));
        Assert.True(pendiente.GetProperty("headers").TryGetProperty("Location", out _));
        Assert.True(pendiente.GetProperty("headers").TryGetProperty("Retry-After", out _));
        Assert.True(respuestas.GetProperty("409").GetProperty("content")
            .TryGetProperty("application/problem+json", out _));
    }

    [Fact]
    public async Task Contrato_AdministracionYRecuperacionConservanSuTipoDeRespuesta()
    {
        using var fabrica = new DocumentacionFactory();
        using var cliente = fabrica.CrearCliente();
        using var json = JsonDocument.Parse(await cliente.GetStringAsync("/openapi/v1.json"));
        var raiz = json.RootElement;
        var rutas = raiz.GetProperty("paths");
        foreach (var accion in new[] { "confirmacion", "cancelacion" })
        {
            var respuestas = rutas.GetProperty("/api/v1.0/admin/Pedidos/{idPedido}/" + accion)
                .GetProperty("post").GetProperty("responses");
            Assert.True(EsquemaRespuesta(raiz, respuestas.GetProperty("200"))
                .GetProperty("properties").TryGetProperty("detalles", out _));
            Assert.True(EsquemaRespuesta(raiz, respuestas.GetProperty("202"))
                .GetProperty("properties").TryGetProperty("idOperacion", out _));
            Assert.True(respuestas.TryGetProperty("409", out _));
        }
        var recuperar = rutas.GetProperty("/api/v1.0/Operaciones/{idOperacion}/recuperacion")
            .GetProperty("post").GetProperty("responses");
        foreach (var estado in new[] { "200", "202" })
            Assert.True(EsquemaRespuesta(raiz, recuperar.GetProperty(estado))
                .GetProperty("properties").TryGetProperty("estadoOperacion", out _));
    }

    private static JsonElement EsquemaRespuesta(JsonElement raiz, JsonElement respuesta)
    {
        var esquema = respuesta.GetProperty("content").GetProperty("application/json").GetProperty("schema");
        if (!esquema.TryGetProperty("$ref", out var referencia)) return esquema;
        var actual = raiz;
        foreach (var segmento in referencia.GetString()![2..].Split('/'))
            actual = actual.GetProperty(segmento.Replace("~1", "/").Replace("~0", "~"));
        return actual;
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
