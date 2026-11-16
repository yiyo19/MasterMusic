using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MasterMusic.Ventas.Api.Entities;

[Table("Pedido")]
public class Pedido
{
    // Límite de total definido en el contrato OpenAPI para importes enteros en CLP.
    public const long TotalMaximo = 9_007_199_254_740_991L;
    private readonly List<DetallePedido> _detalles = new();

    [Key]
    [Column("id_pedido")]
    public Guid IdPedido { get; private set; }

    [Column("id_cliente")]
    public int IdCliente { get; private set; }

    [Column("fecha")]
    public DateTime Fecha { get; private set; }

    [Column("estado")]
    public EstadoPedido Estado { get; private set; } = EstadoPedido.Pendiente;

    [Column("total")]
    public long Total { get; private set; }

    [MaxLength(500)]
    [Column("motivo_cancelacion")]
    public string? MotivoCancelacion { get; private set; }

    [Timestamp]
    [Column("version")]
    public byte[] Version { get; private set; } = Array.Empty<byte>();

    public IReadOnlyCollection<DetallePedido> Detalles => _detalles.AsReadOnly();

    private Pedido() { } // EF Core lo usa para reconstruir un pedido guardado.

    // El servicio entrega el ID previsto y las líneas confirmadas por inventario.
    public Pedido(Guid idPedido, int idCliente, IEnumerable<DetallePedido> detalles)
    {
        if (idPedido == Guid.Empty)
            throw new ArgumentException("El identificador del pedido es obligatorio.", nameof(idPedido));

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(idCliente);
        ArgumentNullException.ThrowIfNull(detalles);
        var instrumentos = new HashSet<int>();
        foreach (var detalle in detalles)
        {
            if (detalle is null || detalle.IdPedido != idPedido)
                throw new ArgumentException("Cada detalle debe pertenecer a este pedido.", nameof(detalles));
            if (!instrumentos.Add(detalle.IdInstrumento))
                throw new ArgumentException("Hay un instrumento repetido en el pedido.", nameof(detalles));
            _detalles.Add(detalle);
        }
        if (_detalles.Count == 0)
            throw new ArgumentException("El pedido requiere al menos un instrumento.", nameof(detalles));

        IdPedido = idPedido;
        IdCliente = idCliente;
        Fecha = DateTime.UtcNow;
        Total = CalcularTotal();
    }

    public long CalcularTotal()
    {
        long total = 0;
        foreach (var detalle in _detalles)
        {
            var subtotal = detalle.CalcularSubtotal();
            if (subtotal > TotalMaximo - total)
                throw new ArgumentOutOfRangeException(nameof(Detalles), "El total excede el límite admitido.");
            total += subtotal;
        }
        return total;
    }

    public void Confirmar()
    {
        VerificarPendiente();
        Estado = EstadoPedido.Confirmado;
    }

    // El servicio llama a este método después de comprobar la devolución de stock.
    public void Cancelar(string motivo)
    {
        VerificarPendiente();
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (motivo.Length > 500)
            throw new ArgumentException("El motivo permite hasta 500 caracteres.", nameof(motivo));

        MotivoCancelacion = motivo.Trim();
        Estado = EstadoPedido.Cancelado;
    }

    private void VerificarPendiente()
    {
        if (Estado != EstadoPedido.Pendiente)
            throw new InvalidOperationException("Solo se puede modificar un pedido pendiente.");
    }
}
