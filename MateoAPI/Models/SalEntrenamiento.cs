using MateoAPI.Entities.Models;

namespace MateoAPI.Models {
    public class SalEntrenamiento {

        public DateTime Desde { get; set; }

        public DateTime Hasta { get; set; }

        public int Pagina { get; set; }

        public int TotalPaginas { get; set; }

        public int CantidadElementosPorPagina { get; set; }
        
        public int CantidadTotalEntrenamientos {  get; set; }

        public List<Entrenamiento>? Entrenamientos { get; set; }
    }
}
