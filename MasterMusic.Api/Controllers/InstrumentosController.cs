using Asp.Versioning;
using MasterMusic.Api.Entities;
using MasterMusic.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MasterMusic.Api.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiController]
    public class InstrumentosController : ControllerBase
    {
        private readonly IInstrumentoRepository _repository;

        // Inyección de dependencias del Repositorio
        public InstrumentosController(IInstrumentoRepository repository)
        {
            _repository = repository;
        }

        // GET: api/Instrumentos
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var instrumentos = await _repository.GetAllAsync();
            return Ok(instrumentos);
        }

        // GET: api/Instrumentos/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var instrumento = await _repository.GetByIdAsync(id);
            if (instrumento == null)
                return NotFound(new { message = "Instrumento no encontrado" });

            return Ok(instrumento);
        }

        // POST: api/Instrumentos
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Instrumento instrumento)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var nuevoInstrumento = await _repository.AddAsync(instrumento);
            return CreatedAtAction(nameof(GetById), new { id = nuevoInstrumento.IdInstrumento }, nuevoInstrumento);
        }

        // PUT: api/Instrumentos/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Instrumento instrumento)
        {
            if (id != instrumento.IdInstrumento)
                return BadRequest(new { message = "El ID de la URL no coincide con el ID del cuerpo" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var actualizado = await _repository.UpdateAsync(instrumento);
            if (!actualizado)
                return NotFound(new { message = "Instrumento no encontrado o no se pudo actualizar" });

            return NoContent();
        }

        // DELETE: api/Instrumentos/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var eliminado = await _repository.DeleteAsync(id);
            if (!eliminado)
                return NotFound(new { message = "Instrumento no encontrado" });

            return NoContent();
        }
    }
}

