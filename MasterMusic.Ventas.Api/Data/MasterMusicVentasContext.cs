using MasterMusic.Ventas.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Ventas.Api.Data;

public class MasterMusicVentasContext : DbContext
{
    public MasterMusicVentasContext(DbContextOptions<MasterMusicVentasContext> options)
        : base(options) { }

    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<DetallePedido> DetallesPedido => Set<DetallePedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var pedido = modelBuilder.Entity<Pedido>();

        // La API genera este identificador antes de solicitar el descuento de inventario.
        pedido.Property(p => p.IdPedido).ValueGeneratedNever();
        pedido.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);
        pedido.Property(p => p.Fecha).HasConversion(
            fecha => fecha,
            fecha => DateTime.SpecifyKind(fecha, DateTimeKind.Utc));
        pedido.HasIndex(p => new { p.IdCliente, p.Fecha });

        pedido.HasMany(p => p.Detalles)
            .WithOne()
            .HasForeignKey(d => d.IdPedido)
            .OnDelete(DeleteBehavior.Restrict);

        // EF carga la lista interna. La aplicación ve los detalles como solo lectura.
        pedido.Navigation(p => p.Detalles)
            .HasField("_detalles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        pedido.ToTable("Pedido", tabla =>
        {
            tabla.HasCheckConstraint("CK_Pedido_Total",
                $"[total] BETWEEN 1 AND {Pedido.TotalMaximo}");
            tabla.HasCheckConstraint("CK_Pedido_Estado",
                "[estado] IN (N'Pendiente', N'Confirmado', N'Cancelado')");
            tabla.HasCheckConstraint("CK_Pedido_MotivoCancelacion",
                "([estado] = N'Cancelado' AND [motivo_cancelacion] IS NOT NULL AND LEN([motivo_cancelacion]) > 0) " +
                "OR ([estado] <> N'Cancelado' AND [motivo_cancelacion] IS NULL)");
        });

        var detalle = modelBuilder.Entity<DetallePedido>();
        detalle.HasIndex(d => new { d.IdPedido, d.IdInstrumento }).IsUnique();
        detalle.ToTable("DetallePedido", tabla =>
        {
            tabla.HasCheckConstraint("CK_DetallePedido_Cantidad", "[cantidad] > 0");
            tabla.HasCheckConstraint("CK_DetallePedido_PrecioUnitario", "[precio_unitario] > 0");
        });
    }
}
