using Amazon.Lambda.Core;
using MateoAPI.Entities.Contexts;
using MateoAPI.Entities.Models;
using MateoAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Globalization;

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
                    .Where(e => e.IdUsuario == User.Identity!.Name && e.Inicio >= inicio && e.Inicio <= termino)
                    .ToListAsync();

                LambdaLogger.Log(
                    $"[GET] - [EntrenamientoController] - [Listar] - [{stopwatch.ElapsedMilliseconds} ms] - [200] - " +
                    $"Se obtienen correctamente los entrenamientos del usuario {User.Identity!.Name} para los filtros de inicio {inicio.ToString("O")} y termino {termino.ToString("O")}: {entrenamientos.Count} entrenamientos.");

                return Ok(entrenamientos);
            } catch (Exception ex) {
                LambdaLogger.Log(
                    $"[GET] - [EntrenamientoController] - [Listar] - [{stopwatch.ElapsedMilliseconds} ms] - [500] - " +
                    $"Ocurrió un error al obtener los entrenamientos del usuario {User.Identity!.Name} para los filtros de inicio {inicio.ToString("O")} y termino {termino.ToString("O")}. " +
                    $"{ex.ToString()}");                
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [Route("[action]")]
        [HttpPost]
        public async Task<ActionResult<Entrenamiento>> Crear(EntEntrenamiento entEntrenamiento) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try {
                Entrenamiento entrenamiento = new Entrenamiento { 
                    IdUsuario = User.Identity!.Name!,
                    Inicio = entEntrenamiento.Inicio,
                    Termino = entEntrenamiento.Termino,
                    IdTipoEjercicio = entEntrenamiento.IdTipoEjercicio,
                    Serie = entEntrenamiento.Serie,
                    Repeticiones = entEntrenamiento.Repeticiones,
                    SegundosEntrenamiento = entEntrenamiento.SegundosEntrenamiento,
                    SegundosDescanso = entEntrenamiento.SegundosDescanso
                };
                await _context.Entrenamientos.AddAsync(entrenamiento);
                await _context.SaveChangesAsync();

                LambdaLogger.Log(
                    $"[POST] - [EntrenamientoController] - [Crear] - [{stopwatch.ElapsedMilliseconds} ms] - [200] - " +
                    $"Se inserta correctamente el entrenamiento del usuario {User.Identity!.Name} - ID: {entrenamiento.Id}.");

                return Ok(entrenamiento);
            } catch (Exception ex) {
                LambdaLogger.Log(
                    $"[POST] - [EntrenamientoController] - [Crear] - [{stopwatch.ElapsedMilliseconds} ms] - [500] - " +
                    $"Ocurrió un error al insertar un entrenamiento del usuario {User.Identity!.Name} - {entEntrenamiento.ToString()}. " +
                    $"{ex.ToString()}");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
