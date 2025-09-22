using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Services;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NuGet.Packaging;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;

namespace LaLlamaDelBosque.Controllers
{
    [Authorize]

    public class AwardController: Controller
    {
        private AwardModel _awards;

        private ScrapingService _scrapingService;

        public AwardController()
        {
            _awards = GetAwards();
            _scrapingService = new ScrapingService();
        }

        // GET: CreditController
        public ActionResult Index()
        {
            var award = _awards.Awards.OrderByDescending(x => x.Date);
            return View(award);
        }

        public ActionResult DetailsPdf(int Id)
        {
            return new ViewAsPdf("_Report", _awards.Awards.FirstOrDefault(a => a.Id == Id))
            {
                PageSize = Size.A4,
                FileName = $"Resumen del {DateTime.Today.ToShortDateString()}.pdf",
                PageMargins = new Margins(10, 20, 10, 20)
            };
        }

        // GET: AwardController/Create
        public async Task<ActionResult> Create()
        {
			try
			{
				var award = _awards?.Awards?.FirstOrDefault(x => x.Date == DateTime.Today);
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
                    award.AwardLines.AddRange(awardLines);
                }
                SetAwards(_awards);
                return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
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
                if(award is not null && double.Parse(collection["AwardLine.amount"]) >= 0)
                {
                    var awardLine = new AwardLine()
                    {
                        Order = award?.AwardLines.LastOrDefault()?.Order + 1 ?? 1,
                        Description = collection["AwardLine.description"],
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

        private AwardModel GetAwards()
        {
            var award = JsonFile.Read("Awards", new AwardModel());
            return award;
        }

        private void SetAwards(AwardModel? awards)
        {
            if(awards != null)
                JsonFile.Write("Awards", awards);
        }
    }
}