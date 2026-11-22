using System.IdentityModel.Tokens.Jwt;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MasterMusic.Ventas.Api.Controllers;
using MasterMusic.Ventas.Api.Data;
using MasterMusic.Ventas.Api.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace MasterMusic.Tests;

public class PedidosConsultaTests
{
    private const string Ruta = "/api/v1.0/Pedidos";

    [Fact]
    public async Task SinToken_Devuelve401ConProblema()
    {
        using var factory = new VentasFactory();
        using var cliente = factory.CreateClient();
        using var respuesta = await cliente.GetAsync(Ruta);

        await ComprobarProblema(respuesta, HttpStatusCode.Unauthorized, "NO_AUTENTICADO");
        Assert.Contains(respuesta.Headers.WwwAuthenticate, valor => valor.Scheme == "Bearer");
    }

    [Theory]
    [InlineData("firma")]
    [InlineData("emisor")]
    [InlineData("audiencia")]
    [InlineData("vencido")]
    public async Task TokenInvalido_Devuelve401(string defecto)
    {
        using var factory = new VentasFactory();
        var token = factory.CrearToken(
            clave: defecto == "firma" ? new string('x', 64) : null,
            emisor: defecto == "emisor" ? "OtroEmisor" : null,
            audiencia: defecto == "audiencia" ? "OtraAudiencia" : null,
            vencimiento: defecto == "vencido" ? DateTime.UtcNow.AddMinutes(-1) : null);
        using var cliente = factory.ClienteAutenticado(token);
        using var respuesta = await cliente.GetAsync(Ruta);

        await ComprobarProblema(respuesta, HttpStatusCode.Unauthorized, "NO_AUTENTICADO");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("otro")]
    public async Task IdentidadInvalida_Devuelve401(string? identidad)
    {
        using var factory = new VentasFactory();
        using var cliente = factory.ClienteAutenticado(factory.CrearToken(identidad: identidad));
        using var respuesta = await cliente.GetAsync(Ruta);

        await ComprobarProblema(respuesta, HttpStatusCode.Unauthorized, "NO_AUTENTICADO");
    }

    [Fact]
    public async Task Administrador_Devuelve403()
    {
        using var factory = new VentasFactory();
        using var cliente = factory.ClienteAutenticado(factory.CrearToken(rol: "Administrador"));
        using var respuesta = await cliente.GetAsync(Ruta);

        await ComprobarProblema(respuesta, HttpStatusCode.Forbidden, "PROHIBIDO");
    }

