using MasterMusic.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Api.Data
{
    public class MasterMusicContext : DbContext
    {
        public MasterMusicContext(DbContextOptions<MasterMusicContext> options) : base(options)
        {
        }

        public DbSet<Rol> Roles { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Marca> Marcas { get; set; }
        public DbSet<Instrumento> Instrumentos { get; set; }
        public DbSet<Venta> Ventas { get; set; }
        public DbSet<DetalleVenta> DetallesVentas { get; set; }
        public DbSet<Resena> Resenas { get; set; }
    }
}
