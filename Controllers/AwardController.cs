using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using LaLlamaDelBosque.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using NuGet.Packaging;

namespace LaLlamaDelBosque.Controllers
{
	[Authorize]

	public class AwardController: Controller
	{
		private AwardModel _awards;
		private AwardSetting _setting;

		private ScrapingService _scrapingService;

		public AwardController()
		{
			_awards = GetAwards();
			_setting = GetSetting();
			_scrapingService = new ScrapingService();
		}

		// GET: CreditController
		public ActionResult Index()
		{
			var award = _awards.Awards.OrderByDescending( x => x.Date);
			return View(award);
		}

		public ActionResult DetailsPdf(int Id)
		{
			return new ViewAsPdf("_Report", _awards.Awards.FirstOrDefault( a => a.Id == Id))
			{
				PageSize = Size.A4,
				FileName = $"Resumen del {DateTime.Today.ToShortDateString()}.pdf",
				PageMargins = new Margins(10, 20, 10, 20)
			};
		}

		// GET: AwardController/Create
		public ActionResult Create()
		{
			var award = _awards?.Awards?.FirstOrDefault(x => x.Date == DateTime.Today);
			if(award == null)
			{
				award = _scrapingService.Add(null);
				award.Id = _awards?.Awards?.LastOrDefault()?.Id + 1 ?? 0;
				_awards?.Awards.Add(award);
			}
			else
			{
				var descriptions = award.AwardLines.Select(a => a.Description).ToList();
				var awardLines = _scrapingService.Add(descriptions).AwardLines;
				award.AwardLines.AddRange(awardLines);
			}
			SetAwards(_awards);
			return RedirectToAction(nameof(Index));
		}

		// GET: AwardController/Edit/5
		public ActionResult Edit(int awardId, int lineId)
		{
			TempData["Id"] = awardId;
			TempData["LineId"] = lineId;
			TempData["Method"] = "Edit";
			return RedirectToAction(nameof(Index));
		}

		// POST: AwardController/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Edit(int awardId, IFormCollection collection)
		{
			try
			{
				TempData["Id"] = awardId;

				var award = _awards.Awards.First(x => x.Id == awardId);
				var line = award.AwardLines.First(l => l.Order == int.Parse(collection["Order"]));

				line.Description = collection["description"];
				line.Number = collection["number"];
				line.Busted = int.Parse(collection["busted"]);
				line.Amount = double.Parse(collection["amount"]);
				line.TimesBusted = int.Parse(collection["timesbusted"]);
				line.TimesAmount = double.Parse(collection["timesamount"]);
				line.Award = line.Amount * line.TimesAmount + line.Busted * line.TimesBusted;
				SetAwards(_awards);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				Console.WriteLine(ex);
				return RedirectToAction(nameof(Index));
			}
		}

		// GET: AwardController/Delete/5
		public ActionResult Delete(int id)
		{
			TempData["Id"] = id;
			TempData["Method"] = "Delete";
			return RedirectToAction(nameof(Index));
		}

		// POST: AwardController/Delete/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Delete(string id)
		{
			try
			{
				var credit = _awards.Awards.First(x => x.Id == int.Parse(id));
				_awards.Awards.Remove(credit);
				SetAwards(_awards);
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return RedirectToAction(nameof(Index));
			}
		}

		// GET: AwardController/Add
		public ActionResult Add(int id)
		{
			TempData["Id"] = id;
			TempData["Method"] = "Add";
			return RedirectToAction(nameof(Index));
		}

		// POST: AwardController/Add
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Add(int id, IFormCollection collection)
		{
			try
			{
				TempData["Id"] = id;
				var award = _awards.Awards.FirstOrDefault(x => x.Id == id);
				if(award is not null && double.Parse(collection["amount"]) > 0)
				{
					var awardLine = new AwardLine()
					{
						Order = award?.AwardLines.LastOrDefault()?.Order + 1 ?? 1,
						Description = collection["description"],
						Number = collection["number"],
						Amount = double.Parse(collection["amount"]),
						Busted = double.Parse(collection["busted"]),
						TimesBusted = double.Parse(collection["timesbusted"]),
						TimesAmount = double.Parse(collection["timesamount"]),
						Award = double.Parse(collection["amount"]) * double.Parse(collection["timesamount"]) + double.Parse(collection["timesbusted"]) * double.Parse(collection["timesbusted"]),		
					};
					award?.AwardLines.Add(awardLine);

					SetAwards(_awards);
				}
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return RedirectToAction(nameof(Index));
			}
		}

		#region AwardSetting

		// GET: AwardController/Setting
		public ActionResult Setting()
		{
			var settings = new AwardSetting();
			return View(settings);
		}

		#endregion

		private AwardModel GetAwards()
		{
			var award = JsonFile.Read("Awards", new AwardModel());
			return award;
		}

		private void SetAwards(AwardModel? awards)
		{
			if (awards != null)
				JsonFile.Write("Awards", awards);
		}

		private AwardSetting GetSetting()
		{
			var setting = JsonFile.Read("Setting", new AwardSetting());
			return setting;
		}

		private void SetSetting(AwardSetting setting)
		{
			JsonFile.Write("Setting", setting);
		}
	}
}