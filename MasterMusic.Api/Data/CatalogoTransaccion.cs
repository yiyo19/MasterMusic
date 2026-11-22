using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MasterMusic.Api.Data;

// El bloqueo de la fila principal impide que aparezcan nuevas referencias entre
// la comprobación y el DELETE, incluso en las relaciones antiguas con cascada.
public static class CatalogoTransaccion
{
    public static Task<IDbContextTransaction?> IniciarAsync(MasterMusicContext db) =>
        db.Database.IsSqlServer() ? IniciarSqlAsync(db) : Task.FromResult<IDbContextTransaction?>(null);

    private static async Task<IDbContextTransaction?> IniciarSqlAsync(MasterMusicContext db) =>
        await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

    public static async Task BloquearAsync(MasterMusicContext db, string recurso, int id)
    {
        if (!db.Database.IsSqlServer()) return;
        switch (recurso)
        {
            case "Instrumento":
                await db.Instrumentos.FromSqlInterpolated(
                    $"SELECT * FROM [Instrumento] WITH (XLOCK, HOLDLOCK) WHERE [id_instrumento] = {id}")
                    .LoadAsync();
                break;
            case "Categoria":
                await db.Categorias.FromSqlInterpolated(
                    $"SELECT * FROM [Categoria] WITH (XLOCK, HOLDLOCK) WHERE [id_categoria] = {id}")
                    .LoadAsync();
                break;
            case "Marca":
                await db.Marcas.FromSqlInterpolated(
                    $"SELECT * FROM [Marca] WITH (XLOCK, HOLDLOCK) WHERE [id_marca] = {id}")
                    .LoadAsync();
                break;
            default: throw new ArgumentOutOfRangeException(nameof(recurso));
        }
    }
}

public sealed class RecursoEnUsoException() : Exception("El recurso tiene referencias que deben conservarse.");

public sealed class InstrumentoConStockException() : Exception("No se puede eliminar un instrumento con existencias.");
