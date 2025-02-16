using MateoAPI.Data;
using MateoAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MateoAPI.Controllers {
    [Authorize]
    [Route("[controller]")]
    [ApiController]
    public class EntrenamientoController : Controller {
        private readonly MateoDbContext _context;

        public EntrenamientoController(MateoDbContext context) {
            _context = context;
        }

        [Route("[action]")]
        [HttpGet]
        public async Task<IActionResult> Listar(DateTime inicio, DateTime termino) {
            List<Entrenamiento> entrenamientos = await _context.Entrenamientos
                .Where(e => e.IdUsuario == User.Identity!.Name && e.Inicio >= inicio && e.Termino <= termino)
                .ToListAsync();
            return Ok(entrenamientos);
        }
    }
}
