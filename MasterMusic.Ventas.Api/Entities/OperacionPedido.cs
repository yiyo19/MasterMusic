namespace MasterMusic.Ventas.Api.Entities;

public class OperacionPedido
{
    public Guid IdOperacion { get; set; }
    public Guid ClaveCliente { get; set; }
    public int IdUsuario { get; set; }
    public Guid? IdPedido { get; set; }
    public Pedido? Pedido { get; set; }
    public string Tipo { get; set; } = "Crear";
    public string EstadoOperacion { get; set; } = "EnProceso";
    public string HuellaSolicitud { get; set; } = string.Empty;
    public string SolicitudJson { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;
    public string? CodigoResultado { get; set; }
    public string? ProblemaJson { get; set; }
    public byte[] Version { get; set; } = [];
}
