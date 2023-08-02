using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Globalization;

namespace LaLlamaDelBosque.Controllers
{
	[Authorize]
	public class HomeController: Controller
	{
		private readonly CreditModel _credits;
		private readonly PaperModel _papers;
		private readonly SummaryModel _summary;

		public HomeController()
		{
			_credits = GetCredits();
			_papers = GetPapers();
			_summary = GetSummary();
		}

		public IActionResult Index()
		{
			var summary = new SummaryModel();
			return View(_summary);
		}

		public IActionResult Privacy()
		{
			return View();
		}

		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error()
		{
			return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
		}

		private CreditModel GetCredits()
		{
			var credits = JsonFile.Read("Credits", new CreditModel());
			return credits;
		}

		private static PaperModel GetPapers()
		{
			var papers = JsonFile.Read("Papers", new PaperModel());
			return papers;
		}

		private SummaryModel GetSummary()
		{
			SummaryModel summary = new SummaryModel();
			GetClients(summary);
			GetAmounts(summary);
			GetLists(summary);
			GetLotteries(summary);
			return summary;
		}

		private SummaryModel GetClients(SummaryModel summary)
		{
			summary.Credit.Clients = _credits.Credits.Count;
			summary.Credit.ClientsDescription = summary.Credit.Clients == 1 ? "persona" : summary.Credit.ClientsDescription;
			return summary;
		}

		private SummaryModel GetAmounts(SummaryModel summary)
		{
			summary.Credit.Receivable = _credits.Credits.Sum(c => c.CreditSummary.Total).ToString("N", CultureInfo.InvariantCulture);
			summary.Credit.Received = Math.Abs(_credits.Credits.Sum(c => c.CreditLines.Where(cl => cl.Amount<0 && cl.CreatedDate.ToShortDateString() == DateTime.Today.ToShortDateString()).Sum(cl => cl.Amount))).ToString("N", CultureInfo.InvariantCulture);		
			return summary;
		}

		private SummaryModel GetLists(SummaryModel summary)
		{
			var inactiveClients = _credits.Credits.Where(c => DateTime.Compare(c.CreditLines?.LastOrDefault()?.CreatedDate ?? DateTime.Now, DateTime.Today.AddMonths(-2)) < 0).ToList();

			var top = inactiveClients.Count == 0 ? 1 : inactiveClients.Count;

			var majorDebts = _credits.Credits.OrderByDescending(c => c.CreditSummary.Total).Take(top).ToList();
			var minorDebts = _credits.Credits.OrderBy(c => c.CreditSummary.Total).Take(top).ToList();


			inactiveClients.ForEach(ic => summary.Credit.InactiveClients.Add(new SummaryClient() { Name = ic.Client.Name, Amount = ic.CreditSummary.Total.ToString("N", CultureInfo.InvariantCulture) }));
			majorDebts.ForEach(md => summary.Credit.MajorDebts.Add(new SummaryClient() { Name = md.Client.Name, Amount = md.CreditSummary.Total.ToString("N", CultureInfo.InvariantCulture) }));
			minorDebts.ForEach(md => summary.Credit.MinorDebts.Add(new SummaryClient() { Name = md.Client.Name, Amount = md.CreditSummary.Total.ToString("N", CultureInfo.InvariantCulture) }));

			return summary;
		}

		private SummaryModel GetLotteries(SummaryModel summary)
		{
			var todayPapers = _papers.Papers.Where(p => p.Date.ToShortDateString() == DateTime.Today.ToShortDateString()).ToList();
			
			var totalPapers = todayPapers.Count;
			var totalAmount = todayPapers.Sum(p => p.Numbers.Sum(n => n.Amount));
			var totalBusted = todayPapers.Sum(p => p.Numbers.Sum(n => n.Busted));
			var total = totalAmount + totalBusted;

			summary.Lottery.Papers = totalPapers;
			summary.Lottery.TotalAmount = totalAmount.ToString("N", CultureInfo.InvariantCulture);
			summary.Lottery.TotalBusted = totalBusted.ToString("N", CultureInfo.InvariantCulture);
			summary.Lottery.Total = total.ToString("N", CultureInfo.InvariantCulture);
			return summary;
		}
	}
}