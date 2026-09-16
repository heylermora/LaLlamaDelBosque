using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
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
        private readonly AwardModel _awards;
        private readonly IJsonRepository _repository;
        private readonly TimeProvider _timeProvider;

        public HomeController(IJsonRepository repository, TimeProvider timeProvider)
        {
            _repository = repository;
            _timeProvider = timeProvider;
            _credits = GetCredits();
            _papers = GetPapers();
            _summary = GetSummary();
            _awards = GetAwards();
        }

        public IActionResult Index()
        {
            ViewBag.MissingTodayAwards = !_awards.Awards.Any(a => a.Date.Date == _timeProvider.GetLocalNow().Date);
            return View(_summary);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(string? ErrorMsg, string? ErrorStack)
        {
            if(ErrorMsg == null)
            {
                var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

                if(exceptionHandlerPathFeature?.Error is not null)
                {
                    // Obtener el mensaje de error de la excepción
                    ErrorMsg = exceptionHandlerPathFeature.Error.Message;
                    ErrorStack = exceptionHandlerPathFeature.Error.StackTrace;
                }
            }
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, ErrorMsg = ErrorMsg, ErrorStack = ErrorStack });
        }

        private CreditModel GetCredits()
        {
            var credits = _repository.Read("Credits", new CreditModel());
            return credits;
        }

        private PaperModel GetPapers()
        {
            var papers = _repository.Read("Papers", new PaperModel());
            return papers;
        }

        private AwardModel GetAwards()
        {
            var awards = _repository.Read("Awards", new AwardModel());
            return awards;
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
            summary.Credit.Receivable = _credits.Credits.Sum(c => c.CreditSummary.Total);
            summary.Credit.Received = Math.Abs(_credits.Credits.Sum(c => c.CreditLines.Where(cl => cl.Amount < 0 && cl.CreatedDate.ToShortDateString() == _timeProvider.GetLocalNow().Date.ToShortDateString()).Sum(cl => cl.Amount)));
            return summary;
        }

        private SummaryModel GetLists(SummaryModel summary)
        {
            var inactiveClients = _credits.Credits.Where(c => DateTime.Compare(c.CreditLines?.LastOrDefault()?.CreatedDate ?? _timeProvider.GetLocalNow().DateTime, _timeProvider.GetLocalNow().Date.AddMonths(-2)) < 0).ToList();

            var top = inactiveClients.Count == 0 ? 1 : inactiveClients.Count;

            var majorDebts = _credits.Credits.OrderByDescending(c => c.CreditSummary.Total).Take(top).ToList();
            var minorDebts = _credits.Credits.OrderBy(c => c.CreditSummary.Total).Take(top).ToList();

            inactiveClients.ForEach(ic => summary.Credit.InactiveClients.Add(new SummaryClient() { Name = ic.Client.Name, Amount = ic.CreditSummary.Total }));
            majorDebts.ForEach(md => summary.Credit.MajorDebts.Add(new SummaryClient() { Name = md.Client.Name, Amount = md.CreditSummary.Total }));
            minorDebts.ForEach(md => summary.Credit.MinorDebts.Add(new SummaryClient() { Name = md.Client.Name, Amount = md.CreditSummary.Total }));

            return summary;
        }

        private SummaryModel GetLotteries(SummaryModel summary)
        {
            var todayPapers = _papers.Papers.Where(p => p.CreationDate.ToShortDateString() == _timeProvider.GetLocalNow().Date.ToShortDateString()).ToList();

            var totalPapers = todayPapers.Count;
            var totalAmount = todayPapers.Sum(p => p.Numbers.Sum(n => n.Amount));
            var totalBusted = todayPapers.Sum(p => p.Numbers.Sum(n => n.Busted));
            var total = totalAmount + totalBusted;

            summary.Lottery.Papers = totalPapers;
            summary.Lottery.TotalAmount = totalAmount;
            summary.Lottery.TotalBusted = totalBusted;
            summary.Lottery.Total = total;
            return summary;
        }
    }
}
