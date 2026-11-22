namespace MasterMusic.Api.Entities;

public class MovimientoInventario
{
    public Guid IdOperacion { get; set; }
    public Guid IdPedido { get; set; }
    public int IdCliente { get; set; }
    public string Tipo { get; set; } = "Descuento";
    public string HuellaSolicitud { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public Guid? IdOperacionOrigen { get; set; }
    public List<DetalleMovimientoInventario> Detalles { get; set; } = [];
}

public class DetalleMovimientoInventario
{
    public int IdDetalleMovimiento { get; set; }
    public Guid IdOperacion { get; set; }
    public int IdInstrumento { get; set; }
    public int Cantidad { get; set; }
    public string NombreInstrumento { get; set; } = string.Empty;
    public int PrecioUnitario { get; set; }
}
