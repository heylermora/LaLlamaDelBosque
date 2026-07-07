using System.ComponentModel.DataAnnotations;

namespace LaLlamaDelBosque.Models
{
    public class NoteModel
    {
        public List<Note> Notes { get; set; } = new List<Note>();
    }

    public enum NotePaymentMethod
    {
        SINPE = 1,
        Tarjeta = 2
    }

    public class Note
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El campo es requerido")]
        [StringLength(50, ErrorMessage = "La longitud del título debe estar entre {2} y {1}.", MinimumLength = 2)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "El campo es requerido")]
        [Range(10, 1000000, ErrorMessage = "El valor debe estar entre {1} y {2}.")]
        public double Value { get; set; }

        [Required(ErrorMessage = "El método de pago es requerido")]
        [Display(Name = "Método de pago")]
        public NotePaymentMethod PaymentMethod { get; set; } = NotePaymentMethod.SINPE;

        [Required(ErrorMessage = "El día de turno es requerido")]
        [DataType(DataType.Date)]
        [Display(Name = "Día de turno")]
        public DateTime ShiftDate { get; set; } = DateTime.Today;
    }
}
