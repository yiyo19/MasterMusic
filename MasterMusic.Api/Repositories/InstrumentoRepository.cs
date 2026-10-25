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
            _context.Instrumentos.Update(instrumento);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var instrumento = await _context.Instrumentos.FindAsync(id);
            if (instrumento == null) return false;

            _context.Instrumentos.Remove(instrumento);
            return await _context.SaveChangesAsync() > 0;
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