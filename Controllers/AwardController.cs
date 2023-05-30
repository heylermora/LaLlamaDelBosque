using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;

namespace LaLlamaDelBosque.Controllers
{
	[Authorize]

	public class AwardController: Controller
	{
		private AwardModel _awards;

		private AwardSetting _setting;

		public AwardController()
		{
			_awards = GetAwards();
			_setting = GetSetting();
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
			return View();
		}

		// POST: AwardController/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Create(IFormCollection collection)
		{
			try
			{
				var award = new Award()
				{
					Id = _awards.Awards.LastOrDefault()?.Id + 1 ?? 1,
					Date = DateTime.Parse(collection["date"]),
					AwardLines = new List<AwardLines>()
					{
						new AwardLines()
						{
							Order = 1,
							Description = "La Primera"
						},
						new AwardLines()
						{
							Order = 2,
							Description = "Nica 11 A.M."
						},
						new AwardLines()
						{
							Order = 3,
							Description = "Tica Día"
						},
						new AwardLines()
						{
							Order = 4,
							Description = "Nica 3 P.M."
						},
						new AwardLines()
						{
							Order = 5,
							Description = "Tica 4 P.M."
						},
						new AwardLines()
						{
							Order = 6,
							Description = "Primera"
						},
						new AwardLines()
						{
							Order = 7,
							Description = "Tica Noche"
						},
						new AwardLines()
						{
							Order = 8,
							Description = "Nica 9 P.M."
						}
					}
				};

				_awards.Awards.Add(award);
				SetAwards(_awards);
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return View();
			}
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
				line.Number = int.Parse(collection["number"]);
				line.Busted = int.Parse(collection["busted"]);
				line.Amount = double.Parse(collection["amount"]);
				line.Award = double.Parse(collection["award"]);

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

		#region AwardSetting

		// GET: AwardController/Setting
		public ActionResult Setting()
		{
			var settings = new AwardSetting();
			return View(settings);
		}

		// GET: AwardController/Add
		public ActionResult Add()
		{
			return View();
		}

		// POST: AwardController/Add
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Add(IFormCollection collection)
		{
			try
			{
				return RedirectToAction(nameof(Setting));
			}
			catch
			{
				return RedirectToAction(nameof(Setting));
			}
		}

		#endregion

		private AwardModel GetAwards()
		{
			var award = JsonFile.Read("Awards", new AwardModel());
			return award;
		}

		private void SetAwards(AwardModel awards)
		{
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