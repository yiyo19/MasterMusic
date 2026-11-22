using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MasterMusic.Api.Controllers;
using MasterMusic.Api.Data;
using MasterMusic.Api.Entities;
using MasterMusic.Ventas.Api.Controllers;
using MasterMusic.Ventas.Api.Data;
using MasterMusic.Ventas.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MasterMusic.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MASTERMUSIC_TEST_SQL")))
            Skip = "Requiere MASTERMUSIC_TEST_SQL. GitHub Actions configura SQL Server de pruebas.";
    }
}

public sealed class PedidosSqlFixture : IAsyncLifetime
{
    public const string ClaveServicio = "CredencialExclusivaDeIntegracionCI_1234567890_abcdef";
    private const string ClaveJwt = "FirmaExclusivaDeIntegracionCI_abcdefghijklmnopqrstuvwxyz_1234567890";
    private string _conexionCatalogo = "";
    private string _conexionVentas = "";
    private string _baseCatalogo = "";
    private string _baseVentas = "";
    private CatalogoFactory? _catalogo;
    private VentasFactory? _ventas;
    public HttpClient Catalogo { get; private set; } = null!;
    public HttpClient Ventas { get; private set; } = null!;
    public string TokenCliente { get; private set; } = "";
    public string TokenOtroCliente { get; private set; } = "";
    public int IdCliente { get; private set; }
    public string TokenAdministrador { get; private set; } = "";
    public string TokenOtroAdministrador { get; private set; } = "";
    public FallosInventario Fallos { get; } = new();

