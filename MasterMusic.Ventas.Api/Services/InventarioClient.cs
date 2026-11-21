using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MasterMusic.Ventas.Api.DTOs;

namespace MasterMusic.Ventas.Api.Services;

public sealed record SolicitudDescuento(
    Guid IdOperacion, Guid IdPedido, int IdCliente, IReadOnlyList<LineaPedido> Lineas);

public sealed record SolicitudDevolucion(
    Guid IdOperacion, Guid IdOperacionOrigen, Guid IdPedido, int IdCliente);

public sealed record MovimientoInventarioRespuesta(
    Guid IdOperacion, Guid IdPedido, int IdCliente, string Tipo,
    Guid? IdOperacionOrigen, DateTime Fecha, IReadOnlyList<DetallePedidoRespuesta> Detalles);

public sealed record ResultadoInventario(
    MovimientoInventarioRespuesta? Movimiento = null,
    ProblemaPedido? Problema = null, bool NoEncontrado = false);

public sealed class InventarioClient(
    HttpClient cliente, IConfiguration configuracion, ILogger<InventarioClient> logger)
{
    public Task<ResultadoInventario> ConsultarAsync(Guid id, string autorizacion, CancellationToken cancelacion) =>
        EnviarAsync(HttpMethod.Get, $"api/v1.0/interno/Movimientos/{id}", null, autorizacion, cancelacion);

    public Task<ResultadoInventario> DescontarAsync(
        SolicitudDescuento solicitud, string autorizacion, CancellationToken cancelacion) =>
        EnviarAsync(HttpMethod.Post, "api/v1.0/interno/Movimientos/descuentos",
            solicitud, autorizacion, cancelacion);

    public Task<ResultadoInventario> DevolverAsync(
        SolicitudDevolucion solicitud, string autorizacion, CancellationToken cancelacion) =>
        EnviarAsync(HttpMethod.Post, "api/v1.0/interno/Movimientos/devoluciones",
            solicitud, autorizacion, cancelacion);

    private async Task<ResultadoInventario> EnviarAsync(
        HttpMethod metodo, string ruta, object? solicitud,
        string autorizacion, CancellationToken cancelacion)
    {
        var claveServicio = configuracion["Inventario:ServiceKey"];
        if (string.IsNullOrWhiteSpace(claveServicio))
        {
            logger.LogError("Falta configurar Inventario__ServiceKey.");
            return new ResultadoInventario();
        }

        using var mensaje = new HttpRequestMessage(metodo, ruta);
        mensaje.Headers.Authorization = AuthenticationHeaderValue.Parse(autorizacion);
        mensaje.Headers.Add("X-Service-Key", claveServicio);
        if (solicitud is not null)
            mensaje.Content = JsonContent.Create(solicitud);

        try
        {
            // SendAsync carga también el cuerpo dentro del timeout del cliente.
            using var respuesta = await cliente.SendAsync(mensaje, cancelacion);
            if (respuesta.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
            {
                var movimiento = await respuesta.Content.ReadFromJsonAsync<MovimientoInventarioRespuesta>(cancelacion);
                return new ResultadoInventario(Movimiento: movimiento);
            }
            if (metodo == HttpMethod.Get)
                return new ResultadoInventario(NoEncontrado: respuesta.StatusCode == HttpStatusCode.NotFound);

            if (respuesta.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict)
            {
                var problema = await respuesta.Content.ReadFromJsonAsync<ProblemaPedido>(cancelacion);
                if (problema is not null && problema.Status == (int)respuesta.StatusCode
                    && problema.Codigo is ("VALIDACION" or "TOTAL_FUERA_DE_RANGO" or "NO_ENCONTRADO"
                        or "PRECIO_CAMBIADO" or "STOCK_INSUFICIENTE" or "INSTRUMENTO_INACTIVO"
                        or "STOCK_FUERA_DE_RANGO" or "MOVIMIENTO_ORIGEN_INVALIDO"
                        or "DEVOLUCION_YA_REGISTRADA" or "IDEMPOTENCIA_CONFLICTO"))
                    return new ResultadoInventario(Problema: problema);
            }
        }
        catch (OperationCanceledException) when (!cancelacion.IsCancellationRequested)
        {
            logger.LogWarning("Inventario no respondió dentro del plazo. La operación puede recuperarse.");
        }
        catch (Exception error) when (error is HttpRequestException or JsonException)
        {
            logger.LogWarning("No fue posible obtener una respuesta válida de inventario.");
        }
        return new ResultadoInventario();
    }
}
