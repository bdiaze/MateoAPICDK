using MateoAPI.Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace MateoAPI.Entities.Contexts {
    public class MateoDbContext : DbContext {

        public MateoDbContext(DbContextOptions<MateoDbContext> options) : base(options) { }

        public DbSet<Entrenamiento> Entrenamientos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.UseIdentityAlwaysColumns();
        }
    }
}
