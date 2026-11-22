using MasterMusic.Api.Data;
using MasterMusic.Api.Entities;
using MasterMusic.Api.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Api.Repositories
{
    public class InstrumentoRepository : IInstrumentoRepository
    {
        private readonly MasterMusicContext _context;

        public InstrumentoRepository(MasterMusicContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Instrumento>> GetAllAsync()
        {
            return await _context.Instrumentos
                .Include(i => i.Categoria)
                .Include(i => i.Marca)
                .ToListAsync();
        }

        public async Task<Instrumento?> GetByIdAsync(int id)
        {
            return await _context.Instrumentos
                .Include(i => i.Categoria)
                .Include(i => i.Marca)
                .FirstOrDefaultAsync(i => i.IdInstrumento == id);
        }

        public async Task<Instrumento> AddAsync(Instrumento instrumento)
        {
            await _context.Instrumentos.AddAsync(instrumento);
            await _context.SaveChangesAsync();
            return instrumento;
        }

        public async Task<bool> UpdateAsync(Instrumento instrumento)
        {
            var actual = await _context.Instrumentos.FindAsync(instrumento.IdInstrumento);
            if (actual == null) return false;
            var version = actual.Version;
            var stock = actual.Stock;
            var activo = actual.Activo;
            _context.Entry(actual).CurrentValues.SetValues(instrumento);
            actual.Version = version;
            // El PUT de datos no sobrescribe el inventario ni el estado.
            actual.Stock = stock;
            actual.Activo = activo;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            await using var tx = await CatalogoTransaccion.IniciarAsync(_context);
            await CatalogoTransaccion.BloquearAsync(_context, "Instrumento", id);
            var instrumento = await _context.Instrumentos.FindAsync(id);
            if (instrumento == null) return false;
            if (instrumento.Stock != 0) throw new InstrumentoConStockException();

            if (await _context.DetallesVentas.AnyAsync(d => d.IdInstrumento == id)
                || await _context.Resenas.AnyAsync(r => r.IdInstrumento == id)
                || await _context.Set<DetalleMovimientoInventario>().AnyAsync(d => d.IdInstrumento == id))
                throw new RecursoEnUsoException();

            _context.Instrumentos.Remove(instrumento);
            await _context.SaveChangesAsync();
            if (tx is not null) await tx.CommitAsync();
            return true;
        }

        public async Task<bool> ExisteAsync(int id)
        {
            return await _context.Instrumentos.AnyAsync(i => i.IdInstrumento == id);
        }

        public async Task<bool> CategoriaExisteAsync(int idCategoria)
        {
            return await _context.Categorias.AnyAsync(c => c.IdCategoria == idCategoria);
        }

        public async Task<bool> MarcaExisteAsync(int idMarca)
        {
            return await _context.Marcas.AnyAsync(m => m.IdMarca == idMarca);
        }
    }
}