    [Fact]
    public async Task ClienteSinPedidos_DevuelveListaVacia()
    {
        using var factory = new VentasFactory();
        using var cliente = factory.ClienteAutenticado(factory.CrearToken());
        using var respuesta = await cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("[]", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Consulta_UsaIdentidadDelJwtYRespetaContrato()
    {
        using var factory = new VentasFactory();
        using var cliente = factory.ClienteAutenticado(factory.CrearToken());
        var fecha = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        var antiguo = factory.GuardarPedido(101, fecha, EstadoPedido.Pendiente);
        factory.GuardarPedido(202, fecha.AddDays(3), EstadoPedido.Confirmado);
        var reciente = factory.GuardarPedido(101, fecha.AddDays(2), EstadoPedido.Cancelado);

        // Un parámetro elegido por el cliente nunca cambia el propietario consultado.
        using var respuesta = await cliente.GetAsync(Ruta + "?idCliente=202");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        var pedidos = json.RootElement.EnumerateArray().ToArray();

        Assert.Equal(2, pedidos.Length);
        Assert.Equal(reciente, pedidos[0].GetProperty("idPedido").GetGuid());
        Assert.Equal(antiguo, pedidos[1].GetProperty("idPedido").GetGuid());
        Assert.All(pedidos, pedido => Assert.Equal(101, pedido.GetProperty("idCliente").GetInt32()));
        Assert.Equal("Cancelado", pedidos[0].GetProperty("estado").GetString());
        Assert.Equal("Pendiente", pedidos[1].GetProperty("estado").GetString());
        Assert.Equal("Solicitud del cliente", pedidos[0].GetProperty("motivoCancelacion").GetString());
        Assert.Equal(JsonValueKind.Null, pedidos[1].GetProperty("motivoCancelacion").ValueKind);
        Assert.Equal(900_000, pedidos[0].GetProperty("total").GetInt64());
        Assert.EndsWith("Z", pedidos[0].GetProperty("fecha").GetString());
        Assert.Equal(
            new[] { "detalles", "estado", "fecha", "idCliente", "idPedido", "motivoCancelacion", "total" },
            pedidos[0].EnumerateObject().Select(propiedad => propiedad.Name).Order().ToArray());
        var detalle = Assert.Single(pedidos[0].GetProperty("detalles").EnumerateArray());
        Assert.Equal("Guitarra de prueba", detalle.GetProperty("nombreInstrumento").GetString());
        Assert.Equal(2, detalle.GetProperty("cantidad").GetInt32());
        Assert.Equal(450_000, detalle.GetProperty("precioUnitario").GetInt32());
        Assert.Equal(
            new[] { "cantidad", "idInstrumento", "nombreInstrumento", "precioUnitario" },
            detalle.EnumerateObject().Select(propiedad => propiedad.Name).Order().ToArray());
    }

    [Fact]
    public async Task BaseDeDatosNoDisponible_Devuelve503()
    {
        using var factory = new VentasFactory(simularFallo: true);
        using var cliente = factory.ClienteAutenticado(factory.CrearToken());
        using var respuesta = await cliente.GetAsync(Ruta);

        await ComprobarProblema(respuesta, HttpStatusCode.ServiceUnavailable, "SERVICIO_NO_DISPONIBLE");
        Assert.DoesNotContain("Fallo simulado", await respuesta.Content.ReadAsStringAsync());
    }

    private static async Task ComprobarProblema(
        HttpResponseMessage respuesta, HttpStatusCode estado, string codigo)
    {
        Assert.Equal(estado, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("about:blank", json.RootElement.GetProperty("type").GetString());
        Assert.Equal((int)estado, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("detail").GetString()));
        Assert.Equal(codigo, json.RootElement.GetProperty("codigo").GetString());
    }

    private sealed class VentasFactory(bool simularFallo = false)
        : WebApplicationFactory<PedidosController>
    {
        private const string Clave = "ClaveExclusivaDePruebasPedidos_abcdefghijklmnopqrstuvwxyz_123456";
        private const string Emisor = "PruebasMasterMusic";
        private const string Audiencia = "PruebasPedidos";
        private readonly string _baseDeDatos = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:MasterMusicVentas",
                "Server=localhost;Database=SoloPruebas;Integrated Security=True");
            builder.UseSetting("Jwt:Key", Clave);
            builder.UseSetting("Jwt:Issuer", Emisor);
            builder.UseSetting("Jwt:Audience", Audiencia);
            builder.ConfigureServices(servicios =>
            {
                servicios.RemoveAll<MasterMusicVentasContext>();
                servicios.RemoveAll<DbContextOptions<MasterMusicVentasContext>>();
                servicios.RemoveAll<IDbContextOptionsConfiguration<MasterMusicVentasContext>>();
                servicios.AddDbContext<MasterMusicVentasContext>(opciones =>
                {
                    opciones.UseInMemoryDatabase(_baseDeDatos);
                    if (simularFallo)
                        opciones.AddInterceptors(new ConsultaNoDisponible());
                });
            });
        }

        public HttpClient ClienteAutenticado(string token)
        {
            var cliente = CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
            cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return cliente;
        }

        public string CrearToken(string? identidad = "101", string rol = "Cliente",
            string? clave = null, string? emisor = null, string? audiencia = null,
            DateTime? vencimiento = null)
        {
            var claims = new List<Claim> { new(ClaimTypes.Role, rol) };
            if (identidad is not null)
                claims.Add(new Claim(ClaimTypes.NameIdentifier, identidad));

            var token = new JwtSecurityToken(
                issuer: emisor ?? Emisor,
                audience: audiencia ?? Audiencia,
                claims: claims,
                notBefore: DateTime.UtcNow.AddMinutes(-10),
                expires: vencimiento ?? DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(clave ?? Clave)),
                    SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public Guid GuardarPedido(int idCliente, DateTime fecha, EstadoPedido estado)
        {
            using var scope = Services.CreateScope();
            var contexto = scope.ServiceProvider.GetRequiredService<MasterMusicVentasContext>();
            var id = Guid.NewGuid();
            var pedido = new Pedido(id, idCliente,
                [new DetallePedido(id, 1, "Guitarra de prueba", 2, 450_000)]);
            if (estado == EstadoPedido.Confirmado)
                pedido.Confirmar();
            if (estado == EstadoPedido.Cancelado)
                pedido.Cancelar("Solicitud del cliente");
            contexto.Pedidos.Add(pedido);
            contexto.Entry(pedido).Property(p => p.Fecha).CurrentValue = fecha;
            contexto.SaveChanges();
            return id;
        }
    }

    private sealed class ConsultaNoDisponible : IQueryExpressionInterceptor
    {
        public Expression QueryCompilationStarting(
            Expression queryExpression, QueryExpressionEventData eventData) =>
            throw new TimeoutException("Fallo simulado de la base de datos.");
    }
}
