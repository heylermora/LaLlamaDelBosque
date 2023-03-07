using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
	[Authorize]

	public class CreditController: Controller
    {
		private CreditModel _credits;

		public CreditController()
		{
			_credits = GetCredits();
		}

		// GET: CreditController
		public ActionResult Index(string searchString)
        {
			var credits = _credits.Credits;

			if(!string.IsNullOrEmpty(searchString))
			{
				credits = credits.Where(s => s.Client.Name.ToLower().Contains(searchString.ToLower())).ToList();
			}

			TempData["Message"] = GetMessage(_credits.Credits);
			return View(credits);
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
				return RedirectToAction(nameof(Index));
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
				TempData["Id"] = id;
				var credit = _credits.Credits.FirstOrDefault(x => x.Client.Id == id);
				if(credit is not null && double.Parse(collection["amount"]) > 0) {
					var creditLine = new CreditLine()
					{
						Id = credit?.CreditLines.LastOrDefault()?.Id + 1 ?? 1,
						CreatedDate = DateTime.Now,
						Description = collection["description"],
						Amount = double.Parse(collection["amount"])
					};

					credit?.CreditLines.Add(creditLine);

					credit.CreditSummary.Total = (credit?.CreditSummary?.Total ?? 0) + creditLine.Amount;

					SetCredits(_credits);
				}
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return RedirectToAction(nameof(Index));
			}
		}

		// GET: CreditController/Fee
		public ActionResult Fee(int id)
		{
			TempData["Id"] = id;
			TempData["Method"] = "Fee";
			return RedirectToAction(nameof(Index));
		}

		// POST: CreditController/Fee
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Fee(int id, IFormCollection collection)
		{
			try
			{
				TempData["Id"] = id;
				var credit = _credits.Credits.FirstOrDefault(x => x.Client.Id == id);
				if(credit is not null)
				{
					var creditLine = new CreditLine()
					{
						Id = credit?.CreditLines.LastOrDefault()?.Id + 1 ?? 1,
						CreatedDate = DateTime.Now,
						Description = collection["description"],
						Amount = -(double.Parse(collection["amount"]))
					};

					credit?.CreditLines.Add(creditLine);

					credit.CreditSummary.Total = (credit?.CreditSummary?.Total ?? 0) + creditLine.Amount;

					SetCredits(_credits);
				}
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return RedirectToAction(nameof(Index));
			}
		}

		// GET: CreditController/Refresh/5
		public ActionResult Refresh(int clientId, int lineId)
		{
			TempData["Id"] = clientId;
			TempData["LineId"] = lineId;
			TempData["Method"] = "Refresh";
			return RedirectToAction(nameof(Index));
		}

		// POST: CreditController/Refresh/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Refresh(int clientId, IFormCollection collection)
		{
			try
			{
				TempData["Id"] = clientId;

				var credit = _credits.Credits.First(x => x.Client.Id == clientId);
				var line = credit.CreditLines.First(l => l.Id == int.Parse(collection["Id"]));

				credit.CreditSummary.Total -= line.Amount;

				line.Description = collection["description"];
				line.Amount = double.Parse(collection["amount"]);

				credit.CreditSummary.Total += line.Amount;

				SetCredits(_credits);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex);
				return RedirectToAction(nameof(Index));
			}
		}

		// GET: CreditController/Remove/5
		public ActionResult Remove(int clientId, int lineId)
		{
			TempData["Id"] = clientId;
			TempData["LineId"] = lineId;
			TempData["Method"] = "Remove";
			return RedirectToAction(nameof(Index));
		}

		// POST: CreditController/Remove/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Remove(string clientId, string lineId)
		{
			try
			{
				TempData["Id"] = int.Parse(clientId);
				var credit = _credits.Credits.First(c => c.Client.Id == int.Parse(clientId));
				var line = credit.CreditLines.First(l => l.Id == int.Parse(lineId));
				credit.CreditSummary.Total -= line.Amount; 
				credit.CreditLines.Remove(line);
				SetCredits(_credits);
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return RedirectToAction(nameof(Index));
			}
		}

		// GET: CreditController/Clear/5
		public ActionResult Clear(int Id)
		{
			TempData["Id"] = Id;
			TempData["Method"] = "Clear";
			return RedirectToAction(nameof(Index));
		}

		// POST: CreditController/Clear/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Clear(string? Id)
		{
			try
			{
				TempData["Id"] = int.Parse(Id);
				var credit = _credits.Credits.First(c => c.Client.Id == int.Parse(Id ?? "0"));
				credit.CreditSummary.Total = 0;
				credit.CreditLines.Clear();
				SetCredits(_credits);
				return RedirectToAction(nameof(Index));
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex);
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

		private string GetMessage(IList<Credit> credits)
		{
			var text = "";
			foreach(var credit in credits)
			{
				text += "%0A";
				text += $"✅ {credit.Client.Name.ToUpper()}: *₡ {credit.CreditSummary.Total}*. ";
			}
			return text;
		}
	}
}
