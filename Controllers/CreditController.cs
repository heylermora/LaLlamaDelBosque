using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
    public class CreditController: Controller
    {
		private CreditModel _credits;

		public CreditController()
		{
			_credits = GetCredits();
		}

		// GET: CreditController
		public ActionResult Index()
        {
            return View(_credits.Credits);
        }

        // GET: CreditController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: CreditController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
				var credit = new Credit()
				{
                    Client = new Client()
                    {
                        Id = _credits.Credits.LastOrDefault()?.Client.Id + 1 ?? 1,
                        Name = collection["name"],
                        Phone = collection["phone"]
                    }
				};

				_credits.Credits.Add(credit);
				SetCredits(_credits);
				return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: CreditController/Edit/5
        public ActionResult Edit(int id)
        {
			TempData["Id"] = id;
			TempData["Method"] = "Edit";
			return RedirectToAction(nameof(Index));
		}

        // POST: CreditController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, IFormCollection collection)
        {
            try
            {
				var credit = _credits.Credits.First(x => x.Client.Id == int.Parse(id));
                credit.Client = new Client()
                {
                    Id = int.Parse(id),
                    Name = collection["name"],
                    Phone = collection["phone"]
				};
                SetCredits(_credits);
				return RedirectToAction(nameof(Index));
            }
            catch
            {
				return RedirectToAction(nameof(Index));
			}
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
        public ActionResult Delete(string id)
        {
            try
            {
				var credit = _credits.Credits.First(x => x.Client.Id == int.Parse(id));
				_credits.Credits.Remove(credit);
				SetCredits(_credits);
				return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

		#region Credit Line
		// GET: CreditController/Add
		public ActionResult Add(int id)
		{
			TempData["Id"] = id;
			TempData["Method"] = "Add";
			return RedirectToAction(nameof(Index));
		}

		// POST: CreditController/Add
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Add(int id, IFormCollection collection)
		{
			try
			{
				var credit = _credits.Credits.FirstOrDefault(x => x.Client.Id == id);

				if(credit is not null) {
					var creditLine = new CreditLine()
					{
						Id = credit?.CreditLines.LastOrDefault()?.Id + 1 ?? 1,
						CreatedDate = DateTime.Now,
						Description = collection["description"],
						Amount = double.Parse(collection["amount"])
					};

					credit.CreditLines.Add(creditLine);

					credit.CreditSummary = new CreditSummary()
					{
						Total = (credit.CreditSummary?.Total ?? 0) + creditLine.Amount,
						CountLines = credit.CreditLines.Count()
					};

					SetCredits(_credits);
				}
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return RedirectToAction(nameof(Index));
			}
		}
		#endregion

		private CreditModel GetCredits()
		{
			var credits = JsonFile.Read<CreditModel>("Credits", new CreditModel());
			return credits;
		}

		private void SetCredits(CreditModel credits)
		{
			JsonFile.Write<CreditModel>("Credits", credits);
		}
	}
}
