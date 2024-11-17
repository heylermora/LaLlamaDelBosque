using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
	[Authorize]

	public class DepartmentController: Controller
	{
		private DepartmentModel _departments;

		public DepartmentController()
		{
			_departments = GetDepartments();
		}

		// GET: DepartmentController
		public ActionResult Index()
		{
			var departments = _departments.Departments;
			return View(departments);
		}

		// GET: DepartmentController/Create
		public ActionResult Create()
		{
			return View();
		}

		// POST: DepartmentController/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Create(IFormCollection collection)
		{
			try
			{
				var department = new Department()
				{
					Id = _departments.Departments.LastOrDefault()?.Id + 1 ?? 1,
					Name = collection["name"],
				};

				_departments.Departments.Add(department);
				SetDepartments(_departments);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		// POST: DepartmentController/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Edit(string id, IFormCollection collection)
		{
			try
			{
				var department = _departments.Departments.First(x => x.Id == int.Parse(id));
				department.Name = collection["name"];
				SetDepartments(_departments);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		// POST: DepartmentController/Delete/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Delete(string id)
		{
			try
			{
				var department = _departments.Departments.First(x => x.Id == int.Parse(id));
				_departments.Departments.Remove(department);
				SetDepartments(_departments);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		private DepartmentModel GetDepartments()
		{
			var departments = JsonFile.Read("Department", new DepartmentModel());
			return departments;
		}

		private void SetDepartments(DepartmentModel department)
		{
			JsonFile.Write("Department", department);
		}
	}
}
