using MasterMusic.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Tests;

public class ModeloCatalogoTests
{
    [Fact]
    public void ModeloCatalogo_CoincideConSnapshotDeMigraciones()
    {
        // Esta comprobación compara metadatos; no abre una conexión SQL.
        using var db = new MasterMusicContext(new DbContextOptionsBuilder<MasterMusicContext>()
            .UseSqlServer("Server=localhost;Database=MasterMusicModelo;Integrated Security=True;TrustServerCertificate=True")
            .Options);
        Assert.False(db.Database.HasPendingModelChanges(),
            "El modelo de EF y el snapshot deben coincidir antes de aplicar las migraciones.");
    }
}
