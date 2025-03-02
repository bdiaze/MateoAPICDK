using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MateoAPI.Entities.Models {
    [Table("entrenamiento", Schema = "mateo")]
    public class Entrenamiento {
        [Required]
        [Column("id")]
        public long Id { get; set; }

        [Required]
        [Column("id_usuario")]
        public required string IdUsuario { get; set; }

        [Required]
        [Column("id_request")]
        public required Guid IdRequest { get; set; }

        [Required]
        [Column("inicio")]
        public DateTime Inicio { get; set; }

        [Required]
        [Column("termino")]
        public DateTime Termino { get; set; }

        [Column("id_tipo_ejercicio")]
        public int? IdTipoEjercicio { get; set; }

        [Column("serie")]
        public short? Serie { get; set; }

        [Column("repeticiones")]
        public short? Repeticiones { get; set; }

        [Column("segundos_entrenamiento")]
        public short? SegundosEntrenamiento { get; set; }

        [Column("segundos_descanso")]
        public short? SegundosDescanso { get; set; }
    }
}
