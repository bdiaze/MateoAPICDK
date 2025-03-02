using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;

namespace MateoAPI.Models {
    public class EntEntrenamiento {

        public Guid IdRequest { get; set; }
        
        public DateTime Inicio { get; set; }

        public DateTime Termino { get; set; }

        public int? IdTipoEjercicio { get; set; }

        public short Serie {  get; set; }

        public short? Repeticiones { get; set; }

        public short SegundosEntrenamiento { get; set; }

        public short SegundosDescanso { get; set; }

        public override string ToString() {
            return $"Inicio: {Inicio:O} - Termino: {Termino:O} - IdTipoEjercicio: {IdTipoEjercicio} - " +
                $"Serie: {Serie} - Repeticiones: {Repeticiones} - SegundosEntrenamiento: {SegundosEntrenamiento} - SegundosDescanso: {SegundosDescanso}";
        }
    }
}
