using Asp.Versioning;
using MasterMusic.Api.Data;
using MasterMusic.Api.Dtos;
using MasterMusic.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Api.Controllers;

[ApiController, ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/Instrumentos")]
[Authorize(Roles = "Administrador")]
[TypeFilter(typeof(ErroresPersistenciaFilter))]
public class InstrumentosAdminController(MasterMusicContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar() => Ok((await db.Instrumentos.AsNoTracking()
        .Include(i => i.Categoria).Include(i => i.Marca).OrderBy(i => i.Nombre).ToListAsync())
        .Select(InstrumentoRespuesta.Desde));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Consultar(int id)
    {
        var i = await db.Instrumentos.AsNoTracking().Include(i => i.Categoria).Include(i => i.Marca)
            .SingleOrDefaultAsync(i => i.IdInstrumento == id);
        return i is null ? NotFound() : Ok(InstrumentoRespuesta.Desde(i));
    }

    [HttpPatch("/api/v{version:apiVersion}/Instrumentos/{id:int}/estado")]
    public async Task<IActionResult> Estado(int id, EstadoInstrumentoEntrada entrada)
    {
        var i = await db.Instrumentos.FindAsync(id);
        if (i is null) return NotFound();
        i.Activo = entrada.Activo!.Value;
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("/api/v{version:apiVersion}/Instrumentos/{id:int}/existencias")]
    public async Task<IActionResult> Existencias(int id, EntradaStock entrada)
    {
        await using var tx = await CatalogoTransaccion.IniciarAsync(db);
        await CatalogoTransaccion.BloquearAsync(db, "Instrumento", id);
        var i = await db.Instrumentos.FindAsync(id);
        if (i is null) return NotFound();
        if ((long)i.Stock + entrada.Cantidad > int.MaxValue)
            return Problem(statusCode: 409, title: "Límite de existencias",
                detail: "La cantidad supera el máximo permitido.",
                extensions: new Dictionary<string, object?> { ["codigo"] = "STOCK_FUERA_DE_RANGO" });
        i.Stock += entrada.Cantidad;
        await db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        await db.Entry(i).Reference(x => x.Categoria).LoadAsync();
        await db.Entry(i).Reference(x => x.Marca).LoadAsync();
        return Ok(InstrumentoRespuesta.Desde(i));
    }
}
