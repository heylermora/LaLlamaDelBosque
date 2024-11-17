using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
	[Authorize]

	public class SupplierController: Controller
	{
		private SupplierModel _suppliers;

		public SupplierController()
		{
			_suppliers = GetSuppliers();
		}

		// GET: SupplierController
		public ActionResult Index()
		{
			var suppliers = _suppliers.Suppliers;
			return View(suppliers);
		}

		// GET: SupplierController/Create
		public ActionResult Create()
		{
			return View();
		}

		// POST: SupplierController/Create
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Create(IFormCollection collection)
		{
			try
			{
				var supplier = new Supplier()
				{
					Id = _suppliers.Suppliers.LastOrDefault()?.Id + 1 ?? 1,
					Name = collection["name"],
				};

				_suppliers.Suppliers.Add(supplier);
				SetSuppliers(_suppliers);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		// POST: SupplierController/Edit/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Edit(string id, IFormCollection collection)
		{
			try
			{
				var supplier = _suppliers.Suppliers.First(x => x.Id == int.Parse(id));
				supplier.Name = collection["name"];
				SetSuppliers(_suppliers);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		// POST: SupplierController/Delete/5
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Delete(string id)
		{
			try
			{
				var supplier = _suppliers.Suppliers.First(x => x.Id == int.Parse(id));
				_suppliers.Suppliers.Remove(supplier);
				SetSuppliers(_suppliers);
				return RedirectToAction(nameof(Index));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
			}
		}

		private SupplierModel GetSuppliers()
		{
			var suppliers = JsonFile.Read("Supplier", new SupplierModel());
			return suppliers;
		}

		private void SetSuppliers(SupplierModel supplier)
		{
			JsonFile.Write("Supplier", supplier);
		}
	}
}
