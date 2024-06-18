using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System.Globalization;
using System.Reflection;

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
        public ActionResult Index(string searchString, int clientId, int currentPage = 1)
        {
            var credits = _credits.Credits;
            if(!string.IsNullOrEmpty(searchString))
            {
                credits = credits.Where(s => s.Client.Name.ToLower().Contains(searchString.ToLower())).ToList();
            }
            credits = credits.OrderBy(c => c.Client.Name).ToList();

			int totalItems = credits.Count();
			int totalPages = (int)Math.Ceiling((double)totalItems / 20);
			currentPage = Math.Max(1, Math.Min(currentPage, totalPages));
			int startIndex = (currentPage - 1) * 20;
			int endIndex = Math.Min(startIndex + 20 - 1, totalItems - 1);

			credits = credits.Where((item, index) => index >= startIndex && index <= endIndex).ToList();

			foreach(var credit in credits)
			{
				credit.CreditSummary.CalculateStatus(credit.Client.Limit);
			}

			ViewBag.TotalPages = totalPages;
			ViewBag.CurrentPage = currentPage;
			ViewBag.ClientId = clientId;
            ViewBag.SearchString = searchString;

			return View(credits);
        }

        public ActionResult IndexPdf()
        {
            return new ViewAsPdf("_Report", _credits.Credits)
            {
                PageSize = Size.A4,
                FileName = $"Resumen del {DateTime.Today.ToShortDateString()}.pdf",
                PageMargins = new Margins(10, 20, 10, 20)
            };
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
                        Name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(collection["name"]),
                        Phone = collection["phone"],
                        Limit = double.Parse(collection["limit"])
					}
                };

				_credits.Credits.Add(credit);
                SetCredits(_credits);
                return RedirectToAction(nameof(Index));
            }
            catch(Exception ex)
            {
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
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
                    Phone = collection["phone"],
					Limit = !string.IsNullOrEmpty(collection["limit"]) ? double.Parse(collection["limit"]) : null
				};
                SetCredits(_credits);
                return RedirectToAction(nameof(Index));
            }
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
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
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

        #region Credit Line

        // POST: CreditController/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Add(int id, string searchString, int currentPage, IFormCollection collection)
        {
            try
            {
                var credit = _credits.Credits.FirstOrDefault(x => x.Client.Id == id);
                if(double.Parse(collection["amount"]) > 0)
                {
                    var creditLine = new CreditLine()
                    {
                        Id = credit?.CreditLines.LastOrDefault()?.Id + 1 ?? 1,
                        CreatedDate = DateTime.Now,
                        Description = collection["description"],
                        Amount = double.Parse(collection["amount"])
                    };

                    if(credit != null && credit.CreditSummary != null)
                    {
                        credit.CreditLines.Add(creditLine);
                        credit.CreditSummary.Total = credit.CreditSummary.Total + creditLine.Amount;
                    }

                    SetCredits(_credits);
                }
                return RedirectToAction(nameof(Index), new { searchString = searchString, clientId = id, currentPage = currentPage,  });
            }
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

        // POST: CreditController/Fee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Fee(int id, string searchString, int currentPage, IFormCollection collection)
        {
            try
            {
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

                    if(credit != null && credit.CreditSummary != null)
                    {
                        credit.CreditLines.Add(creditLine);
                        credit.CreditSummary.Total = credit.CreditSummary.Total + creditLine.Amount;
                    }

                    SetCredits(_credits);
                }
                return RedirectToAction(nameof(Index), new { searchString = searchString, clientId = id, currentPage = currentPage });
            }
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

        // POST: CreditController/Refresh/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Refresh(int clientId, string searchString, int currentPage, IFormCollection collection)
        {
            try
            {
                var credit = _credits.Credits.First(x => x.Client.Id == clientId);
                var line = credit.CreditLines.First(l => l.Id == int.Parse(collection["CreditLine.Id"]));

                credit.CreditSummary.Total -= line.Amount;

                line.Description = collection["CreditLine.description"];
                line.Amount = double.Parse(collection["CreditLine.amount"]);

                credit.CreditSummary.Total += line.Amount;

                SetCredits(_credits);
                return RedirectToAction(nameof(Index), new { searchString = searchString, clientId = clientId, currentPage = currentPage });
            }
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

        // POST: CreditController/Remove/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Remove(string clientId, string searchString, int currentPage, string lineId)
        {
            try
            {
                var credit = _credits.Credits.First(c => c.Client.Id == int.Parse(clientId));
                var line = credit.CreditLines.First(l => l.Id == int.Parse(lineId));
                credit.CreditSummary.Total -= line.Amount;
                credit.CreditLines.Remove(line);
                SetCredits(_credits);
                return RedirectToAction(nameof(Index), new { searchString = searchString, clientId = clientId, currentPage = currentPage });
            }
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

        // POST: CreditController/Clear/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Clear(string? Id, string searchString, int currentPage)
        {
            try
            {
                var credit = _credits.Credits.First(c => c.Client.Id == int.Parse(Id ?? "0"));
                credit.CreditSummary.Total = 0;
                credit.CreditLines.Clear();
                SetCredits(_credits);
                return RedirectToAction(nameof(Index), new { searchString = searchString, clientId = Id, currentPage = currentPage });
            }
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}
        #endregion

        private CreditModel GetCredits()
        {
            var credits = JsonFile.Read("Credits", new CreditModel());
            return credits;
        }

        private void SetCredits(CreditModel credits)
        {
            JsonFile.Write("Credits", credits);
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
