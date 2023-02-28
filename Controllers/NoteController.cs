using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Differencing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace KrakenNotes.Web.Controllers
{
	public class NoteController: Controller
	{
		private NoteModel _notes;

		public NoteController()
		{
			_notes = GetNotes();
		}

		public IActionResult Index()
		{
			_notes.Notes = _notes.Notes.OrderBy(n => n.Description.Split('\n').Length).ToList();
			return View(_notes.Notes);
		}

		[HttpGet]
		public IActionResult Create()
		{
			return View();
		}

		[HttpPost]
		public IActionResult Create(Note model)
		{
			model.Id = _notes.Notes.LastOrDefault()?.Id + 1 ?? 1;
			_notes.Notes.Add(model);
			SetNotes(_notes);
			return RedirectToAction("Index");
		}

		[HttpGet]
		public IActionResult Edit(int id)
		{
			var note = _notes.Notes.FirstOrDefault( n => n.Id == id);
			return View(note);
		}

		[HttpPost]
		public async Task<IActionResult> Edit(Note model)
		{
			var note = _notes.Notes.FirstOrDefault(n => n.Id == model.Id);
			note.Title = model.Title;
			note.Description = model.Description;
			SetNotes(_notes);
			return RedirectToAction("Index");
		}

		// GET: CreditController/Delete/5
		public ActionResult Delete(int id)
		{
			TempData["Id"] = id;
			TempData["Method"] = "Delete";
			return RedirectToAction(nameof(Index));
		}

		// POST: CreditController/Delete/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult Delete(string id)
		{
			_notes.Notes.RemoveAll(n => n.Id == int.Parse(id));
			SetNotes(_notes);
			return RedirectToAction("Index");
		}

		public IActionResult Search(SearchModel srcModel)
		{
			var result = _notes.Notes.FindAll(n => n.Title.Contains(srcModel.SearchText));

			var sResult = result.Select(n => new Note
			{
				Description = n.Description,
				Id = n.Id,
				Title = n.Title
			});

			var model = new SearchModel
			{
				SearchText = "",
				SearchResult = sResult
			};

			return View(model);
		}

		private NoteModel GetNotes()
		{
			var notes = JsonFile.Read<NoteModel>("Notes", new NoteModel());
			return notes;
		}

		private void SetNotes(NoteModel notes)
		{
			JsonFile.Write<NoteModel>("Notes", notes);
		}
	}
}