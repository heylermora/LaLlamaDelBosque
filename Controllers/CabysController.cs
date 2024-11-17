using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
    public class CabysController: Controller
    {
        [HttpGet]
        public IActionResult GetCabysProducts()
        {
            var cabysJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "ListaCabys.json");
            var cabysJson = System.IO.File.ReadAllText(cabysJsonPath);
            return Content(cabysJson, "application/json");
        }
    }
}
