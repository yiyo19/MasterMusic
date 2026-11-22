using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MasterMusic.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Tests;

public class AuthRegistroSqlTests(PedidosSqlFixture fixture) : IClassFixture<PedidosSqlFixture>
{
    [SqlServerFact]
    public async Task RegistroConcurrente_MismoCorreo_CreaUnaCuentaYDevuelveConflicto()
    {
        var correo = $"concurrente_{Guid.NewGuid():N}@example.com";
        var entrada = new { correo, clave = Guid.NewGuid().ToString("N"), nombreCompleto = "Registro simultáneo" };
        var primera = fixture.Catalogo.PostAsJsonAsync("/api/v1.0/Auth/registro", entrada);
        var segunda = fixture.Catalogo.PostAsJsonAsync("/api/v1.0/Auth/registro", entrada);
        var respuestas = await Task.WhenAll(primera, segunda);
        try
        {
            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
            var conflicto = Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
            var problema = await conflicto.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("CORREO_DUPLICADO", problema.GetProperty("codigo").GetString());
            await using var db = fixture.CrearCatalogo();
            Assert.Equal(1, await db.Usuarios.CountAsync(u => u.Email == correo));
        }
        finally
        {
            foreach (var respuesta in respuestas) respuesta.Dispose();
        }
    }

    [SqlServerFact]
    public async Task IndiceUnicoCorreo_RechazaInsercionDuplicadaFueraDelControlador()
    {
        await using var db = fixture.CrearCatalogo();
        var correo = $"indice_{Guid.NewGuid():N}@example.com";
        var rol = await db.Roles.Where(r => r.Nombre == "Cliente").Select(r => r.IdRol).SingleAsync();
        var hash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"));
        db.Usuarios.Add(new Usuario { Nombre = "Primera cuenta", Email = correo, Password = hash, IdRol = rol, Activo = true });
        await db.SaveChangesAsync();
        db.Usuarios.Add(new Usuario { Nombre = "Duplicado", Email = correo, Password = hash, IdRol = rol, Activo = true });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        var sql = Assert.IsType<Microsoft.Data.SqlClient.SqlException>(error.InnerException);
        Assert.Contains(sql.Number, new[] { 2601, 2627 });
    }
}
