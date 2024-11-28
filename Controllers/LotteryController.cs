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
		public ActionResult Create(string? dateString, string? lottery, int? clientId, bool cc = false)
		{
			cc = cc ? cc : TempData.Put<Paper>("Paper", null);
			var paper = TempData.Get<Paper>("Paper") ?? new Paper() { CreationDate = DateTime.Now };

			DateTime date = string.IsNullOrEmpty(dateString) ? paper.CreationDate : DateTime.Parse(dateString);
			UpdateLotteries(date);
			UpdatePaper(paper, lottery, clientId, date);

			ViewData["Names"] = _lotteries;
			ViewData["Clients"] = _credits.Select(c => c.Client).ToList();
			ViewData["busted"] = _lotteries.FirstOrDefault(l => l.Name == paper.Lottery)?.Busted ?? false;

			TempData.Put("Paper", paper);
			return View(paper);
		}

		// POST: LotteryController/Save
		public ActionResult Save()
		{
			try
			{
				var paper = TempData.Get<Paper>("Paper");
				if(paper != null)
				{
					var lottery = _lotteries.First(l => l.Name == paper.Lottery);
					paper.DrawDate = new DateTime(paper.DrawDate.Year, paper.DrawDate.Month, paper.DrawDate.Day, lottery.Hour.Hours, lottery.Hour.Minutes, lottery.Hour.Seconds);
					paper.CreationDate = DateTime.Now;
					_papers.Add(paper);
					SetPapers(_papers);
					if(paper.ClientId != null && paper.ClientId != -1)
					{
						var amount = paper.Numbers.Sum(p => p.Amount) + paper.Numbers.Sum(p => p.Busted);
						Add((int)paper.ClientId, paper.Lottery, amount);
					}
				}
				TempData.Put<Paper>("Paper", null);
				return RedirectToAction(nameof(Print), new { id = paper?.Id });
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		public ActionResult Print(int id)
		{
			var paper = _papers.FirstOrDefault(p => p.Id == id);
			var client = _credits.Select(c => c.Client).FirstOrDefault(c => c.Id == paper?.ClientId)?.Name;
			ViewData["Date"] = DateTime.Now.ToShortDateString();
			ViewData["Client"] = client;
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
				return RedirectToAction(nameof(Create), new { cc = true });
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

		private void UpdatePaper(Paper paper, string lottery, int? clientId, DateTime date)
		{
			paper.Id = _papers.LastOrDefault()?.Id + 1 ?? 1;
			paper.Lottery = lottery ?? paper.Lottery;
			paper.ClientId = clientId ?? paper.ClientId;
			paper.DrawDate = date;
			paper.CreationDate = date;
		}
	}
}
