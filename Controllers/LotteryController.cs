using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace LaLlamaDelBosque.Controllers
{
	public class LotteryController: Controller
	{
		private List<Lottery> _lotteries;
		private List<Paper> _papers;
		private List<Credit> _credits;

		public LotteryController()
		{
			_lotteries = GetLotteries();
			_papers = GetPapers();
			_credits = GetCredits();
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
					FromDate = fromDate ?? DateTime.Today,
					ToDate = toDate ?? DateTime.Today
				};

				_lotteries = _lotteries.OrderBy(l => l.Hour).ToList();
				var papers = _papers.Where(p =>
					((id != null && lottery == null && p.Id == id) ||
					(id == null && lottery != null && p.Lottery == lottery) ||
					(id != null && lottery != null && p.Id == id && p.Lottery == lottery) ||
					(lottery == "TODOS")) &&
					(p.Date >= fromDate && p.Date <= toDate)
				).ToList();

				ViewData["Names"] = _lotteries;
				ViewData["LotterySearchModel"] = searchModel;
				TempData.Put("Papers", papers);

				return View(papers);
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex);
				return View();
			}
		}

		// GET: LotteryController/Create
		public ActionResult Create(string dateString)
		{
			DateTime date  = string.IsNullOrEmpty(dateString) ? DateTime.Today : DateTime.Parse(dateString);
			_lotteries = date == DateTime.Today ? _lotteries.Where(l => l.Hour > DateTime.Now.TimeOfDay).OrderBy(l => l.Hour).ToList() : _lotteries.OrderBy(l => l.Hour).ToList();
			ViewData["Names"] = _lotteries;
			ViewData["Clients"] = _credits.Select(c => c.Client).ToList();

			var paper = TempData.Get<Paper>("Paper") ?? new Paper();
			paper.Id = _papers.LastOrDefault()?.Id + 1 ?? 1;
			paper.Date = date;

			TempData.Put("Paper", paper);
			return View(paper);
		}

		// POST: LotteryController/Save
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Save(Paper paper)
		{
			try
			{
				var temPaper = TempData.Get<Paper>("Paper");
				paper.Id = temPaper.Id;
				paper.Numbers = temPaper.Numbers;
				if(paper != null)
				{
					paper.Hour = _lotteries.First(l => l.Name == paper.Lottery).Hour;
					_papers.Add(paper);
					SetPapers(_papers);
					if(paper.ClientId != null)
					{
						var amount = paper.Numbers.Sum(p => p.Amount) + paper.Numbers.Sum(p => p.Busted);
						Add((int)paper.ClientId, paper.Lottery, amount);
					}
				}
				TempData.Put<Paper>("Paper", null);
				return RedirectToAction(nameof(Print), new {id = paper?.Id});
			}
			catch
			{
				return View();
			}
		}

		public ActionResult Print(int id)
		{
			var paper = _papers.FirstOrDefault(p => p.Id == id);
			ViewData["Date"] = DateTime.Now.ToShortDateString();
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
				return RedirectToAction(nameof(Create));
			}
			catch
			{
				return View();
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
				Console.WriteLine(ex);
				return RedirectToAction(nameof(Index));
			}
		}

		#region Lottery Line
		// GET: LotteryController/Add/
		public ActionResult Add(IFormCollection collection)
		{
			try
			{
				var numberList = collection["value"].ToString().TrimEnd('+').Split('+');

				var paper = TempData.Get<Paper>("Paper") ?? new Paper();
				var count = paper.Numbers.Max( p => p.Id) ?? 0;

				foreach(var number in numberList)
				{
					var value = number;
					var numbers = paper.Numbers.Find(x => x.Value == value);
					if(numbers != null)
					{
						numbers.Amount += double.Parse(collection["amount"]);
						numbers.Busted += double.Parse(collection["busted"]);
					}
					else
					{
						paper.Numbers.Add(new Number()
						{
							Id = ++count,
							Amount = double.Parse(collection["amount"]),
							Busted = double.Parse(collection["busted"]),
							Value = number
						});
					}
				}
				paper.Numbers = paper.Numbers.OrderBy(x => x.Value).ToList();

				TempData.Put("Paper", paper);
				return RedirectToAction(nameof(Create));
			}
			catch
			{
				return RedirectToAction(nameof(Create));
			}
		}

		// GET: LotteryController/Remove/5
		public ActionResult Remove(int paperId, int lineId)
		{
			TempData["Id"] = paperId;
			TempData["LineId"] = lineId;
			TempData["Method"] = "Remove";
			return RedirectToAction(nameof(Create));
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
			catch
			{
				return RedirectToAction(nameof(Index));
			}
		}

		// GET: LotteryController/Clear/
		public ActionResult Clear()
		{
			try
			{
				var paper = TempData.Get<Paper>("Paper") ?? new Paper();
				paper.Numbers.Clear();
				TempData.Put("Paper", paper);
				return RedirectToAction(nameof(Create));
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex);
				return RedirectToAction(nameof(Index));
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

		private void SetPapers(List<Paper> papers)
		{
			var paperModel = new PaperModel()
			{
				Papers = papers
			};
			JsonFile.Write("Papers", paperModel);
		}

		private List<Credit> GetCredits()
		{
			var credits = JsonFile.Read("Credits", new CreditModel());
			return credits.Credits.ToList();
		}

		private void SetCredits(List<Credit> credits)
		{
			var creditModel = new CreditModel()
			{
				Credits = credits
			};
			JsonFile.Write("Credits", creditModel);
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

				credit?.CreditLines.Add(creditLine);

				credit.CreditSummary.Total = (credit?.CreditSummary?.Total ?? 0) + creditLine.Amount;

				SetCredits(_credits);
			};
			return;
		}
	}
}
