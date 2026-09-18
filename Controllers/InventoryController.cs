using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
	[Authorize]

	public class InventoryController: Controller
	{
		private InventoryModel _inventory;
		private FamilyModel _families;
		private DepartmentModel _departments;
		private readonly IJsonRepository _repository;

		public InventoryController(IJsonRepository repository)
		{
			_repository = repository;
			_inventory = GetInventory();
			_families = GetFamilies();
			_departments = GetDepartments();
		}

		// GET: InventoryController
		public ActionResult Index()
		{
			var credits = _inventory.Inventory;
			return View(credits);
		}

		// GET: InventoryController/Create
		public ActionResult Create()
		{
			ViewData["Families"] = _families.Families;
			ViewData["Departments"] = _departments.Departments;
			return View();
		}

		private InventoryModel GetInventory()
		{
			var inventory = _repository.Read("Inventory", new InventoryModel());
			return inventory;
		}

		private FamilyModel GetFamilies()
		{
			var families = _repository.Read("Family", new FamilyModel());
			return families;
		}

		private DepartmentModel GetDepartments()
		{
			var departments = _repository.Read("Department", new DepartmentModel());
			return departments;
		}

		private void SetInventory(InventoryModel inventory)
		{
			_repository.Write("Inventory", inventory);
		}
	}
}
