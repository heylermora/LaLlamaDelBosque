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

        public IActionResult Index(string? searchText)
        {
            var notes = _notes.Notes.AsEnumerable();

            if(!string.IsNullOrWhiteSpace(searchText))
                notes = notes.Where(n => n.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));

            ViewBag.SearchText = searchText;
            return View(notes.OrderBy(n => n.Title).ToList());
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Note());
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
            return RedirectToAction(nameof(Index));
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
            SetNotes(_notes);
            TempData["SuccessMessage"] = "Nota actualizada correctamente.";
            return RedirectToAction(nameof(Index));
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
            return notes;
        }

        private static void SetNotes(NoteModel notes)
        {
            JsonFile.Write<NoteModel>("Notes", notes);
        }
    }
}
