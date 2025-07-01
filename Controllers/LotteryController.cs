using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
	public class LotteryController: Controller
	{
		private List<Lottery> _lotteries;
		private readonly List<Paper> _papers;
		private readonly List<Credit> _credits;
		private readonly List<Award> _awards;

		public LotteryController()
		{
			_lotteries = GetLotteries();
			_papers = GetPapers();
			_credits = GetCredits();
			_awards = GetAwards();
		}

		// GET: LotteryController
		public ActionResult Index(int? id, string lottery, DateTime? fromDate, DateTime? toDate)
		{
			try
			{
				var searchModel = new LotterySearchModel()
				{
					Id = id,
					Lottery = lottery,
					FromDate = fromDate ?? DateTime.Now,
					ToDate = toDate ?? DateTime.Now
				};

				_lotteries = _lotteries.OrderBy(l => l.Hour).ToList();
				var papers = _papers.Where(p =>
					((id != null && lottery == null && p.Id == id) ||
					(id == null && lottery != null && p.Lottery == lottery) ||
					(id != null && lottery != null && p.Id == id && p.Lottery == lottery) ||
					(lottery == "TODOS")) &&
					(p.DrawDate.Date >= fromDate && p.DrawDate.Date <= toDate)
				).ToList();

				if((papers.All(p => p.DrawDate.Date == searchModel.FromDate.Value.Date &&
									searchModel.FromDate.Value.Date == searchModel.ToDate.Value.Date)
									&& !string.IsNullOrEmpty(lottery) && lottery != "TODOS" && papers.Count > 0) || papers.Count == 1)
				{
					var number = _awards.FirstOrDefault(a => a.Date.Date == searchModel.FromDate)?.AwardLines.FirstOrDefault(l => l.Description == lottery)?.Number;
					ViewBag.Number = number;
				}

				ViewData["Names"] = _lotteries;
				ViewData["LotterySearchModel"] = searchModel;

				return View(papers);
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		// GET: LotteryController/Create
		public ActionResult Create(string? dateString, string? selectedLotteries, int? clientId, bool cc = false)
		{
			// Limpiar TempData si cc es falso
			if(!cc)
				TempData.Put<Paper>("Paper", null);

			// Recuperar papelito o crear uno nuevo
			var paper = TempData.Get<Paper>("Paper") ?? new Paper { CreationDate = DateTime.Now };

			// Parsear la fecha o usar la de creación
			var date = string.IsNullOrEmpty(dateString) ? paper.CreationDate : DateTime.Parse(dateString);

			// Actualizar sorteos disponibles en esa fecha
			UpdateLotteries(date);

			// Filtrar sorteos válidos desde el string recibido
			var selectedList = (selectedLotteries ?? "")
				.Split(",", StringSplitOptions.RemoveEmptyEntries)
				.Select(name => name.Trim())
				.Where(name => _lotteries.Any(l => l.Name == name))
				.Distinct()
				.ToList();

			// Actualizar el modelo con los sorteos válidos
			paper.SelectedLotteries = selectedList;

			// Actualizar datos del papelito (cliente, loterías, fecha)
			UpdatePaper(paper, string.Join(", ", selectedList), clientId, date);

			// Preparar datos para la vista
			ViewData["Names"] = _lotteries;
			ViewData["Clients"] = _credits.Select(c => c.Client).ToList();
			ViewData["busted"] = _lotteries.FirstOrDefault(l => l.Name == paper.Lottery)?.Busted ?? false;

			// Guardar temporalmente el estado del papelito
			TempData.Put("Paper", paper);

			return View(paper);
		}


		// POST: LotteryController/Save
		public ActionResult Save()
		{
			try
			{
				var paper = TempData.Get<Paper>("Paper");
				var ids = new List<int>();
				if(paper != null)
				{
					var lotteryNames = (paper.Lottery ?? "").Split(",", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
					foreach(var name in lotteryNames)
					{
						var lottery = _lotteries.FirstOrDefault(l => l.Name == name);
						if(lottery == null) continue;

						var id = _papers.Any() ? _papers.Max(p => p.Id) + 1 : 1;
						ids.Add(id);
						var newPaper = new Paper
						{
							Id = id,
							Numbers = paper.Numbers.Select(n => new Number
							{
								Amount = n.Amount,
								Busted = n.Busted,
								Value = n.Value
							}).ToList(),
							Lottery = name,
							ClientId = paper.ClientId,
							DrawDate = new DateTime(
								paper.DrawDate.Year,
								paper.DrawDate.Month,
								paper.DrawDate.Day,
								lottery.Hour.Hours,
								lottery.Hour.Minutes,
								lottery.Hour.Seconds
							),
							CreationDate = DateTime.Now
						};

						_papers.Add(newPaper);

						if(newPaper.ClientId != null && newPaper.ClientId != -1)
						{
							var amount = newPaper.Numbers.Sum(p => p.Amount) + newPaper.Numbers.Sum(p => p.Busted);
							Add((int)newPaper.ClientId, name, amount);
						}
					}
					SetPapers(_papers);
				}
				TempData.Put<Paper>("Paper", null);
				return RedirectToAction(nameof(Print), new { ids = ids });
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
			}
		}

		public ActionResult Print(List<int>? ids = null, int? id = null)
		{
			var selectedIds = (ids?.Any() == true) ? ids : (id.HasValue ? new List<int> { id.Value } : null);
			if(selectedIds == null || !selectedIds.Any()) return BadRequest("No se especificaron IDs válidos.");

			var papers = _papers.Where(p => selectedIds.Contains(p.Id)).ToList();
			if(!papers.Any()) return NotFound();

			var paper = papers.First();
			paper.Lottery = string.Join(", ", papers.Select(p => p.Lottery).Distinct());

			ViewData["Date"] = DateTime.Now.ToShortDateString();
			ViewData["Client"] = _credits.Select(c => c.Client).FirstOrDefault(c => c.Id == paper.ClientId)?.Name;
			ViewData["Cant"] = selectedIds.Count;
			ViewData["Ids"] = string.Join(", ", papers.Select(p => "#" + p.Id));

			return View(paper);
		}

		// POST: LotteryController/Copy/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Copy(int id)
		{
			try
			{
				var numbers = _papers.FirstOrDefault(p => p.Id == id)?.Numbers;
				var paper = TempData.Get<Paper>("Paper");
				if(paper != null)
				{
					paper.Numbers = numbers ?? new List<Number>();
				}
				TempData.Put("Paper", paper);
				return RedirectToAction(nameof(Create), new { cc = true });
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		// POST: CreditController/Delete/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Delete(int id, string lottery, string fromDate, string toDate)
		{
			Console.WriteLine("Id: " + id);
			try
			{
				var paper = _papers.First(x => x.Id == id);
				_papers.Remove(paper);
				SetPapers(_papers);
				return RedirectToAction(nameof(Index), new { lottery, fromDate = DateTime.Parse(fromDate), toDate = DateTime.Parse(toDate) });
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		#region Lottery Line
		// GET: LotteryController/Add/
		public ActionResult Add(IFormCollection collection)
		{
			try
			{
				double amount;
				double busted;

				var amountParsed = double.TryParse(collection["amount"], out amount);
				var bustedParsed = double.TryParse(collection["busted"], out busted);

				var numberList = collection["value"].ToString().TrimEnd('+').Split('+');

				var paper = TempData.Get<Paper>("Paper") ?? new Paper();
				var count = paper.Numbers.Max(p => p.Id) ?? 0;

				foreach(var number in numberList)
				{
					var value = number;
					var numbers = paper.Numbers.Find(x => x.Value == value);
					if(numbers != null)
					{
						numbers.Amount += amount;
						numbers.Busted += busted;
					}
					else
					{
						paper.Numbers.Add(new Number()
						{
							Id = ++count,
							Amount = amount,
							Busted = busted,
							Value = number
						});
					}
				}
				paper.Numbers = paper.Numbers.OrderBy(x => x.Value).ToList();
				TempData.Put("Paper", paper);
				return RedirectToAction(nameof(Create), new { selectedLotteries = paper.Lottery,  cc = true });
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		// POST: LotteryController/Remove/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Remove(string paperId, string lineId)
		{
			try
			{
				TempData["Id"] = int.Parse(paperId);
				var paper = TempData.Get<Paper>("Paper") ?? new Paper();
				var line = paper.Numbers.First(l => l.Id == int.Parse(lineId));
				paper.Numbers.Remove(line);
				TempData.Put("Paper", paper);
				return RedirectToAction(nameof(Create));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		// GET: LotteryController/Clear/
		public ActionResult Clear()
		{
			try
			{
				TempData.Put<Paper>("Paper", null);
				return RedirectToAction(nameof(Create));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		#endregion

		private static List<Lottery> GetLotteries()
		{
			var lotteries = JsonFile.Read("Lotteries", new LotteryModel());
			return lotteries.Lotteries;
		}

		private static List<Paper> GetPapers()
		{
			var papers = JsonFile.Read("Papers", new PaperModel());
			return papers.Papers;
		}

		private static void SetPapers(List<Paper> papers)
		{
			var paperModel = new PaperModel()
			{
				Papers = papers
			};
			JsonFile.Write("Papers", paperModel);
		}

		private static List<Credit> GetCredits()
		{
			var credits = JsonFile.Read("Credits", new CreditModel());
			return credits.Credits.ToList();
		}

		private static void SetCredits(List<Credit> credits)
		{
			var creditModel = new CreditModel()
			{
				Credits = credits
			};
			JsonFile.Write("Credits", creditModel);
		}

		private List<Award> GetAwards()
		{
			var award = JsonFile.Read("Awards", new AwardModel());
			return award.Awards.ToList();
		}

		private void Add(int id, string lottery, double amount)
		{
			var credit = _credits.FirstOrDefault(x => x.Client.Id == id);
			if(credit is not null)
			{
				var creditLine = new CreditLine()
				{
					Id = credit?.CreditLines.LastOrDefault()?.Id + 1 ?? 1,
					CreatedDate = DateTime.Now,
					Description = "SORTEO: " + lottery,
					Amount = amount
				};

				if(credit != null && credit.CreditSummary != null)
				{
					credit.CreditLines.Add(creditLine);
					credit.CreditSummary.Total = credit.CreditSummary.Total + creditLine.Amount;
				}

				SetCredits(_credits);
			};
			return;
		}

		private void UpdateLotteries(DateTime date)
		{
			_lotteries = _lotteries
				.Where(l => (date.ToShortDateString() == DateTime.Today.ToShortDateString() ?
							 l.Hour.Add(TimeSpan.FromMinutes(-10)) > DateTime.Now.TimeOfDay : true) &&
							(l.Days?.Contains(date.DayOfWeek.ToString()) ?? true))
				.OrderBy(l => l.Hour)
				.ToList();
		}

		private void UpdatePaper(Paper paper, string selectedLotteries, int? clientId, DateTime date)
		{
			paper.Id = _papers.LastOrDefault()?.Id + 1 ?? 1;
			paper.Lottery = selectedLotteries ?? paper.Lottery;
			paper.ClientId = clientId ?? paper.ClientId;
			paper.DrawDate = date;
			paper.CreationDate = date;
		}
	}
}
