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
    public class EntrenamientoController(MateoDbContext context) : Controller {
        private readonly MateoDbContext _context = context;

        [Route("[action]")]
        [HttpGet]
        public async Task<ActionResult<SalEntrenamiento>> Listar(DateTime desde, DateTime hasta, int numPagina = 1, int cantElemPagina = 25) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try {
                int cantTotalElementos = _context.Entrenamientos.Where(e => e.IdUsuario == User.Identity!.Name && e.Inicio >= desde && e.Inicio <= hasta).Count();
                int cantTotalPaginas = Convert.ToInt32(Math.Ceiling(Decimal.Divide(cantTotalElementos, cantElemPagina)));
                List<Entrenamiento> entrenamientos = await _context.Entrenamientos
                    .AsNoTracking()
                    .Where(e => e.IdUsuario == User.Identity!.Name && e.Inicio >= desde && e.Inicio <= hasta)
                    .OrderBy(e => e.IdUsuario)
                    .OrderBy(e => e.Inicio)
                    .Skip((numPagina - 1) * cantElemPagina)
                    .Take(cantElemPagina)
                    .ToListAsync();

                LambdaLogger.Log(
                    $"[GET] - [EntrenamientoController] - [Listar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
                    $"Se obtienen correctamente los entrenamientos del usuario {User.Identity!.Name} para los filtros desde {desde:O}, hasta {hasta:O}, numPagina {numPagina} y cantElemPagina {cantElemPagina}: " +
                    $"{entrenamientos.Count} de {cantTotalElementos} entrenamientos.");

                return Ok(new SalEntrenamiento { 
                    Desde = desde,
                    Hasta = hasta,
                    Pagina = numPagina,
                    TotalPaginas = cantTotalPaginas,
                    CantidadElementosPorPagina = cantElemPagina,
                    CantidadTotalEntrenamientos = cantTotalElementos,
                    Entrenamientos = entrenamientos,
                });
            } catch (Exception ex) {
                LambdaLogger.Log(
                    $"[GET] - [EntrenamientoController] - [Listar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
                    $"Ocurrió un error al obtener los entrenamientos del usuario {User.Identity!.Name} para los filtros desde {desde:O}, hasta {hasta:O}, numPagina {numPagina} y cantElemPagina {cantElemPagina}. " +
                    $"{ex}");                
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [Route("[action]")]
        [HttpPost]
        public async Task<ActionResult<Entrenamiento>> Crear(EntEntrenamiento entEntrenamiento) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try {
                Entrenamiento entrenamiento = new() { 
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
                    $"[POST] - [EntrenamientoController] - [Crear] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
                    $"Se inserta correctamente el entrenamiento del usuario {User.Identity!.Name} - ID: {entrenamiento.Id}.");

                return Ok(entrenamiento);
            } catch (Exception ex) {
                LambdaLogger.Log(
                    $"[POST] - [EntrenamientoController] - [Crear] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
                    $"Ocurrió un error al insertar un entrenamiento del usuario {User.Identity!.Name} - {entEntrenamiento}. " +
                    $"{ex}");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [Route("[action]")]
        [HttpDelete]
        public async Task<ActionResult> Eliminar(long id) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try {
                // Se valida si entrenamiento pertenece al usuario conectado...
                Entrenamiento? entrenamiento = await _context.Entrenamientos.Where(e => e.Id == id).FirstOrDefaultAsync();
                if (entrenamiento != null && entrenamiento.IdUsuario != User.Identity!.Name) {
                    LambdaLogger.Log(
                        $"[DELETE] - [EntrenamientoController] - [Eliminar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status401Unauthorized}] - " +
                        $"No se elimina el entrenamiento dado que no pertenece al usuario {User.Identity!.Name} - ID: {id}.");
                    return Unauthorized();
                }

                // Si existe el entrenamiento, se elimina...
                if (entrenamiento != null) {
                    _context.Entrenamientos.Remove(entrenamiento);
                    await _context.SaveChangesAsync();
                }

                LambdaLogger.Log(
                    $"[DELETE] - [EntrenamientoController] - [Eliminar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
                    $"Se elimina correctamente el entrenamiento del usuario {User.Identity!.Name} - ID: {id}.");

                return Ok();
            } catch (Exception ex) {
                LambdaLogger.Log(
                    $"[DELETE] - [EntrenamientoController] - [Eliminar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
                    $"Ocurrió un error al eliminar un entrenamiento del usuario {User.Identity!.Name} - ID: {id}. " +
                    $"{ex}");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }

        [Route("[action]")]
        [HttpPut]
        public async Task<ActionResult<Entrenamiento>> Actualizar(long id, EntEntrenamiento entEntrenamiento) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try {
                // Se valida si entrenamiento pertenece al usuario conectado...
                Entrenamiento? entrenamiento = await _context.Entrenamientos.Where(e => e.Id == id).FirstOrDefaultAsync();
                if (entrenamiento != null && entrenamiento.IdUsuario != User.Identity!.Name) {
                    LambdaLogger.Log(
                        $"[PUT] - [EntrenamientoController] - [Actualizar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status401Unauthorized}] - " +
                        $"No se actualiza el entrenamiento dado que no pertenece al usuario {User.Identity!.Name} - ID: {id}.");
                    return Unauthorized();
                }

                if (entrenamiento == null) {
                    LambdaLogger.Log(
                        $"[PUT] - [EntrenamientoController] - [Actualizar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status400BadRequest}] - " +
                        $"No se actualiza el entrenamiento dado que no existe - Usuario {User.Identity!.Name} - ID: {id}.");
                    return BadRequest();
                }

                entrenamiento.Inicio = entEntrenamiento.Inicio;
                entrenamiento.Termino = entEntrenamiento.Termino;
                entrenamiento.IdTipoEjercicio = entEntrenamiento.IdTipoEjercicio;
                entrenamiento.Serie = entEntrenamiento.Serie;
                entrenamiento.Repeticiones = entEntrenamiento.Repeticiones;
                entrenamiento.SegundosEntrenamiento = entEntrenamiento.SegundosEntrenamiento;
                entrenamiento.SegundosDescanso = entEntrenamiento.SegundosDescanso;
                _context.Update(entrenamiento);
                await _context.SaveChangesAsync();

                LambdaLogger.Log(
                    $"[PUT] - [EntrenamientoController] - [Actualizar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status200OK}] - " +
                    $"Se actualiza correctamente el entrenamiento del usuario {User.Identity!.Name} - ID: {id}.");

                return Ok(entrenamiento);
            } catch (Exception ex) {
                LambdaLogger.Log(
                    $"[PUT] - [EntrenamientoController] - [Actualizar] - [{stopwatch.ElapsedMilliseconds} ms] - [{StatusCodes.Status500InternalServerError}] - " +
                    $"Ocurrió un error al actualizar un entrenamiento del usuario {User.Identity!.Name} - ID: {id} - {entEntrenamiento}. " +
                    $"{ex}");
                return StatusCode(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
