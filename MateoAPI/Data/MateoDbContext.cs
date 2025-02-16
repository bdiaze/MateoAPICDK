using MateoAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace MateoAPI.Data {
    public class MateoDbContext : DbContext {
        
        public MateoDbContext(DbContextOptions<MateoDbContext> options) : base(options) { }
        
        public DbSet<Entrenamiento> Entrenamientos { get; set; }
    }
}
