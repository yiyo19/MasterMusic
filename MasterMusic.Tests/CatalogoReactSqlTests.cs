using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MasterMusic.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Tests;

public class CatalogoReactSqlTests(PedidosSqlFixture fixture) : IClassFixture<PedidosSqlFixture>
{
    private async Task<HttpResponseMessage> Enviar(HttpMethod metodo, string ruta, object? cuerpo = null, bool admin = true)
    {
        using var solicitud = new HttpRequestMessage(metodo, "/api/v1.0/" + ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            admin ? fixture.TokenAdministrador : fixture.TokenCliente);
        if (cuerpo is not null) solicitud.Content = JsonContent.Create(cuerpo);
        return await fixture.Catalogo.SendAsync(solicitud);
    }

    [SqlServerFact]
    public async Task CatalogoPublico_OcultaInactivos_YAdministradorLosConsulta()
    {
        var id = await fixture.CrearInstrumentoAsync(3, activo: false);
        var publico = await fixture.Catalogo.GetFromJsonAsync<JsonElement>("/api/v1.0/Instrumentos");
        Assert.DoesNotContain(publico.EnumerateArray(), i => i.GetProperty("idInstrumento").GetInt32() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await fixture.Catalogo.GetAsync($"/api/v1.0/Instrumentos/{id}")).StatusCode);
        var admin = await Enviar(HttpMethod.Get, $"admin/Instrumentos/{id}");
        Assert.Equal(HttpStatusCode.OK, admin.StatusCode);
        var json = await admin.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Categoria CI", json.GetProperty("nombreCategoria").GetString());
        Assert.False(json.GetProperty("activo").GetBoolean());
    }

    [SqlServerFact]
    public async Task Catalogos_ExigenAutenticacionYRolAdministrador()
    {
        foreach (var ruta in new[] { "Categorias", "Marcas", "admin/Instrumentos" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Catalogo.GetAsync("/api/v1.0/" + ruta)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Get, ruta, admin: false)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Enviar(HttpMethod.Get, ruta)).StatusCode);
        }
    }

    [SqlServerFact]
    public async Task Categoria_CrearEditarConsultarEliminar_SinReferencias()
    {
        var crear = await Enviar(HttpMethod.Post, "Categorias", new { nombre = "Cuerdas", descripcion = "Inicial" });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var id = (await crear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idCategoria").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Put, $"Categorias/{id}", new { nombre = "Cuerdas acústicas", descripcion = "Actualizada" })).StatusCode);
        var consulta = await Enviar(HttpMethod.Get, $"Categorias/{id}");
        Assert.Equal("Cuerdas acústicas", (await consulta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("nombre").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Delete, $"Categorias/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Enviar(HttpMethod.Get, $"Categorias/{id}")).StatusCode);
    }

    [SqlServerFact]
    public async Task Marca_CrearEditarConsultarEliminar_SinReferencias()
    {
        var crear = await Enviar(HttpMethod.Post, "Marcas", new { nombre = "Marca de prueba", pais = "Chile" });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        var id = (await crear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idMarca").GetInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Put, $"Marcas/{id}", new { nombre = "Marca actualizada", pais = "Chile" })).StatusCode);
        var consulta = await Enviar(HttpMethod.Get, $"Marcas/{id}");
        Assert.Equal("Marca actualizada", (await consulta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("nombre").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Delete, $"Marcas/{id}")).StatusCode);
    }

    [SqlServerFact]
    public async Task EliminarCategoriaOMarcaEnUso_NoBorraInstrumentosEnCascada()
    {
        var id = await fixture.CrearInstrumentoAsync(5);
        await using var db = fixture.CrearCatalogo();
        var instrumento = await db.Instrumentos.AsNoTracking().SingleAsync(i => i.IdInstrumento == id);
        Assert.Equal(HttpStatusCode.Conflict, (await Enviar(HttpMethod.Delete, $"Categorias/{instrumento.IdCategoria}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Enviar(HttpMethod.Delete, $"Marcas/{instrumento.IdMarca}")).StatusCode);
        Assert.True(await db.Instrumentos.AnyAsync(i => i.IdInstrumento == id));
        Assert.Equal(5, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task EditarDatos_NoSobrescribeStockNiEstado()
    {
        var id = await fixture.CrearInstrumentoAsync(4, activo: false);
        await using var db = fixture.CrearCatalogo();
        var i = await db.Instrumentos.AsNoTracking().SingleAsync(i => i.IdInstrumento == id);
        var cambio = await Enviar(HttpMethod.Put, $"Instrumentos/{id}", new
        {
            idInstrumento = id, nombre = "Nombre actualizado", precio = 2000,
            stock = 999, activo = true, idCategoria = i.IdCategoria, idMarca = i.IdMarca
        });
        Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);
        var actual = await db.Instrumentos.AsNoTracking().SingleAsync(x => x.IdInstrumento == id);
        Assert.Equal(4, actual.Stock);
        Assert.False(actual.Activo);
        Assert.Equal("Nombre actualizado", actual.Nombre);
    }

    [SqlServerFact]
    public async Task EntradasSimultaneas_SumanSinPerderExistencias()
    {
        var id = await fixture.CrearInstrumentoAsync(10);
        var resultados = await Task.WhenAll(
            Enviar(HttpMethod.Post, $"Instrumentos/{id}/existencias", new { cantidad = 3 }),
            Enviar(HttpMethod.Post, $"Instrumentos/{id}/existencias", new { cantidad = 5 }));
        Assert.All(resultados, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Equal(18, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task EntradaStock_ValidaCantidadYNoDesborda()
    {
        var id = await fixture.CrearInstrumentoAsync(int.MaxValue);
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(HttpMethod.Post, $"Instrumentos/{id}/existencias", new { cantidad = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Enviar(HttpMethod.Post, $"Instrumentos/{id}/existencias", new { cantidad = 1 })).StatusCode);
        Assert.Equal(int.MaxValue, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task Cliente_NoPuedeCambiarStockNiEstado()
    {
        var id = await fixture.CrearInstrumentoAsync(2);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Post, $"Instrumentos/{id}/existencias", new { cantidad = 4 }, admin: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Enviar(HttpMethod.Patch, $"Instrumentos/{id}/estado", new { activo = false }, admin: false)).StatusCode);
        Assert.Equal(2, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task Estado_AusenteEsInvalido_YActivarPublicaInstrumento()
    {
        var id = await fixture.CrearInstrumentoAsync(2, activo: false);
        Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(HttpMethod.Patch, $"Instrumentos/{id}/estado", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Enviar(HttpMethod.Patch, $"Instrumentos/{id}/estado", new { activo = true })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Catalogo.GetAsync($"/api/v1.0/Instrumentos/{id}")).StatusCode);
    }

    [SqlServerFact]
    public async Task InstrumentoConPedido_NoSeElimina()
    {
        var id = await fixture.CrearInstrumentoAsync(1);
        using var solicitud = new HttpRequestMessage(HttpMethod.Post, "/api/v1.0/Pedidos")
        {
            Content = JsonContent.Create(new { lineas = new[] { new { idInstrumento = id, cantidad = 1, precioEsperado = 1000 } } })
        };
        solicitud.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var pedido = await fixture.Ventas.SendAsync(solicitud);
        Assert.Equal(HttpStatusCode.Created, pedido.StatusCode);
        using var eliminado = await Enviar(HttpMethod.Delete, $"Instrumentos/{id}");
        Assert.Equal(HttpStatusCode.Conflict, eliminado.StatusCode);
        var error = await eliminado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RECURSO_EN_USO", error.GetProperty("codigo").GetString());
        Assert.Equal(0, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task InstrumentoConStockSinHistorial_RechazaEliminarYConservaDatos()
    {
        var id = await fixture.CrearInstrumentoAsync(5);
        using var eliminado = await Enviar(HttpMethod.Delete, $"Instrumentos/{id}");
        Assert.Equal(HttpStatusCode.Conflict, eliminado.StatusCode);
        Assert.Equal("application/problem+json", eliminado.Content.Headers.ContentType?.MediaType);
        var error = await eliminado.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("STOCK_NO_CERO", error.GetProperty("codigo").GetString());
        Assert.Equal(5, await fixture.StockAsync(id));
        using var consulta = await Enviar(HttpMethod.Get, $"admin/Instrumentos/{id}");
        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
    }

    [SqlServerFact]
    public async Task InstrumentoSinStockNiHistorial_SeElimina()
    {
        var id = await fixture.CrearInstrumentoAsync(0);
        using var eliminado = await Enviar(HttpMethod.Delete, $"Instrumentos/{id}");
        Assert.Equal(HttpStatusCode.NoContent, eliminado.StatusCode);
        using var consulta = await Enviar(HttpMethod.Get, $"admin/Instrumentos/{id}");
        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);
        await using var db = fixture.CrearCatalogo();
        Assert.False(await db.Instrumentos.AnyAsync(i => i.IdInstrumento == id));
    }

    [SqlServerFact]
    public async Task EliminarYSumarStockConcurrentes_NoEliminanExistencias()
    {
        var id = await fixture.CrearInstrumentoAsync(0);
        var eliminacion = Enviar(HttpMethod.Delete, $"Instrumentos/{id}");
        var entrada = Enviar(HttpMethod.Post, $"Instrumentos/{id}/existencias", new { cantidad = 3 });
        await Task.WhenAll(eliminacion, entrada);
        using var eliminado = await eliminacion;
        using var agregado = await entrada;
        await using var db = fixture.CrearCatalogo();
        var instrumento = await db.Instrumentos.AsNoTracking().SingleOrDefaultAsync(i => i.IdInstrumento == id);

        if (agregado.StatusCode == HttpStatusCode.OK)
        {
            Assert.Equal(HttpStatusCode.Conflict, eliminado.StatusCode);
            Assert.NotNull(instrumento);
            Assert.Equal(3, instrumento.Stock);
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, agregado.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, eliminado.StatusCode);
            Assert.Null(instrumento);
        }
    }

    [SqlServerFact]
    public async Task Cors_SoloPermiteElOrigenConfigurado_EnAmbasApis()
    {
        foreach (var cliente in new[] { fixture.Catalogo, fixture.Ventas })
        {
            foreach (var origen in new[] { "http://localhost:5173", "https://no-permitido.example" })
            {
                using var solicitud = new HttpRequestMessage(HttpMethod.Options, "/api/v1.0/Pedidos");
                solicitud.Headers.Add("Origin", origen);
                solicitud.Headers.Add("Access-Control-Request-Method", "POST");
                solicitud.Headers.Add("Access-Control-Request-Headers", "authorization,content-type,idempotency-key");
                using var respuesta = await cliente.SendAsync(solicitud);
                if (origen == "http://localhost:5173")
                {
                    Assert.True(respuesta.Headers.TryGetValues("Access-Control-Allow-Origin", out var valores));
                    Assert.Equal(origen, valores.Single());
                }
                else Assert.False(respuesta.Headers.Contains("Access-Control-Allow-Origin"));
            }
        }
    }
}
