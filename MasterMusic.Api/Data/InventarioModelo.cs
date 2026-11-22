using MasterMusic.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Api.Data;

public static class InventarioModelo
{
    public static void Configurar(ModelBuilder modelo)
    {
        modelo.Entity<Instrumento>().Property(i => i.Version).IsRowVersion().HasColumnName("version");
        modelo.Entity<Instrumento>().ToTable("Instrumento", tabla =>
            tabla.HasCheckConstraint("CK_Instrumento_Stock", "[stock] >= 0"));

        var movimiento = modelo.Entity<MovimientoInventario>();
        movimiento.ToTable("MovimientoInventario");
        movimiento.HasKey(m => m.IdOperacion);
        movimiento.Property(m => m.IdOperacion).HasColumnName("id_operacion").ValueGeneratedNever();
        movimiento.Property(m => m.IdPedido).HasColumnName("id_pedido");
        movimiento.Property(m => m.IdCliente).HasColumnName("id_cliente");
        movimiento.Property(m => m.Tipo).HasColumnName("tipo").HasMaxLength(20);
        movimiento.Property(m => m.HuellaSolicitud).HasColumnName("huella_solicitud").HasMaxLength(64);
        movimiento.Property(m => m.Fecha).HasColumnName("fecha").HasConversion(
            fecha => fecha, fecha => DateTime.SpecifyKind(fecha, DateTimeKind.Utc));
        movimiento.Property(m => m.IdOperacionOrigen).HasColumnName("id_operacion_origen");
        movimiento.HasIndex(m => m.IdPedido).IsUnique().HasFilter("[tipo] = N'Descuento'");
        movimiento.HasIndex(m => m.IdOperacionOrigen).IsUnique().HasFilter("[id_operacion_origen] IS NOT NULL");
        movimiento.HasOne<MovimientoInventario>().WithMany().HasForeignKey(m => m.IdOperacionOrigen)
            .OnDelete(DeleteBehavior.Restrict);
        movimiento.HasMany(m => m.Detalles).WithOne().HasForeignKey(d => d.IdOperacion)
            .OnDelete(DeleteBehavior.Restrict);

        var detalle = modelo.Entity<DetalleMovimientoInventario>();
        detalle.ToTable("DetalleMovimientoInventario", tabla =>
        {
            tabla.HasCheckConstraint("CK_Movimiento_Cantidad", "[cantidad] > 0");
            tabla.HasCheckConstraint("CK_Movimiento_Precio", "[precio_unitario] > 0");
        });
        detalle.HasKey(d => d.IdDetalleMovimiento);
        detalle.Property(d => d.IdDetalleMovimiento).HasColumnName("id_detalle_movimiento");
        detalle.Property(d => d.IdOperacion).HasColumnName("id_operacion");
        detalle.Property(d => d.IdInstrumento).HasColumnName("id_instrumento");
        detalle.Property(d => d.Cantidad).HasColumnName("cantidad");
        detalle.Property(d => d.NombreInstrumento).HasColumnName("nombre_instrumento").HasMaxLength(150);
        detalle.Property(d => d.PrecioUnitario).HasColumnName("precio_unitario");
        detalle.HasIndex(d => new { d.IdOperacion, d.IdInstrumento }).IsUnique();
        detalle.HasOne<Instrumento>().WithMany().HasForeignKey(d => d.IdInstrumento)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
