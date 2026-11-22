using MasterMusic.Ventas.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Ventas.Api.Data;

public static class OperacionModelo
{
    public static void Configurar(ModelBuilder modelo)
    {
        var operacion = modelo.Entity<OperacionPedido>();
        operacion.ToTable("OperacionPedido");
        operacion.HasKey(o => o.IdOperacion);
        operacion.Property(o => o.IdOperacion).HasColumnName("id_operacion").ValueGeneratedNever();
        operacion.Property(o => o.ClaveCliente).HasColumnName("clave_cliente");
        operacion.Property(o => o.IdUsuario).HasColumnName("id_usuario");
        operacion.Property(o => o.IdPedido).HasColumnName("id_pedido");
        operacion.Property(o => o.Tipo).HasColumnName("tipo").HasMaxLength(20);
        operacion.Property(o => o.EstadoOperacion).HasColumnName("estado_operacion").HasMaxLength(20);
        operacion.Property(o => o.HuellaSolicitud).HasColumnName("huella_solicitud").HasMaxLength(64);
        operacion.Property(o => o.SolicitudJson).HasColumnName("solicitud_json");
        operacion.Property(o => o.FechaCreacion).HasColumnName("fecha_creacion").HasConversion(
            fecha => fecha, fecha => DateTime.SpecifyKind(fecha, DateTimeKind.Utc));
        operacion.Property(o => o.FechaActualizacion).HasColumnName("fecha_actualizacion").HasConversion(
            fecha => fecha, fecha => DateTime.SpecifyKind(fecha, DateTimeKind.Utc));
        operacion.Property(o => o.CodigoResultado).HasColumnName("codigo_resultado").HasMaxLength(80);
        operacion.Property(o => o.ProblemaJson).HasColumnName("problema_json");
        operacion.Property(o => o.Version).HasColumnName("version").IsRowVersion();
        operacion.HasIndex(o => new { o.IdUsuario, o.Tipo, o.ClaveCliente }).IsUnique();
        operacion.HasOne(o => o.Pedido).WithMany().HasForeignKey(o => o.IdPedido)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
