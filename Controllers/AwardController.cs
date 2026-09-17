using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;

namespace LaLlamaDelBosque.Controllers
{
    [Authorize]

    public class AwardController: Controller
    {
        private readonly AwardModel _awards;
        private readonly IScrapingService _scrapingService;
        private readonly IJsonRepository _repository;
        private readonly TimeProvider _timeProvider;

        public AwardController(IScrapingService scrapingService, IJsonRepository repository, TimeProvider timeProvider)
        {
            _scrapingService = scrapingService;
            _repository = repository;
            _timeProvider = timeProvider;
            _awards = _repository.Read<AwardModel>("Awards");
        }

        // GET: CreditController
        public ActionResult Index()
        {
            var award = _awards.Awards.OrderByDescending(x => x.Date).ToList();
            var lotteries = GetLotteries();
            ViewBag.AvailableAwardLotteries = award.ToDictionary(x => x.Id, x => GetAvailableAwardLotteries(x, lotteries));
            ViewBag.ScrapingSources = _repository.Read<ScrapingLotteryModel>("ScrapingLotteries").Sources
                .Where(x => x.ShowToUser)
                .GroupBy(x => x.LotteryType)
                .ToDictionary(x => x.Key, x => x.ToList());
            return View(award);
        }

        public ActionResult DetailsPdf(int Id)
        {
            return new ViewAsPdf("_Report", _awards.Awards.FirstOrDefault(a => a.Id == Id))
            {
                PageSize = Size.A4,
                FileName = $"Resumen del {_timeProvider.GetLocalNow().Date.ToShortDateString()}.pdf",
                PageMargins = new Margins(10, 20, 10, 20)
            };
        }

        // GET: AwardController/Create
        public async Task<ActionResult> Create()
        {
			try
			{
				var award = _awards?.Awards?.FirstOrDefault(x => x.Date == _timeProvider.GetLocalNow().Date);
                if(award == null)
                {
                    award = await _scrapingService.Add();
                    award.Id = _awards?.Awards?.LastOrDefault()?.Id + 1 ?? 0;
                    _awards?.Awards.Add(award);
                }
                else
                {
					award.AwardLines.Clear();
					var awardLines = (await _scrapingService.Add()).AwardLines;
					foreach(var awardLine in awardLines)
						award.AwardLines.Add(awardLine);
                }
				SetAwards(_awards);
				if(_scrapingService.Warnings.Count > 0)
				{
					TempData["WarningMessage"] = $"Se registraron {award.AwardLines.Count} resultados. No se pudieron consultar algunas fuentes: {string.Join(" ", _scrapingService.Warnings)} Los demás resultados sí fueron procesados.";
				}
				else
				{
					TempData["SuccessMessage"] = $"Actualización completada. Se registraron {award.AwardLines.Count} resultados encontrados en las fuentes.";
				}
                return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				TempData["ErrorMessage"] = $"No se pudo completar la actualización automática: {ex.Message}. Revise las fuentes oficiales y vuelva a intentarlo.";
				return RedirectToAction(nameof(Index));
			}
		}

        // POST: AwardController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int awardId, IFormCollection collection)
        {
            try
            {
                var award = _awards.Awards.First(x => x.Id == awardId);
                var line = award.AwardLines.First(l => l.Order == int.Parse(collection["AwardLine.Order"]));

                line.Description = collection["AwardLine.description"];
                line.Number = collection["AwardLine.number"];
                line.Busted = int.Parse(collection["AwardLine.busted"]);
                line.Amount = double.Parse(collection["AwardLine.amount"]);
                line.TimesBusted = int.Parse(collection["AwardLine.timesbusted"]);
                line.TimesAmount = double.Parse(collection["AwardLine.timesamount"]);
                line.Award = line.Amount * line.TimesAmount + line.Busted * line.TimesBusted;
                SetAwards(_awards);
                return RedirectToAction(nameof(Index));
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
            }
        }

        // POST: AwardController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id)
        {
            try
            {
                var award = _awards.Awards.First(x => x.Id == int.Parse(id));
                _awards.Awards.Remove(award);
                SetAwards(_awards);
                return RedirectToAction(nameof(Index));
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
            }
        }

        // POST: AwardController/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteAll()
        {
            try
            {
                _awards.Awards.Clear();
                SetAwards(_awards);
                return RedirectToAction(nameof(Index));
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
            }
        }

        // POST: AwardController/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Add(int awardId, IFormCollection collection)
        {
            try
            {
                var award = _awards.Awards.FirstOrDefault(x => x.Id == awardId);
                var selectedDescription = collection["AwardLine.Description"].FirstOrDefault() ?? collection["AwardLine.description"].FirstOrDefault() ?? string.Empty;
                var selectedLottery = GetLotteries().FirstOrDefault(x => x.Name.Equals(selectedDescription, StringComparison.OrdinalIgnoreCase));

                if(award is not null && selectedLottery is not null && !award.AwardLines.Any(x => x.Description.Equals(selectedLottery.Name, StringComparison.OrdinalIgnoreCase)) && double.Parse(collection["AwardLine.amount"]) >= 0)
                {
                    var awardLine = new AwardLine()
                    {
                        Order = selectedLottery.Order,
                        Description = selectedLottery.Name,
                        Number = collection["AwardLine.number"],
                        Amount = double.Parse(collection["AwardLine.amount"]),
                        Busted = double.Parse(collection["AwardLine.busted"]),
                        TimesBusted = double.Parse(collection["AwardLine.timesbusted"]),
                        TimesAmount = double.Parse(collection["AwardLine.timesamount"]),
                        Award = double.Parse(collection["AwardLine.amount"]) * double.Parse(collection["AwardLine.timesamount"]) + double.Parse(collection["AwardLine.busted"]) * double.Parse(collection["AwardLine.timesbusted"]),
                    };
                    award?.AwardLines.Add(awardLine);

                    SetAwards(_awards);
                }
                return RedirectToAction(nameof(Index));
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
            }
        }

        private List<Lottery> GetLotteries()
        {
            var lotteries = _repository.Read<LotteryModel>("Lotteries");
            return lotteries.Lotteries;
        }

        private List<Lottery> GetAvailableAwardLotteries(Award award, List<Lottery> lotteries)
        {
            var awardDate = award.Date.Date;
            var existingDescriptions = award.AwardLines
                .Select(x => x.Description)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return lotteries
                .Where(x => IsLotteryDrawPassed(x, awardDate) && !existingDescriptions.Contains(x.Name))
                .OrderBy(x => x.Hour)
                .ToList();
        }

        private bool IsLotteryDrawPassed(Lottery lottery, DateTime awardDate)
        {
            if(!(lottery.Days?.Contains(awardDate.DayOfWeek.ToString()) ?? true))
                return false;

            var now = _timeProvider.GetLocalNow();
            if(awardDate.Date < now.Date)
                return true;

            if(awardDate.Date > now.Date)
                return false;

            return lottery.Hour <= now.TimeOfDay;
        }

        private void SetAwards(AwardModel? awards)
        {
            if(awards != null)
                _repository.Write("Awards", awards);
        }
    }
}
