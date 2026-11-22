using System.Net;
using System.Net.Http.Json;
using MasterMusic.Ventas.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Tests;

public partial class PedidosCambiosSqlTests
{
    [SqlServerFact]
    public async Task CancelarInstrumentoInactivo_UsaCantidadesYPreciosHistoricos()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        await using (var db = fixture.CrearCatalogo())
        {
            var actual = (await db.Instrumentos.FindAsync(instrumento))!;
            actual.Activo = false;
            actual.Precio = 9000;
            actual.Nombre = "Nombre posterior";
            await db.SaveChangesAsync();
        }
        using var cancelado = await CambiarAsync(pedido.IdPedido, "cancelacion", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, cancelado.StatusCode);
        Assert.Equal(5, await fixture.StockAsync(instrumento));
        await using var catalogo = fixture.CrearCatalogo();
        var movimiento = await catalogo.MovimientosInventario.Include(m => m.Detalles)
            .SingleAsync(m => m.IdPedido == pedido.IdPedido && m.Tipo == "Devolucion");
        var detalle = Assert.Single(movimiento.Detalles);
        Assert.Equal(2, detalle.Cantidad);
        Assert.Equal(1000, detalle.PrecioUnitario);
        Assert.Equal(pedido.Detalles[0].NombreInstrumento, detalle.NombreInstrumento);
    }

    [SqlServerFact]
    public async Task StockDesbordado_RechazaTodaLaDevolucionYConservaElResultado()
    {
        var primero = await fixture.CrearInstrumentoAsync(5);
        var segundo = await fixture.CrearInstrumentoAsync(5);
        using var alta = await EnviarAsync(fixture.Ventas, HttpMethod.Post, "/api/v1.0/Pedidos",
            fixture.TokenCliente, new { lineas = new[] {
                new { idInstrumento = primero, cantidad = 2, precioEsperado = 1000 },
                new { idInstrumento = segundo, cantidad = 2, precioEsperado = 1000 } } }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        var pedido = (await alta.Content.ReadFromJsonAsync<PedidoRespuesta>())!;
        await using (var db = fixture.CrearCatalogo())
        {
            (await db.Instrumentos.FindAsync(segundo))!.Stock = int.MaxValue;
            await db.SaveChangesAsync();
        }
        var clave = Guid.NewGuid();
        using var rechazo = await CambiarAsync(pedido.IdPedido, "cancelacion", clave);
        Assert.Equal(HttpStatusCode.Conflict, rechazo.StatusCode);
        Assert.Equal("STOCK_FUERA_DE_RANGO", await CodigoAsync(rechazo));
        Assert.Equal(3, await fixture.StockAsync(primero));
        Assert.Equal(int.MaxValue, await fixture.StockAsync(segundo));
        Assert.Equal("Pendiente", (await PedidoAsync(pedido.IdPedido)).Estado);
        Assert.Equal("Rechazada", (await OperacionAsync(clave, "Cancelar")).EstadoOperacion);

        await using (var db = fixture.CrearCatalogo())
        {
            (await db.Instrumentos.FindAsync(segundo))!.Stock = 3;
            await db.SaveChangesAsync();
        }
        using var repetido = await CambiarAsync(pedido.IdPedido, "cancelacion", clave);
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.Equal(3, await fixture.StockAsync(primero));
        Assert.Equal(3, await fixture.StockAsync(segundo));
        using var nuevo = await CambiarAsync(pedido.IdPedido, "cancelacion", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, nuevo.StatusCode);
        Assert.Equal(5, await fixture.StockAsync(primero));
        Assert.Equal(5, await fixture.StockAsync(segundo));
    }

    [SqlServerFact]
    public async Task DevolucionInterna_ReintentoYClavesDistintas_NoDuplicanStock()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        Guid origen;
        await using (var db = fixture.CrearCatalogo())
            origen = await db.MovimientosInventario.Where(m => m.IdPedido == pedido.IdPedido)
                .Select(m => m.IdOperacion).SingleAsync();
        var id = Guid.NewGuid();
        object Cuerpo(Guid operacion, Guid original) =>
            new { idOperacion = operacion, idOperacionOrigen = original, idPedido = pedido.IdPedido, idCliente = pedido.IdCliente };
        const string ruta = "/api/v1.0/interno/Movimientos/devoluciones";
        using var primera = await EnviarAsync(fixture.Catalogo, HttpMethod.Post, ruta,
            fixture.TokenAdministrador, Cuerpo(id, origen), interna: true);
        Assert.Equal(HttpStatusCode.Created, primera.StatusCode);
        using var repetida = await EnviarAsync(fixture.Catalogo, HttpMethod.Post, ruta,
            fixture.TokenAdministrador, Cuerpo(id, origen), interna: true);
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        using var otra = await EnviarAsync(fixture.Catalogo, HttpMethod.Post, ruta,
            fixture.TokenAdministrador, Cuerpo(Guid.NewGuid(), origen), interna: true);
        Assert.Equal(HttpStatusCode.Conflict, otra.StatusCode);
        Assert.Equal("DEVOLUCION_YA_REGISTRADA", await CodigoAsync(otra));
        using var cadena = await EnviarAsync(fixture.Catalogo, HttpMethod.Post, ruta,
            fixture.TokenAdministrador, Cuerpo(Guid.NewGuid(), id), interna: true);
        Assert.Equal(HttpStatusCode.Conflict, cadena.StatusCode);
        Assert.Equal("MOVIMIENTO_ORIGEN_INVALIDO", await CodigoAsync(cadena));
        Assert.Equal(5, await fixture.StockAsync(instrumento));
    }

    [SqlServerFact]
    public async Task DevolucionInterna_ValidaOrigenCredencialesYCantidadesAjenas()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        Guid origen;
        await using (var db = fixture.CrearCatalogo())
            origen = await db.MovimientosInventario.Where(m => m.IdPedido == pedido.IdPedido)
                .Select(m => m.IdOperacion).SingleAsync();
        const string ruta = "/api/v1.0/interno/Movimientos/devoluciones";
        var cuerpo = new { idOperacion = Guid.NewGuid(), idOperacionOrigen = origen, idPedido = pedido.IdPedido, idCliente = pedido.IdCliente };
        using var sinClave = await EnviarAsync(fixture.Catalogo, HttpMethod.Post, ruta, fixture.TokenAdministrador, cuerpo);
        Assert.Equal(HttpStatusCode.Unauthorized, sinClave.StatusCode);
        using var otroCliente = await EnviarAsync(fixture.Catalogo, HttpMethod.Post, ruta, fixture.TokenOtroCliente, cuerpo, interna: true);
        Assert.Equal(HttpStatusCode.Forbidden, otroCliente.StatusCode);
        using var otroPedido = await EnviarAsync(fixture.Catalogo, HttpMethod.Post, ruta, fixture.TokenAdministrador,
            new { cuerpo.idOperacion, cuerpo.idOperacionOrigen, idPedido = Guid.NewGuid(), cuerpo.idCliente }, interna: true);
        Assert.Equal(HttpStatusCode.Conflict, otroPedido.StatusCode);
        using var otroOrigen = await EnviarAsync(fixture.Catalogo, HttpMethod.Post, ruta, fixture.TokenAdministrador,
            new { cuerpo.idOperacion, idOperacionOrigen = Guid.NewGuid(), cuerpo.idPedido, cuerpo.idCliente }, interna: true);
        Assert.Equal(HttpStatusCode.NotFound, otroOrigen.StatusCode);
        using var cantidadAjena = await EnviarAsync(fixture.Catalogo, HttpMethod.Post, ruta, fixture.TokenAdministrador,
            new { cuerpo.idOperacion, cuerpo.idOperacionOrigen, cuerpo.idPedido, cuerpo.idCliente, cantidad = 999 }, interna: true);
        Assert.Equal(HttpStatusCode.BadRequest, cantidadAjena.StatusCode);
        Assert.Equal(3, await fixture.StockAsync(instrumento));
    }

    [SqlServerFact]
    public async Task ClaveDeOperacion_TieneAlcancePorAdministrador()
    {
        var (primero, _, _) = await CrearAsync();
        var (segundo, _, _) = await CrearAsync();
        var clave = Guid.NewGuid();
        using var uno = await CambiarAsync(primero.IdPedido, "confirmacion", clave);
        using var dos = await CambiarAsync(segundo.IdPedido, "confirmacion", clave, token: fixture.TokenOtroAdministrador);
        Assert.Equal(HttpStatusCode.OK, uno.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dos.StatusCode);
        var primera = await OperacionAsync(clave, "Confirmar");
        using var consulta = await EnviarAsync(fixture.Ventas, HttpMethod.Get,
            $"/api/v1.0/Operaciones?claveCliente={clave}&tipo=Confirmar", fixture.TokenOtroAdministrador);
        var segunda = (await consulta.Content.ReadFromJsonAsync<OperacionRespuesta>())!;
        Assert.NotEqual(primera.IdOperacion, segunda.IdOperacion);
        Assert.Equal(primero.IdPedido, primera.IdPedido);
        Assert.Equal(segundo.IdPedido, segunda.IdPedido);
    }
}
