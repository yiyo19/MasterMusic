using MasterMusic.Api.Entities;

namespace MasterMusic.Api.Interfaces
{
    public interface IInstrumentoRepository
    {
        Task<IEnumerable<Instrumento>> GetAllAsync();
        Task<Instrumento?> GetByIdAsync(int id);
        Task<Instrumento> AddAsync(Instrumento instrumento);
        Task<bool> UpdateAsync(Instrumento instrumento);
        Task<bool> DeleteAsync(int id);
        Task<bool> ExisteAsync(int id);
        Task<bool> CategoriaExisteAsync(int idCategoria);
        Task<bool> MarcaExisteAsync(int idMarca);
    }
}