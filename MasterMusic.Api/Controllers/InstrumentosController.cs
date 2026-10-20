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

        // Inyección de dependencias del Repositorio
        public InstrumentosController(IInstrumentoRepository repository)
        {
            _repository = repository;
        }

        // GET: api/v1.0/Instrumentos
        // Acceso público: Cualquier usuario puede consultar la lista
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var instrumentos = await _repository.GetAllAsync();
            return Ok(instrumentos);
        }

        // GET: api/v1.0/Instrumentos/5
        // Acceso público: Cualquier usuario puede ver el detalle
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetById(int id)
        {
            var instrumento = await _repository.GetByIdAsync(id);
            if (instrumento == null)
                return NotFound(new { message = "Instrumento no encontrado" });

            return Ok(instrumento);
        }

        // POST: api/v1.0/Instrumentos
        // Requiere estar autenticado con ROL 'Cliente' o 'Administrador'
        [HttpPost]
        [Authorize(Roles = "Cliente,Administrador")]
        public async Task<IActionResult> Create([FromBody] Instrumento instrumento)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var nuevoInstrumento = await _repository.AddAsync(instrumento);
            return CreatedAtAction(nameof(GetById), new { id = nuevoInstrumento.IdInstrumento, version = "1.0" }, nuevoInstrumento);
        }

        // PUT: api/v1.0/Instrumentos/5
        // Exclusivo para ROL 'Administrador'
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
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

        // DELETE: api/v1.0/Instrumentos/5
        // Exclusivo para ROL 'Administrador'
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