    public async Task InitializeAsync()
    {
        var conexion = Environment.GetEnvironmentVariable("MASTERMUSIC_TEST_SQL");
        if (string.IsNullOrWhiteSpace(conexion)) return;

        var identificador = Guid.NewGuid().ToString("N");
        _baseCatalogo = $"MasterMusicCI_Catalogo_{identificador}";
        _baseVentas = $"MasterMusicCI_Ventas_{identificador}";
        _conexionCatalogo = new SqlConnectionStringBuilder(conexion) { InitialCatalog = _baseCatalogo }.ConnectionString;
        _conexionVentas = new SqlConnectionStringBuilder(conexion) { InitialCatalog = _baseVentas }.ConnectionString;
        await using (var catalogo = CrearCatalogo())
            await catalogo.Database.EnsureCreatedAsync();
        await using (var ventas = CrearVentas())
            await ventas.Database.EnsureCreatedAsync();

        _catalogo = new CatalogoFactory(_conexionCatalogo);
        Catalogo = _catalogo.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        _ventas = new VentasFactory(_conexionVentas, _catalogo, Fallos);
        Ventas = _ventas.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var cliente = await RegistrarClienteAsync();
        TokenCliente = cliente.Token;
        IdCliente = cliente.Id;
        TokenOtroCliente = (await RegistrarClienteAsync()).Token;
        TokenAdministrador = (await RegistrarClienteAsync(administrador: true)).Token;
        TokenOtroAdministrador = (await RegistrarClienteAsync(administrador: true)).Token;
        Ventas.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TokenCliente);
    }

    private async Task<(string Token, int Id)> RegistrarClienteAsync(bool administrador = false)
    {
        var correo = $"prueba_{Guid.NewGuid():N}@example.com";
        var clave = Guid.NewGuid().ToString("N");
        using var registro = await Catalogo.PostAsJsonAsync("/api/v1.0/Auth/registro",
            new { correo, clave, nombreCompleto = "Cliente de integración SQL" });
        registro.EnsureSuccessStatusCode();
        if (administrador)
        {
            await using var db = CrearCatalogo();
            var usuario = await db.Usuarios.SingleAsync(u => u.Email == correo);
            usuario.IdRol = await db.Roles.Where(r => r.Nombre == "Administrador").Select(r => r.IdRol).SingleAsync();
            await db.SaveChangesAsync();
        }
        using var login = await Catalogo.PostAsJsonAsync("/api/v1.0/Auth/login", new { correo, clave });
        login.EnsureSuccessStatusCode();
        using var datos = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        return (datos.RootElement.GetProperty("token").GetString()!,
            datos.RootElement.GetProperty("usuario").GetProperty("idUser").GetInt32());
    }

    public MasterMusicContext CrearCatalogo() =>
        new(new DbContextOptionsBuilder<MasterMusicContext>().UseSqlServer(_conexionCatalogo).Options);

    public MasterMusicVentasContext CrearVentas() =>
        new(new DbContextOptionsBuilder<MasterMusicVentasContext>().UseSqlServer(_conexionVentas).Options);

    public async Task<int> CrearInstrumentoAsync(int stock, int precio = 1000, bool activo = true)
    {
        await using var db = CrearCatalogo();
        var categoria = new Categoria { Nombre = "Categoria CI" };
        var marca = new Marca { Nombre = "Marca CI" };
        db.AddRange(categoria, marca);
        await db.SaveChangesAsync();
        var instrumento = new Instrumento
        {
            Nombre = "Instrumento CI", Precio = precio, Stock = stock, Activo = activo,
            IdCategoria = categoria.IdCategoria, IdMarca = marca.IdMarca
        };
        db.Instrumentos.Add(instrumento);
        await db.SaveChangesAsync();
        return instrumento.IdInstrumento;
    }

    public async Task<int> StockAsync(int id)
    {
        await using var db = CrearCatalogo();
        return await db.Instrumentos.Where(i => i.IdInstrumento == id).Select(i => i.Stock).SingleAsync();
    }

    public async Task DisposeAsync()
    {
        Ventas?.Dispose();
        Catalogo?.Dispose();
        if (_ventas is not null) await _ventas.DisposeAsync();
        if (_catalogo is not null) await _catalogo.DisposeAsync();
        // Solo elimina las dos bases temporales generadas por esta fixture.
        if (_baseVentas.StartsWith("MasterMusicCI_Ventas_", StringComparison.Ordinal))
        {
            await using var db = CrearVentas();
            if (db.Database.GetDbConnection().Database != _baseVentas)
                throw new InvalidOperationException("La base no corresponde a esta prueba.");
            await db.Database.EnsureDeletedAsync();
        }
        if (_baseCatalogo.StartsWith("MasterMusicCI_Catalogo_", StringComparison.Ordinal))
        {
            await using var db = CrearCatalogo();
            if (db.Database.GetDbConnection().Database != _baseCatalogo)
                throw new InvalidOperationException("La base no corresponde a esta prueba.");
            await db.Database.EnsureDeletedAsync();
        }
    }

    public sealed class FallosInventario
    {
        public bool NoDisponible { get; set; }
        public bool PerderRespuestaDelDescuento { get; set; }
        public bool PerderRespuestaDeLaDevolucion { get; set; }
        public int Solicitudes;
    }

    private sealed class CatalogoFactory(string conexion) : WebApplicationFactory<AuthController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
            builder.UseSetting("ConnectionStrings:MasterMusic", conexion);
            builder.UseSetting("Jwt:Key", ClaveJwt);
            builder.UseSetting("Jwt:Issuer", "MasterMusicSqlCI");
            builder.UseSetting("Jwt:Audience", "ClientesSqlCI");
            builder.UseSetting("Inventario:ServiceKey", ClaveServicio);
            builder.UseSetting("Admin:Email", "");
            builder.UseSetting("Admin:Password", "");
        }
    }

    private sealed class VentasFactory(string conexion, CatalogoFactory catalogo, FallosInventario fallos)
        : WebApplicationFactory<PedidosController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
            builder.UseSetting("ConnectionStrings:MasterMusicVentas", conexion);
            builder.UseSetting("Jwt:Key", ClaveJwt);
            builder.UseSetting("Jwt:Issuer", "MasterMusicSqlCI");
            builder.UseSetting("Jwt:Audience", "ClientesSqlCI");
            builder.UseSetting("Inventario:ServiceKey", ClaveServicio);
            builder.ConfigureServices(servicios =>
                servicios.AddHttpClient<InventarioClient>()
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        new InventarioHandler(catalogo.Server.CreateHandler(), fallos)));
        }
    }

    private sealed class InventarioHandler(HttpMessageHandler interno, FallosInventario fallos) : DelegatingHandler(interno)
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref fallos.Solicitudes);
            if (fallos.NoDisponible)
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

            var respuesta = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post && respuesta.IsSuccessStatusCode && fallos.PerderRespuestaDelDescuento
                && request.RequestUri!.AbsolutePath.EndsWith("/descuentos", StringComparison.Ordinal))
            {
                fallos.PerderRespuestaDelDescuento = false;
                respuesta.Dispose();
                throw new HttpRequestException("Respuesta perdida después de confirmar inventario.");
            }
            if (request.Method == HttpMethod.Post && respuesta.IsSuccessStatusCode
                && fallos.PerderRespuestaDeLaDevolucion
                && request.RequestUri!.AbsolutePath.EndsWith("/devoluciones", StringComparison.Ordinal))
            {
                fallos.PerderRespuestaDeLaDevolucion = false;
                respuesta.Dispose();
                throw new HttpRequestException("Respuesta perdida después de devolver inventario.");
            }
            return respuesta;
        }
    }
}
