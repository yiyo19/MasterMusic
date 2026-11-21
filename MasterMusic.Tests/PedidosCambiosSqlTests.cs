using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MasterMusic.Ventas.Api.DTOs;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Tests;

public partial class PedidosCambiosSqlTests(PedidosSqlFixture fixture) : IClassFixture<PedidosSqlFixture>
{
    private const string Admin = "/api/v1.0/admin/Pedidos";

    private async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, HttpMethod metodo,
        string ruta, string token, object? cuerpo = null, Guid? clave = null, bool interna = false)
    {
        using var mensaje = new HttpRequestMessage(metodo, ruta);
        mensaje.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null) mensaje.Content = JsonContent.Create(cuerpo);
        if (clave.HasValue) mensaje.Headers.Add("Idempotency-Key", clave.Value.ToString());
        if (interna) mensaje.Headers.Add("X-Service-Key", PedidosSqlFixture.ClaveServicio);
        return await cliente.SendAsync(mensaje);
    }

    private async Task<(PedidoRespuesta Pedido, int Instrumento, Guid Clave)> CrearAsync(
        int stock = 5, int cantidad = 2, string? cliente = null)
    {
        var id = await fixture.CrearInstrumentoAsync(stock);
        var clave = Guid.NewGuid();
        using var alta = await EnviarAsync(fixture.Ventas, HttpMethod.Post, "/api/v1.0/Pedidos",
            cliente ?? fixture.TokenCliente,
            new { lineas = new[] { new { idInstrumento = id, cantidad, precioEsperado = 1000 } } }, clave);
        Assert.Equal(HttpStatusCode.Created, alta.StatusCode);
        return ((await alta.Content.ReadFromJsonAsync<PedidoRespuesta>())!, id, clave);
    }

    private Task<HttpResponseMessage> CambiarAsync(Guid pedido, string accion, Guid? clave,
        string? motivo = "Pedido duplicado", string? token = null) =>
        EnviarAsync(fixture.Ventas, HttpMethod.Post, $"{Admin}/{pedido}/{accion}",
            token ?? fixture.TokenAdministrador, accion == "cancelacion" ? new { motivo } : null, clave);

    private async Task<HttpResponseMessage> CambiarHastaResultadoAsync(
        Guid pedido, string accion, Guid clave, string? token = null)
    {
        for (var intento = 0; ; intento++)
        {
            var respuesta = await CambiarAsync(pedido, accion, clave, token: token);
            if (intento == 4 || respuesta.StatusCode is not (HttpStatusCode.Accepted or HttpStatusCode.ServiceUnavailable))
                return respuesta;
            respuesta.Dispose();
            await Task.Delay(250);
        }
    }

    private async Task<OperacionRespuesta> OperacionAsync(Guid clave, string tipo)
    {
        using var respuesta = await EnviarAsync(fixture.Ventas, HttpMethod.Get,
            $"/api/v1.0/Operaciones?claveCliente={clave}&tipo={tipo}", fixture.TokenAdministrador);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<OperacionRespuesta>())!;
    }

    private async Task<PedidoRespuesta> PedidoAsync(Guid pedido)
    {
        using var respuesta = await EnviarAsync(fixture.Ventas, HttpMethod.Get,
            $"{Admin}/{pedido}", fixture.TokenAdministrador);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<PedidoRespuesta>())!;
    }

    private static async Task<string> CodigoAsync(HttpResponseMessage respuesta)
    {
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("codigo").GetString()!;
    }

    [SqlServerFact]
    public async Task Confirmar_NoDescuentaOtraVezNiDependeDeInventario()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        var llamadas = fixture.Fallos.Solicitudes;
        fixture.Fallos.NoDisponible = true;
        try
        {
            var clave = Guid.NewGuid();
            using var confirmacion = await CambiarAsync(pedido.IdPedido, "confirmacion", clave);
            Assert.Equal(HttpStatusCode.OK, confirmacion.StatusCode);
            Assert.Equal("Confirmado", (await confirmacion.Content.ReadFromJsonAsync<PedidoRespuesta>())!.Estado);
            using var repetido = await CambiarAsync(pedido.IdPedido, "confirmacion", clave);
            Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
            var operacion = await OperacionAsync(clave, "Confirmar");
            using var recuperado = await EnviarAsync(fixture.Ventas, HttpMethod.Post,
                $"/api/v1.0/Operaciones/{operacion.IdOperacion}/recuperacion", fixture.TokenAdministrador);
            Assert.Equal(HttpStatusCode.OK, recuperado.StatusCode);
            Assert.Equal("Completada", (await recuperado.Content.ReadFromJsonAsync<OperacionRespuesta>())!.EstadoOperacion);
            Assert.Equal(llamadas, fixture.Fallos.Solicitudes);
            Assert.Equal(3, await fixture.StockAsync(instrumento));
        }
        finally { fixture.Fallos.NoDisponible = false; }
    }

    [SqlServerFact]
    public async Task Cancelar_YReintentar_DevuelveUnaVezYGuardaMotivo()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        var clave = Guid.NewGuid();
        using var cancelacion = await CambiarAsync(pedido.IdPedido, "cancelacion", clave, "  Pedido duplicado  ");
        Assert.Equal(HttpStatusCode.OK, cancelacion.StatusCode);
        var cancelado = await cancelacion.Content.ReadFromJsonAsync<PedidoRespuesta>();
        Assert.Equal("Cancelado", cancelado!.Estado);
        Assert.Equal("Pedido duplicado", cancelado.MotivoCancelacion);
        Assert.Equal(5, await fixture.StockAsync(instrumento));
        using var repetido = await CambiarAsync(pedido.IdPedido, "cancelacion", clave);
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        await using var db = fixture.CrearCatalogo();
        Assert.Equal(1, await db.MovimientosInventario.CountAsync(m => m.IdPedido == pedido.IdPedido && m.Tipo == "Devolucion"));
        Assert.Equal(5, await fixture.StockAsync(instrumento));
    }

    [SqlServerFact]
    public async Task Finalizados_RechazanOtraTransicionConClaveNueva()
    {
        foreach (var accionInicial in new[] { "confirmacion", "cancelacion" })
        {
            var (pedido, instrumento, _) = await CrearAsync();
            using var inicial = await CambiarAsync(pedido.IdPedido, accionInicial, Guid.NewGuid());
            Assert.Equal(HttpStatusCode.OK, inicial.StatusCode);
            foreach (var accion in new[] { "confirmacion", "cancelacion" })
            {
                using var cambio = await CambiarAsync(pedido.IdPedido, accion, Guid.NewGuid());
                Assert.Equal(HttpStatusCode.Conflict, cambio.StatusCode);
                Assert.Equal("ESTADO_PEDIDO_INVALIDO", await CodigoAsync(cambio));
            }
            Assert.Equal(accionInicial == "confirmacion" ? 3 : 5, await fixture.StockAsync(instrumento));
        }
    }

    [SqlServerFact]
    public async Task MismaClaveDeConfirmacion_EnOtroPedido_Devuelve409()
    {
        var (primero, _, _) = await CrearAsync();
        var (segundo, instrumento, _) = await CrearAsync();
        var clave = Guid.NewGuid();
        using var confirmacion = await CambiarAsync(primero.IdPedido, "confirmacion", clave);
        Assert.Equal(HttpStatusCode.OK, confirmacion.StatusCode);
        using var conflicto = await CambiarAsync(segundo.IdPedido, "confirmacion", clave);
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        Assert.Equal("IDEMPOTENCIA_CONFLICTO", await CodigoAsync(conflicto));
        Assert.Equal("Pendiente", (await PedidoAsync(segundo.IdPedido)).Estado);
        Assert.Equal(3, await fixture.StockAsync(instrumento));
    }

    [SqlServerFact]
    public async Task MismaClaveDeCancelacion_ConOtroMotivo_Devuelve409()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        var clave = Guid.NewGuid();
        using var cancelacion = await CambiarAsync(pedido.IdPedido, "cancelacion", clave);
        Assert.Equal(HttpStatusCode.OK, cancelacion.StatusCode);
        using var conflicto = await CambiarAsync(pedido.IdPedido, "cancelacion", clave, "Motivo diferente");
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        Assert.Equal("IDEMPOTENCIA_CONFLICTO", await CodigoAsync(conflicto));
        Assert.Equal("Pedido duplicado", (await PedidoAsync(pedido.IdPedido)).MotivoCancelacion);
        Assert.Equal(5, await fixture.StockAsync(instrumento));
    }

    [SqlServerFact]
    public async Task Administrador_ConsultaPedidosDeAmbosClientes()
    {
        var (primero, _, _) = await CrearAsync();
        var (segundo, _, _) = await CrearAsync(cliente: fixture.TokenOtroCliente);
        using var listado = await EnviarAsync(fixture.Ventas, HttpMethod.Get, Admin, fixture.TokenAdministrador);
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var pedidos = await listado.Content.ReadFromJsonAsync<List<PedidoRespuesta>>();
        Assert.Contains(pedidos!, p => p.IdPedido == primero.IdPedido);
        Assert.Contains(pedidos!, p => p.IdPedido == segundo.IdPedido);
        Assert.Equal(segundo.IdCliente, (await PedidoAsync(segundo.IdPedido)).IdCliente);
    }

    [SqlServerFact]
    public async Task RutasAdministrativas_ExigenSesionYRolAdministrador()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        foreach (var (token, estado) in new[]
        {
            ("", HttpStatusCode.Unauthorized), (fixture.TokenCliente, HttpStatusCode.Forbidden)
        })
        {
            foreach (var ruta in new[] { Admin, $"{Admin}/{pedido.IdPedido}" })
            {
                using var consulta = await EnviarAsync(fixture.Ventas, HttpMethod.Get, ruta, token);
                Assert.Equal(estado, consulta.StatusCode);
            }
            foreach (var accion in new[] { "confirmacion", "cancelacion" })
            {
                using var cambio = await CambiarAsync(pedido.IdPedido, accion, Guid.NewGuid(), token: token);
                Assert.Equal(estado, cambio.StatusCode);
            }
        }
        Assert.Equal("Pendiente", (await PedidoAsync(pedido.IdPedido)).Estado);
        Assert.Equal(3, await fixture.StockAsync(instrumento));
    }

    [SqlServerFact]
    public async Task ClaveYMotivoInvalidos_NoModificanElPedido()
    {
        var (pedido, instrumento, _) = await CrearAsync();
        foreach (var motivo in new string?[] { null, "", "   ", new string('x', 501) })
        {
            using var respuesta = await CambiarAsync(pedido.IdPedido, "cancelacion", Guid.NewGuid(), motivo);
            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }
        foreach (var clave in new Guid?[] { null, Guid.Empty })
        {
            using var respuesta = await CambiarAsync(pedido.IdPedido, "confirmacion", clave);
            Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        }
        using var ajeno = await EnviarAsync(fixture.Ventas, HttpMethod.Post,
            $"{Admin}/{pedido.IdPedido}/cancelacion", fixture.TokenAdministrador,
            new { motivo = "Prueba", idCliente = 999 }, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, ajeno.StatusCode);
        Assert.Equal("Pendiente", (await PedidoAsync(pedido.IdPedido)).Estado);
        Assert.Equal(3, await fixture.StockAsync(instrumento));
    }

    [SqlServerFact]
    public async Task PedidoInexistente_Devuelve404()
    {
        var id = Guid.NewGuid();
        foreach (var accion in new[] { "confirmacion", "cancelacion" })
        {
            using var respuesta = await CambiarAsync(id, accion, Guid.NewGuid());
            Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        }
        using var consulta = await EnviarAsync(fixture.Ventas, HttpMethod.Get, $"{Admin}/{id}", fixture.TokenAdministrador);
        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);
    }
}
