using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
    [Authorize]
    public class NoteController: Controller
    {
        private readonly NoteModel _notes;

        public NoteController()
        {
            _notes = GetNotes();
        }

        public IActionResult Index(string? searchText, DateTime? shiftDate, NotePaymentMethod? paymentMethod)
        {
            var selectedDate = shiftDate?.Date ?? DateTime.Today;
            var notes = _notes.Notes.AsEnumerable()
                .Where(n => n.ShiftDate.Date == selectedDate);

            if(!string.IsNullOrWhiteSpace(searchText))
                notes = notes.Where(n => n.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));

            if(paymentMethod.HasValue)
                notes = notes.Where(n => n.PaymentMethod == paymentMethod.Value);

            var filteredNotes = notes
                .OrderBy(n => n.PaymentMethod)
                .ThenBy(n => n.Title)
                .ToList();

            ViewBag.SearchText = searchText;
            ViewBag.ShiftDate = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.PaymentMethod = paymentMethod;
            ViewBag.SinpeTotal = filteredNotes.Where(n => n.PaymentMethod == NotePaymentMethod.SINPE).Sum(n => n.Value);
            ViewBag.CardTotal = filteredNotes.Where(n => n.PaymentMethod == NotePaymentMethod.Tarjeta).Sum(n => n.Value);
            ViewBag.GrandTotal = filteredNotes.Sum(n => n.Value);
            return View(filteredNotes);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Note { ShiftDate = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Note model)
        {
            if(!ModelState.IsValid)
                return View(model);

            model.Id = _notes.Notes.Any() ? _notes.Notes.Max(n => n.Id) + 1 : 1;
            _notes.Notes.Add(model);
            SetNotes(_notes);
            TempData["SuccessMessage"] = "Nota creada correctamente.";
            return RedirectToAction(nameof(Index), new { shiftDate = model.ShiftDate.ToString("yyyy-MM-dd") });
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var note = _notes.Notes.FirstOrDefault(n => n.Id == id);
            if(note is null)
                return NotFound();

            return View(note);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Note model)
        {
            if(!ModelState.IsValid)
                return View(model);

            var note = _notes.Notes.FirstOrDefault(n => n.Id == model.Id);
            if(note is null)
                return NotFound();

            note.Title = model.Title;
            note.Value = model.Value;
            note.PaymentMethod = model.PaymentMethod;
            note.ShiftDate = model.ShiftDate.Date;
            SetNotes(_notes);
            TempData["SuccessMessage"] = "Nota actualizada correctamente.";
            return RedirectToAction(nameof(Index), new { shiftDate = model.ShiftDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var removed = _notes.Notes.RemoveAll(n => n.Id == id) > 0;
            if(!removed)
                return NotFound();

            SetNotes(_notes);
            TempData["SuccessMessage"] = "Nota eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Search(SearchModel srcModel)
        {
            return RedirectToAction(nameof(Index), new { searchText = srcModel.SearchText });
        }

        private static NoteModel GetNotes()
        {
            var notes = JsonFile.Read<NoteModel>("Notes", new NoteModel());
            foreach(var note in notes.Notes.Where(n => n.ShiftDate == default))
            {
                note.ShiftDate = DateTime.Today;
            }
            return notes;
        }

        private static void SetNotes(NoteModel notes)
        {
            JsonFile.Write<NoteModel>("Notes", notes);
        }
    }
}
