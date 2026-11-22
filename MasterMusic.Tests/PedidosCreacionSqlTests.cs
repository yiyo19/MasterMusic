using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MasterMusic.Ventas.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Tests;

public class PedidosCreacionSqlTests(PedidosSqlFixture fixture) : IClassFixture<PedidosSqlFixture>
{
    private const string Ruta = "/api/v1.0/Pedidos";

    private Task<HttpResponseMessage> CrearAsync(Guid clave, object cuerpo, string? token = null)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, Ruta) { Content = JsonContent.Create(cuerpo) };
        peticion.Headers.Add("Idempotency-Key", clave.ToString());
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token ?? fixture.TokenCliente);
        return EnviarAsync(peticion);
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpRequestMessage peticion)
    {
        using (peticion) return await fixture.Ventas.SendAsync(peticion);
    }

    private static object Solicitud(int id, int cantidad = 1, int precio = 1000) =>
        new { lineas = new[] { new { idInstrumento = id, cantidad, precioEsperado = precio } } };

    private static async Task<string> CodigoAsync(HttpResponseMessage respuesta)
    {
        using var datos = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return datos.RootElement.GetProperty("codigo").GetString()!;
    }

    [SqlServerFact]
    public async Task AltaYReintento_GuardanUnPedidoYUnDescuento()
    {
        var id = await fixture.CrearInstrumentoAsync(5);
        var clave = Guid.NewGuid();
        using var primera = await CrearAsync(clave, Solicitud(id, 2));
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        var pedido = await primera.Content.ReadFromJsonAsync<PedidoRespuesta>();
        Assert.Equal(fixture.IdCliente, pedido!.IdCliente);
        Assert.Equal("Pendiente", pedido.Estado);
        Assert.Equal(2000, pedido.Total);
        Assert.Equal(3, await fixture.StockAsync(id));

        using var segunda = await CrearAsync(clave, Solicitud(id, 2));
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);
        var repetido = await segunda.Content.ReadFromJsonAsync<PedidoRespuesta>();
        Assert.Equal(pedido.IdPedido, repetido!.IdPedido);
        Assert.Equal(3, await fixture.StockAsync(id));
        await using var catalogo = fixture.CrearCatalogo();
        Assert.Equal(1, await catalogo.MovimientosInventario.CountAsync(m => m.IdPedido == pedido.IdPedido));

        using var detalle = await fixture.Ventas.GetAsync(primera.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        Assert.Equal(pedido.IdPedido, (await detalle.Content.ReadFromJsonAsync<PedidoRespuesta>())!.IdPedido);
    }

    [SqlServerFact]
    public async Task MismaClaveConOtrosDatos_Devuelve409()
    {
        var id = await fixture.CrearInstrumentoAsync(5);
        var clave = Guid.NewGuid();
        using var primera = await CrearAsync(clave, Solicitud(id));
        primera.EnsureSuccessStatusCode();
        using var distinta = await CrearAsync(clave, Solicitud(id, 2));
        Assert.Equal(HttpStatusCode.Conflict, distinta.StatusCode);
        Assert.Equal("IDEMPOTENCIA_CONFLICTO", await CodigoAsync(distinta));
        Assert.Equal(4, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task StockInsuficiente_RechazaTodasLasLineasYConservaResultado()
    {
        var primero = await fixture.CrearInstrumentoAsync(4);
        var segundo = await fixture.CrearInstrumentoAsync(0);
        var clave = Guid.NewGuid();
        var solicitud = new { lineas = new[]
        {
            new { idInstrumento = primero, cantidad = 2, precioEsperado = 1000 },
            new { idInstrumento = segundo, cantidad = 1, precioEsperado = 1000 }
        }};
        using var fallo = await CrearAsync(clave, solicitud);
        Assert.Equal(HttpStatusCode.Conflict, fallo.StatusCode);
        Assert.Equal("STOCK_INSUFICIENTE", await CodigoAsync(fallo));
        Assert.Equal(4, await fixture.StockAsync(primero));

        await using (var db = fixture.CrearCatalogo())
        {
            var instrumento = await db.Instrumentos.FindAsync(segundo);
            instrumento!.Stock = 5;
            await db.SaveChangesAsync();
        }
        using var repetido = await CrearAsync(clave, solicitud);
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal("STOCK_INSUFICIENTE", await CodigoAsync(repetido));
        Assert.Equal(4, await fixture.StockAsync(primero));
        Assert.Equal(5, await fixture.StockAsync(segundo));
    }

    [SqlServerFact]
    public async Task PrecioCambiado_DevuelvePrecioVigenteYConservaStock()
    {
        var id = await fixture.CrearInstrumentoAsync(3, 1500);
        using var respuesta = await CrearAsync(Guid.NewGuid(), Solicitud(id));
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("PRECIO_CAMBIADO", await CodigoAsync(respuesta));
        using var datos = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(1500, datos.RootElement.GetProperty("preciosActualizados")[0].GetProperty("precioActual").GetInt32());
        Assert.Equal(3, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task InstrumentoInactivo_Devuelve409()
    {
        var id = await fixture.CrearInstrumentoAsync(3, activo: false);
        using var respuesta = await CrearAsync(Guid.NewGuid(), Solicitud(id));
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal("INSTRUMENTO_INACTIVO", await CodigoAsync(respuesta));
        Assert.Equal(3, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task DuplicadosCantidadCeroYCamposAjenos_Devuelven400()
    {
        var id = await fixture.CrearInstrumentoAsync(4);
        var linea = new { idInstrumento = id, cantidad = 1, precioEsperado = 1000 };
        var solicitudes = new object[]
        {
            new { lineas = new[] { linea, linea } },
            Solicitud(id, 0),
            new { idCliente = 123, lineas = new[] { linea } },
            new { total = 1, lineas = new[] { linea } },
            new { lineas = new object?[] { null } },
            new { lineas = Array.Empty<object>() }
        };
        foreach (var solicitud in solicitudes)
        {
            using var respuesta = await CrearAsync(Guid.NewGuid(), solicitud);
            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
            Assert.Equal("VALIDACION", await CodigoAsync(respuesta));
        }
        Assert.Equal(4, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task TotalFueraDeRango_Devuelve400()
    {
        var id = await fixture.CrearInstrumentoAsync(1);
        using var respuesta = await CrearAsync(Guid.NewGuid(), Solicitud(id, int.MaxValue, int.MaxValue));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("TOTAL_FUERA_DE_RANGO", await CodigoAsync(respuesta));
        Assert.Equal(1, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task RespuestaPerdida_RecuperaPedidoConUnSoloDescuento()
    {
        var id = await fixture.CrearInstrumentoAsync(4);
        var clave = Guid.NewGuid();
        fixture.Fallos.PerderRespuestaDelDescuento = true;
        using var pendiente = await CrearAsync(clave, Solicitud(id, 2));
        Assert.Equal(HttpStatusCode.Accepted, pendiente.StatusCode);
        Assert.Equal(2, await fixture.StockAsync(id));
        var operacion = await pendiente.Content.ReadFromJsonAsync<OperacionRespuesta>();
        Assert.True(operacion!.Recuperable);
        Assert.Equal("EnProceso", operacion.EstadoOperacion);
        Assert.Null(operacion.Pedido);

        using var consulta = await fixture.Ventas.GetAsync($"/api/v1.0/Operaciones?claveCliente={clave}&tipo=Crear");
        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
        Assert.Equal("EnProceso", (await consulta.Content.ReadFromJsonAsync<OperacionRespuesta>())!.EstadoOperacion);
        using var recuperada = await fixture.Ventas.PostAsync($"/api/v1.0/Operaciones/{operacion.IdOperacion}/recuperacion", null);
        Assert.Equal(HttpStatusCode.OK, recuperada.StatusCode);
        var final = await recuperada.Content.ReadFromJsonAsync<OperacionRespuesta>();
        Assert.Equal("Completada", final!.EstadoOperacion);
        Assert.False(final.Recuperable);
        Assert.NotNull(final.Pedido);
        Assert.Equal(2, await fixture.StockAsync(id));
        using var reintento = await CrearAsync(clave, Solicitud(id, 2));
        Assert.Equal(final.IdPedido, (await reintento.Content.ReadFromJsonAsync<PedidoRespuesta>())!.IdPedido);
    }

    [SqlServerFact]
    public async Task InventarioCaido_ConservaOperacionYPermiteRecuperar()
    {
        var id = await fixture.CrearInstrumentoAsync(3);
        var clave = Guid.NewGuid();
        HttpResponseMessage pendiente;
        fixture.Fallos.NoDisponible = true;
        try { pendiente = await CrearAsync(clave, Solicitud(id)); }
        finally { fixture.Fallos.NoDisponible = false; }
        using (pendiente)
        {
            Assert.Equal(HttpStatusCode.Accepted, pendiente.StatusCode);
            Assert.Equal(3, await fixture.StockAsync(id));
        }
        using var recuperado = await CrearAsync(clave, Solicitud(id));
        Assert.Equal(HttpStatusCode.OK, recuperado.StatusCode);
        Assert.Equal(2, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task ClienteAjeno_Recibe404EnPedidoYOperacion()
    {
        var id = await fixture.CrearInstrumentoAsync(3);
        var clave = Guid.NewGuid();
        using var alta = await CrearAsync(clave, Solicitud(id));
        var pedido = await alta.Content.ReadFromJsonAsync<PedidoRespuesta>();
        using var consulta = await fixture.Ventas.GetAsync($"/api/v1.0/Operaciones?claveCliente={clave}&tipo=Crear");
        var operacion = await consulta.Content.ReadFromJsonAsync<OperacionRespuesta>();
        var rutas = new[]
        {
            $"{Ruta}/{pedido!.IdPedido}",
            $"/api/v1.0/Operaciones/{operacion!.IdOperacion}",
            $"/api/v1.0/Operaciones?claveCliente={clave}&tipo=Crear"
        };
        foreach (var ruta in rutas)
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Get, ruta);
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.TokenOtroCliente);
            using var respuesta = await fixture.Ventas.SendAsync(peticion);
            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }
        using var recuperar = new HttpRequestMessage(HttpMethod.Post,
            $"/api/v1.0/Operaciones/{operacion.IdOperacion}/recuperacion");
        recuperar.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.TokenOtroCliente);
        using var ajena = await fixture.Ventas.SendAsync(recuperar);
        Assert.Equal(HttpStatusCode.NotFound, ajena.StatusCode);
        Assert.Equal(2, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task DescuentoInterno_ExigeClaveDeServicio()
    {
        var id = await fixture.CrearInstrumentoAsync(3);
        using var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/v1.0/interno/Movimientos/descuentos")
        {
            Content = JsonContent.Create(new
            {
                idOperacion = Guid.NewGuid(), idPedido = Guid.NewGuid(), idCliente = fixture.IdCliente,
                lineas = new[] { new { idInstrumento = id, cantidad = 1, precioEsperado = 1000 } }
            })
        };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.TokenCliente);
        using var respuesta = await fixture.Catalogo.SendAsync(peticion);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Equal(3, await fixture.StockAsync(id));
    }

    [SqlServerFact]
    public async Task ComprasConcurrentes_NoVendenMasDelStock()
    {
        var id = await fixture.CrearInstrumentoAsync(1);
        var respuestas = await Task.WhenAll(
            CrearHastaResultadoAsync(Guid.NewGuid(), Solicitud(id)),
            CrearHastaResultadoAsync(Guid.NewGuid(), Solicitud(id)));
        try
        {
            Assert.Single(respuestas, r => r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK);
            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(0, await fixture.StockAsync(id));
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }

    [SqlServerFact]
    public async Task MismaClaveConcurrente_ConservaUnPedido()
    {
        var id = await fixture.CrearInstrumentoAsync(4);
        var clave = Guid.NewGuid();
        var respuestas = await Task.WhenAll(CrearHastaResultadoAsync(clave, Solicitud(id)), CrearHastaResultadoAsync(clave, Solicitud(id)));
        try
        {
            Assert.All(respuestas, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK));
            var primero = await respuestas[0].Content.ReadFromJsonAsync<PedidoRespuesta>();
            var segundo = await respuestas[1].Content.ReadFromJsonAsync<PedidoRespuesta>();
            Assert.Equal(primero!.IdPedido, segundo!.IdPedido);
            Assert.Equal(3, await fixture.StockAsync(id));
        }
        finally { foreach (var respuesta in respuestas) respuesta.Dispose(); }
    }
    private async Task<HttpResponseMessage> CrearHastaResultadoAsync(Guid clave, object solicitud)
    {
        for (var intento = 0; ; intento++)
        {
            var respuesta = await CrearAsync(clave, solicitud);
            if (intento == 4 || respuesta.StatusCode is not (HttpStatusCode.Accepted or HttpStatusCode.ServiceUnavailable))
                return respuesta;
            respuesta.Dispose();
            await Task.Delay(250);
        }
    }
}
