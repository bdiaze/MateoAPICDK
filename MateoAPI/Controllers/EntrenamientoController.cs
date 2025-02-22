using Amazon.Lambda.Core;
using MateoAPI.Entities.Contexts;
using MateoAPI.Entities.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

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
            Stopwatch stopwatch = Stopwatch.StartNew();
            try {
                List<Entrenamiento> entrenamientos = await _context.Entrenamientos
                    .Where(e => e.IdUsuario == User.Identity!.Name && e.Inicio >= inicio && e.Termino <= termino)
                    .ToListAsync();

                LambdaLogger.Log(
                    $"[GET] - [EntrenamientoController] - [Listar] - [{stopwatch.ElapsedMilliseconds} ms] - [200] - " +
                    $"Se obtienen los entrenamientos del usuario {User.Identity!.Name} para los filtros de inicio {inicio} y termino {termino}: {entrenamientos.Count} entrenamientos.");

                return Ok(entrenamientos);
            } catch (Exception ex) {
                LambdaLogger.Log(
                    $"[GET] - [EntrenamientoController] - [Listar] - [{stopwatch.ElapsedMilliseconds} ms] - [500] - " +
                    $"Ocurrió un error al obtener los entrenamientos del usuario {User.Identity!.Name} para los filtros de inicio {inicio} y termino {termino}. " +
                    $"{ex.ToString()}");                
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
