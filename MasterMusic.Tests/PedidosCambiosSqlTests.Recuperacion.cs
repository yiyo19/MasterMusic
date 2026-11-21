using System.Net;
using System.Net.Http.Json;
using MasterMusic.Ventas.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Tests;

public partial class PedidosCambiosSqlTests
{
    [SqlServerFact]
    public async Task RespuestaPerdida_DevolucionSeRecuperaSinRepetirStock()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        var clave = Guid.NewGuid();
        fixture.Fallos.PerderRespuestaDeLaDevolucion = true;
        try
        {
            using var intento = await CambiarAsync(pedido.IdPedido, "cancelacion", clave);
            Assert.Equal(HttpStatusCode.Accepted, intento.StatusCode);
            Assert.NotNull(intento.Headers.Location);
            Assert.NotNull(intento.Headers.RetryAfter);
            var operacion = (await intento.Content.ReadFromJsonAsync<OperacionRespuesta>())!;
            Assert.Equal("EnProceso", operacion.EstadoOperacion);
            Assert.True(operacion.Recuperable);
            Assert.Equal("Pendiente", (await PedidoAsync(pedido.IdPedido)).Estado);
            Assert.Equal(5, await fixture.StockAsync(instrumento));

            var llamadas = fixture.Fallos.Solicitudes;
            Assert.Equal("EnProceso", (await OperacionAsync(clave, "Cancelar")).EstadoOperacion);
            Assert.Equal(llamadas, fixture.Fallos.Solicitudes);
            using var confirmar = await CambiarAsync(pedido.IdPedido, "confirmacion", Guid.NewGuid());
            Assert.Equal(HttpStatusCode.Conflict, confirmar.StatusCode);
            Assert.Equal("OPERACION_EN_CURSO", await CodigoAsync(confirmar));

            foreach (var token in new[] { fixture.TokenOtroAdministrador, fixture.TokenCliente })
            {
                using var ajena = await EnviarAsync(fixture.Ventas, HttpMethod.Get,
                    $"/api/v1.0/Operaciones/{operacion.IdOperacion}", token);
                Assert.Equal(HttpStatusCode.NotFound, ajena.StatusCode);
                using var buscar = await EnviarAsync(fixture.Ventas, HttpMethod.Get,
                    $"/api/v1.0/Operaciones?claveCliente={clave}&tipo=Cancelar", token);
                Assert.Equal(HttpStatusCode.NotFound, buscar.StatusCode);
                using var recuperarAjena = await EnviarAsync(fixture.Ventas, HttpMethod.Post,
                    $"/api/v1.0/Operaciones/{operacion.IdOperacion}/recuperacion", token);
                Assert.Equal(HttpStatusCode.NotFound, recuperarAjena.StatusCode);
            }

            for (var repeticion = 0; repeticion < 2; repeticion++)
            {
                using var recuperado = await EnviarAsync(fixture.Ventas, HttpMethod.Post,
                    $"/api/v1.0/Operaciones/{operacion.IdOperacion}/recuperacion", fixture.TokenAdministrador);
                Assert.Equal(HttpStatusCode.OK, recuperado.StatusCode);
                var resultado = (await recuperado.Content.ReadFromJsonAsync<OperacionRespuesta>())!;
                Assert.Equal("Completada", resultado.EstadoOperacion);
                Assert.Equal("Cancelado", resultado.Pedido!.Estado);
                Assert.False(resultado.Recuperable);
            }
            Assert.Equal(5, await fixture.StockAsync(instrumento));
            await using var db = fixture.CrearCatalogo();
            Assert.Equal(1, await db.MovimientosInventario.CountAsync(m => m.IdPedido == pedido.IdPedido && m.Tipo == "Devolucion"));
        }
        finally { fixture.Fallos.PerderRespuestaDeLaDevolucion = false; }
    }

    [SqlServerFact]
    public async Task InventarioCaido_ConservaIntencionYExcluyeOtrosCambios()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        var clave = Guid.NewGuid();
        fixture.Fallos.NoDisponible = true;
        try
        {
            using var intento = await CambiarAsync(pedido.IdPedido, "cancelacion", clave);
            Assert.Equal(HttpStatusCode.Accepted, intento.StatusCode);
            Assert.Equal("Pendiente", (await PedidoAsync(pedido.IdPedido)).Estado);
            Assert.Equal(3, await fixture.StockAsync(instrumento));
            foreach (var accion in new[] { "confirmacion", "cancelacion" })
            {
                using var otra = await CambiarAsync(pedido.IdPedido, accion, Guid.NewGuid(),
                    token: fixture.TokenOtroAdministrador);
                Assert.Equal(HttpStatusCode.Conflict, otra.StatusCode);
                Assert.Equal("OPERACION_EN_CURSO", await CodigoAsync(otra));
            }
        }
        finally { fixture.Fallos.NoDisponible = false; }

        using var repetido = await CambiarAsync(pedido.IdPedido, "cancelacion", clave);
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.Equal("Cancelado", (await PedidoAsync(pedido.IdPedido)).Estado);
        Assert.Equal(5, await fixture.StockAsync(instrumento));
    }

    [SqlServerFact]
    public async Task ConfirmacionYCancelacionConcurrentes_SoloUnaSeCompleta()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        var respuestas = await Task.WhenAll(
            CambiarHastaResultadoAsync(pedido.IdPedido, "confirmacion", Guid.NewGuid()),
            CambiarHastaResultadoAsync(pedido.IdPedido, "cancelacion", Guid.NewGuid()));
        try
        {
            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
            var final = await PedidoAsync(pedido.IdPedido);
            Assert.True(final.Estado is "Confirmado" or "Cancelado");
            Assert.Equal(final.Estado == "Confirmado" ? 3 : 5, await fixture.StockAsync(instrumento));
        }
        finally { foreach (var r in respuestas) r.Dispose(); }
    }

    [SqlServerFact]
    public async Task CancelacionesConcurrentes_MismaClaveDevuelveUnaVez()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        var clave = Guid.NewGuid();
        var respuestas = await Task.WhenAll(
            CambiarHastaResultadoAsync(pedido.IdPedido, "cancelacion", clave),
            CambiarHastaResultadoAsync(pedido.IdPedido, "cancelacion", clave));
        try
        {
            Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(5, await fixture.StockAsync(instrumento));
            await using var db = fixture.CrearCatalogo();
            Assert.Equal(1, await db.MovimientosInventario.CountAsync(m => m.IdPedido == pedido.IdPedido && m.Tipo == "Devolucion"));
        }
        finally { foreach (var r in respuestas) r.Dispose(); }
    }

    [SqlServerFact]
    public async Task DosAdministradoresCancelan_SoloUnaDevolucion()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        var respuestas = await Task.WhenAll(
            CambiarHastaResultadoAsync(pedido.IdPedido, "cancelacion", Guid.NewGuid()),
            CambiarHastaResultadoAsync(pedido.IdPedido, "cancelacion", Guid.NewGuid(), fixture.TokenOtroAdministrador));
        try
        {
            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(5, await fixture.StockAsync(instrumento));
            await using var db = fixture.CrearCatalogo();
            Assert.Equal(1, await db.MovimientosInventario.CountAsync(m => m.IdPedido == pedido.IdPedido && m.Tipo == "Devolucion"));
        }
        finally { foreach (var r in respuestas) r.Dispose(); }
    }

    [SqlServerFact]
    public async Task RepetirAltaDePedidoCancelado_NoVuelveADescontar()
    {
        var (pedido, instrumento, claveAlta) = await CrearAsync();
        using var cancelado = await CambiarAsync(pedido.IdPedido, "cancelacion", Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, cancelado.StatusCode);
        using var repetido = await EnviarAsync(fixture.Ventas, HttpMethod.Post, "/api/v1.0/Pedidos",
            fixture.TokenCliente, new { lineas = new[] {
                new { idInstrumento = instrumento, cantidad = 2, precioEsperado = 1000 } } }, claveAlta);
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        var existente = (await repetido.Content.ReadFromJsonAsync<PedidoRespuesta>())!;
        Assert.Equal(pedido.IdPedido, existente.IdPedido);
        Assert.Equal("Cancelado", existente.Estado);
        Assert.Equal(5, await fixture.StockAsync(instrumento));
    }
}
