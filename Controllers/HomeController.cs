using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NuGet.Protocol.Core.Types;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;

namespace LaLlamaDelBosque.Controllers
{
	[Authorize]
	public class HomeController: Controller
	{
		private readonly CreditModel _credits;
		private readonly SummaryModel _summary;

		public HomeController()
		{
			_credits = GetCredits();
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
			var credits = JsonFile.Read<CreditModel>("Credits", new CreditModel());
			return credits;
		}

		private SummaryModel GetSummary()
		{
			SummaryModel summary = new SummaryModel();
			GetClients(summary);
			GetAmounts(summary);
			GetLists(summary);
			return summary;
		}

		private SummaryModel GetClients(SummaryModel summary)
		{
			summary.Clients = _credits.Credits.Count;
			summary.ClientsDescription = summary.Clients == 1 ? "persona" : summary.ClientsDescription;
			return summary;
		}

		private SummaryModel GetAmounts(SummaryModel summary)
		{
			summary.Receivable = _credits.Credits.Sum(c => c.CreditSummary.Total).ToString("N", CultureInfo.InvariantCulture);
			summary.Received = Math.Abs(_credits.Credits.Sum(c => c.CreditLines.Where(cl => cl.Amount<0 && cl.CreatedDate.ToShortDateString() == DateTime.Today.ToShortDateString()).Sum(cl => cl.Amount))).ToString("N", CultureInfo.InvariantCulture);		
			return summary;
		}

		private SummaryModel GetLists(SummaryModel summary)
		{
			var inactiveClients = _credits.Credits.Where(c => DateTime.Compare(c.CreditLines.Last().CreatedDate, DateTime.Today.AddMonths(-2)) < 0).ToList();

			var top = inactiveClients.Count == 0 ? 1 : inactiveClients.Count;

			var majorDebts = _credits.Credits.OrderBy(c => c.CreditSummary.Total).Take(top).ToList();
			var minorDebts = _credits.Credits.OrderByDescending(c => c.CreditSummary.Total).Take(top).ToList();


			inactiveClients.ForEach(ic => summary.InactiveClients.Add(new SummaryClient() { Name = ic.Client.Name, Amount = ic.CreditSummary.Total.ToString("N", CultureInfo.InvariantCulture) }));
			majorDebts.ForEach(md => summary.MajorDebts.Add(new SummaryClient() { Name = md.Client.Name, Amount = md.CreditSummary.Total.ToString("N", CultureInfo.InvariantCulture) }));
			minorDebts.ForEach(md => summary.MinorDebts.Add(new SummaryClient() { Name = md.Client.Name, Amount = md.CreditSummary.Total.ToString("N", CultureInfo.InvariantCulture) }));


			return summary;
		}
	}
}