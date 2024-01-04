using System.ComponentModel.DataAnnotations;

namespace LaLlamaDelBosque.Models
{
    public class NoteModel
    {
        public List<Note> Notes { get; set; } = new List<Note>();
    }

    public class Note
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El campo es requerido")]
        [StringLength(50, ErrorMessage = "La longitud del título debe estar entre {2} y {1}.", MinimumLength = 2)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "El campo es requerido")]
        [Range(10, 1000000, ErrorMessage = "La longitud de la descripción debe estar entre {2} y {1}.")]
        public double Value { get; set; }
    }
}
