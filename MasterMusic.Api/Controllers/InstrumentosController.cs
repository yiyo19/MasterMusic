using Asp.Versioning;
using MasterMusic.Api.Entities;
using MasterMusic.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MasterMusic.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class InstrumentosController : ControllerBase
    {
        private readonly IInstrumentoRepository _repository;

        
        public InstrumentosController(IInstrumentoRepository repository)
        {
            _repository = repository;
        }

        
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var instrumentos = await _repository.GetAllAsync();
            return Ok(instrumentos);
        }

        
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var instrumento = await _repository.GetByIdAsync(id);
            if (instrumento == null)
                return NotFound(new { message = "Instrumento no encontrado" });

            return Ok(instrumento);
        }

       
        [HttpPost]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Create([FromBody] Instrumento instrumento)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!await _repository.CategoriaExisteAsync(instrumento.IdCategoria))
                return BadRequest(new { message = "La categoría indicada no existe." });

            if (!await _repository.MarcaExisteAsync(instrumento.IdMarca))
                return BadRequest(new { message = "La marca indicada no existe." });

            var nuevoInstrumento = await _repository.AddAsync(instrumento);
            return CreatedAtAction(nameof(GetById), new { id = nuevoInstrumento.IdInstrumento, version = "1.0" }, nuevoInstrumento);
        }

        
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Update(int id, [FromBody] Instrumento instrumento)
        {
            if (id != instrumento.IdInstrumento)
                return BadRequest(new { message = "El ID de la URL no coincide con el ID del cuerpo" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!await _repository.ExisteAsync(id))
                return NotFound(new { message = "Instrumento no encontrado" });

            if (!await _repository.CategoriaExisteAsync(instrumento.IdCategoria))
                return BadRequest(new { message = "La categoría indicada no existe." });

            if (!await _repository.MarcaExisteAsync(instrumento.IdMarca))
                return BadRequest(new { message = "La marca indicada no existe." });

            var actualizado = await _repository.UpdateAsync(instrumento);
            if (!actualizado)
                return NotFound(new { message = "Instrumento no encontrado o no se pudo actualizar" });

            return NoContent();
        }

        
        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Delete(int id)
        {
            var eliminado = await _repository.DeleteAsync(id);
            if (!eliminado)
                return NotFound(new { message = "Instrumento no encontrado" });

            return NoContent();
        }
    }
}