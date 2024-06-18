using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace LaLlamaDelBosque.Controllers
{
    [Authorize]

    public class FamilyController: Controller
    {
        private FamilyModel _families;

        public FamilyController()
        {
			_families = GetFamilies();
        }

        // GET: FamilyController
        public ActionResult Index()
        {
            var families = _families.Families;
			return View(families);
        }

		// GET: FamilyController/Create
		public ActionResult Create()
		{
			return View();
		}

		// POST: FamilyController/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Create(IFormCollection collection)
		{
			try
			{
				var family = new Family()
				{
					Id = _families.Families.LastOrDefault()?.Id + 1 ?? 1,
					Name = collection["name"],
				};

				_families.Families.Add(family);
				SetFamilies(_families);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		// POST: FamilyController/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Edit(string id, IFormCollection collection)
		{
			try
			{
				var family = _families.Families.First(x => x.Id == int.Parse(id));
				family.Name = collection["name"];
				SetFamilies(_families);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		// POST: FamilyController/Delete/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Delete(string id)
		{
			try
			{
				var family = _families.Families.First(x => x.Id == int.Parse(id));
				_families.Families.Remove(family);
				SetFamilies(_families);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		private FamilyModel GetFamilies()
        {
            var families = JsonFile.Read("Family", new FamilyModel());
            return families;
        }

        private void SetFamilies(FamilyModel family)
        {
            JsonFile.Write("Family", family);
        }
    }
}
