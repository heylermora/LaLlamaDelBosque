using LaLlamaDelBosque.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
    public class CabysController: Controller
    {
		private readonly IJsonRepository _repository;

		public CabysController(IJsonRepository repository)
		{
			_repository = repository;
		}

        [HttpGet]
        public IActionResult GetCabysProducts()
        {
            var cabysJson = _repository.ReadText("ListaCabys");
            return Content(cabysJson, "application/json");
        }
    }
}
