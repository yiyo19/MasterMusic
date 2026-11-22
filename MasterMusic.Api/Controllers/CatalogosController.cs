using Asp.Versioning;
using MasterMusic.Api.Data;
using MasterMusic.Api.Dtos;
using MasterMusic.Api.Entities;
using MasterMusic.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MasterMusic.Api.Controllers;

[ApiController, ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/Categorias")]
[Authorize(Roles = "Administrador")]
[TypeFilter(typeof(ErroresPersistenciaFilter))]
public class CategoriasController(MasterMusicContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await db.Categorias.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Consultar(int id)
    {
        var c = await db.Categorias.AsNoTracking().SingleOrDefaultAsync(c => c.IdCategoria == id);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(CategoriaEntrada entrada)
    {
        var c = new Categoria { Nombre = entrada.Nombre.Trim(), Descripcion = entrada.Descripcion?.Trim() };
        db.Categorias.Add(c);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Consultar), new { id = c.IdCategoria, version = "1.0" }, c);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, CategoriaEntrada entrada)
    {
        var c = await db.Categorias.FindAsync(id);
        if (c is null) return NotFound();
        c.Nombre = entrada.Nombre.Trim();
        c.Descripcion = entrada.Descripcion?.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await using var tx = await CatalogoTransaccion.IniciarAsync(db);
        await CatalogoTransaccion.BloquearAsync(db, "Categoria", id);
        var c = await db.Categorias.FindAsync(id);
        if (c is null) return NotFound();
        if (await db.Instrumentos.AnyAsync(i => i.IdCategoria == id)) throw new RecursoEnUsoException();
        db.Categorias.Remove(c);
        await db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        return NoContent();
    }
}

[ApiController, ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/Marcas")]
[Authorize(Roles = "Administrador")]
[TypeFilter(typeof(ErroresPersistenciaFilter))]
public class MarcasController(MasterMusicContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await db.Marcas.AsNoTracking().OrderBy(m => m.Nombre).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Consultar(int id)
    {
        var m = await db.Marcas.AsNoTracking().SingleOrDefaultAsync(m => m.IdMarca == id);
        return m is null ? NotFound() : Ok(m);
    }

    [HttpPost]
    public async Task<IActionResult> Crear(MarcaEntrada entrada)
    {
        var m = new Marca { Nombre = entrada.Nombre.Trim(), Pais = entrada.Pais?.Trim() };
        db.Marcas.Add(m);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Consultar), new { id = m.IdMarca, version = "1.0" }, m);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, MarcaEntrada entrada)
    {
        var m = await db.Marcas.FindAsync(id);
        if (m is null) return NotFound();
        m.Nombre = entrada.Nombre.Trim();
        m.Pais = entrada.Pais?.Trim();
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id)
    {
        await using var tx = await CatalogoTransaccion.IniciarAsync(db);
        await CatalogoTransaccion.BloquearAsync(db, "Marca", id);
        var m = await db.Marcas.FindAsync(id);
        if (m is null) return NotFound();
        if (await db.Instrumentos.AnyAsync(i => i.IdMarca == id)) throw new RecursoEnUsoException();
        db.Marcas.Remove(m);
        await db.SaveChangesAsync();
        if (tx is not null) await tx.CommitAsync();
        return NoContent();
    }
}
