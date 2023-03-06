using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
	[Authorize]
	public class ScheduleController: Controller
	{
		private ScheduleModel _schedules;

		public ScheduleController()
		{
			_schedules = GetSchedules();
		}

		// GET: Schedule
		public ActionResult Index()
		{
			_schedules.Schedules = _schedules.Schedules.OrderBy(s => s.CreatedDate).ToList();
			TempData["Message"] = GetMessage(_schedules.Schedules);
			return View(_schedules.Schedules);
		}

		// GET: Schedule/Turn
		public ActionResult Turn()
		{
			var schedules = _schedules.Schedules.Where(x => 
				!x.IsCompleted && 
				x.CreatedDate.Day == DateTime.Today.Day);

			foreach(var schedule in schedules)
			{
				schedule.FinishDate = DateTime.Now;
			}

			return View(schedules);
		}

		// POST: Schedule/Turn
		public ActionResult Finish(int id)
		{
			var schedule = _schedules.Schedules.First(x => x.Id == id);
			if(schedule.CreatedDate.Hour < schedule.FinishDate.Hour)
			{
				_schedules.Schedules.First(x => x.Id == id).IsCompleted = true;
				_schedules.Schedules.First(x => x.Id == id).FinishDate = DateTime.Now;
				SetSchedules(_schedules);
			}
			return RedirectToAction(nameof(Turn));
		}

		// GET: Schedule/Create
		public ActionResult Create()
		{
			ViewData["Names"] = GetWorkers();
			return View(new Schedule());
		}

		// POST: Schedule/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Create(IFormCollection collection)
		{
			try
			{
				var date = DateTime.Parse(collection["createddate"]);
				var finish = DateTime.Parse(collection["finishdate"]);

				var schedule = new Schedule()
				{ 
					Id = _schedules.Schedules.LastOrDefault()?.Id + 1 ?? 1,
					Name = collection["name"],
					CreatedDate = date,
					FinishDate = new DateTime(date.Year, date.Month, date.Day, finish.Hour, finish.Minute, finish.Second),
				};

				_schedules.Schedules.Add(schedule);
				SetSchedules(_schedules);
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return View();
			}
		}

		// GET: Schedule/Details/5
		public ActionResult Details(int id)
		{
			TempData["Id"] = id;
			TempData["Method"] = "Details";
			return RedirectToAction("Index");
		}


		// GET: Schedule/Edit/5
		public ActionResult Edit(int id)
		{
			TempData["Id"] = id;
			TempData["Method"] = "Edit";
			return RedirectToAction(nameof(Index));
		}

		// POST: Schedule/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Edit(IFormCollection collection)
		{
			try
			{
				var schedule = _schedules.Schedules.First(x => x.Id == int.Parse(collection["id"]));
				schedule.CreatedDate = DateTime.Parse(collection["createddate"]);
				schedule.FinishDate = DateTime.Parse(collection["finishdate"]);
				SetSchedules(_schedules);
				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return RedirectToAction(nameof(Index));
			}
		}

		// GET: Schedule/Delete/5
		public ActionResult Delete(int id)
		{
			TempData["Id"] = id;
			TempData["Method"] = "Delete";
			return RedirectToAction("Index");
		}

		// POST: Schedule/Delete/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Delete(string id)
		{
			try
			{
				var schedule = _schedules.Schedules.First(x => x.Id == int.Parse(id));
				_schedules.Schedules.Remove(schedule);
				SetSchedules(_schedules);

				return RedirectToAction(nameof(Index));
			}
			catch
			{
				return View();
			}
		}

		private List<Worker> GetWorkers()
		{
			var workers = JsonFile.Read<WorkerModel>("Workers", new WorkerModel());
			return workers.Workers;
		}

		private ScheduleModel GetSchedules()
		{
			var schedules = JsonFile.Read<ScheduleModel>("Schedules", new ScheduleModel());
			return schedules;
		}

		private void SetSchedules(ScheduleModel schedules)
		{
			JsonFile.Write<ScheduleModel>("Schedules", schedules);
		}

		private string GetMessage(IList<Schedule> schedules)
		{
			var text = "";
			foreach(var schedule in schedules)
			{
				text += "%0A";
				text += $"✅ {schedule.Name.ToUpper()}: {schedule.CreatedDate.ToShortDateString()} de {schedule.CreatedDate.ToShortTimeString()} a {schedule.FinishDate.ToShortTimeString()} ";
			}
			return text;
		}
	}
}
