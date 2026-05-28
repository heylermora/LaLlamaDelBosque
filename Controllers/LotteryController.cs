using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Controllers
{
	public class LotteryController: Controller
	{
		private static readonly Regex NumberTokenPattern = new(@"^\d{1,2}$", RegexOptions.Compiled);
		private static readonly Regex CompactNumberPattern = new(@"^\d{3,}$", RegexOptions.Compiled);
		private static readonly Regex NumberSplitPattern = new(@"[+,\s]+", RegexOptions.Compiled);

		private List<Lottery> _lotteries;
		private readonly List<Paper> _papers;
		private readonly List<Credit> _credits;
		private readonly List<Award> _awards;
		public LotteryController()
		{
			_lotteries = GetLotteries();
			_papers = GetPapers();
			_credits = GetCredits();
			_awards = GetAwards();
		}

		// GET: LotteryController
		public ActionResult Index(int? id, string lottery, DateTime? fromDate, DateTime? toDate)
		{
			try
			{
				var searchModel = new LotterySearchModel()
				{
					Id = id,
					Lottery = lottery,
					FromDate = fromDate ?? DateTime.Now,
					ToDate = toDate ?? DateTime.Now
				};

				_lotteries = _lotteries.OrderBy(l => l.Hour).ToList();
				var papers = _papers.Where(p =>
					((id != null && lottery == null && p.Id == id) ||
					(id == null && lottery != null && p.Lottery == lottery) ||
					(id != null && lottery != null && p.Id == id && p.Lottery == lottery) ||
					(lottery == "TODOS")) &&
					(p.DrawDate.Date >= searchModel.FromDate.Value.Date && p.DrawDate.Date <= searchModel.ToDate.Value.Date)
				).ToList();

				if((papers.All(p => p.DrawDate.Date == searchModel.FromDate.Value.Date &&
									searchModel.FromDate.Value.Date == searchModel.ToDate.Value.Date)
									&& !string.IsNullOrEmpty(lottery) && lottery != "TODOS" && papers.Count > 0) || papers.Count == 1)
				{
					var number = _awards.FirstOrDefault(a => a.Date.Date == searchModel.FromDate)?.AwardLines.FirstOrDefault(l => l.Description == lottery)?.Number;
					ViewBag.Number = number;
				}

				ViewData["Names"] = _lotteries;
				ViewData["LotterySearchModel"] = searchModel;

				return View(papers);
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
			}
		}

		// GET: LotteryController/Create
		public ActionResult Create(string? dateString, string? selectedLotteries, int? clientId, bool cc = false)
		{
			// Limpiar TempData si cc es falso
			if(!cc)
				TempData.Put<Paper>("Paper", null);

			// Recuperar papelito o crear uno nuevo
			var paper = TempData.Get<Paper>("Paper") ?? new Paper { CreationDate = DateTime.Now };

			// Parsear la fecha o usar la de creación
			var date = !string.IsNullOrWhiteSpace(dateString) && DateTime.TryParse(dateString, out var parsedDate)
				? parsedDate
				: paper.CreationDate;

			// Actualizar sorteos disponibles en esa fecha
			UpdateLotteries(date);

			// Filtrar sorteos válidos desde el string recibido
			var selectedList = (selectedLotteries ?? "")
				.Split(",", StringSplitOptions.RemoveEmptyEntries)
				.Select(name => name.Trim())
				.Where(name => _lotteries.Any(l => l.Name == name))
				.Distinct()
				.ToList();

			// Actualizar el modelo con los sorteos válidos
			paper.SelectedLotteries = selectedList;

			// Actualizar datos del papelito (cliente, loterías, fecha)
			UpdatePaper(paper, string.Join(", ", selectedList), clientId, date);

            // Preparar datos para la vista
            var lotteries = (paper.Lottery ?? "")
				.Split(',', StringSplitOptions.RemoveEmptyEntries)
				.Select(x => x.Trim()).ToList();

            ViewData["Names"] = _lotteries;
			ViewData["Clients"] = _credits.Select(c => c.Client).ToList();
            ViewData["busted"] = _lotteries
                .Any(l => lotteries.Contains(l.Name, StringComparer.OrdinalIgnoreCase) && (l.Busted ?? false));

			// Guardar temporalmente el estado del papelito
			TempData.Put("Paper", paper);

			return View(paper);
		}


		// POST: LotteryController/Save
		[HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Save(string? selectedLotteries, DateTime? drawDate, int? clientId, string? numbersDraftJson)
		{
			try
			{
				var paper = TempData.Get<Paper>("Paper") ?? new Paper();
				var selectedNames = new List<string>();

				var selectedFromForm = Request.Form["SelectedLotteries"]
					.Select(x => x?.Trim())
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.Select(x => x!)
					.ToList();

				if(selectedFromForm.Any())
				{
					selectedNames = selectedFromForm
						.Distinct()
						.ToList();
				}
				else if(!string.IsNullOrWhiteSpace(selectedLotteries))
				{
					selectedNames = selectedLotteries
						.Split(",", StringSplitOptions.RemoveEmptyEntries)
						.Select(x => x.Trim())
						.Where(x => !string.IsNullOrWhiteSpace(x))
						.Distinct()
						.ToList();
				}
				if(selectedNames.Any())
				{
					paper.SelectedLotteries = selectedNames;
					paper.Lottery = string.Join(", ", selectedNames);
				}
				if(drawDate.HasValue) paper.DrawDate = drawDate.Value;
				if(clientId.HasValue) paper.ClientId = clientId;
				if(!string.IsNullOrWhiteSpace(numbersDraftJson))
				{
					try
					{
						var clientNumbers = JsonSerializer.Deserialize<List<Number>>(numbersDraftJson, new JsonSerializerOptions
						{
							PropertyNameCaseInsensitive = true
						});
						if(clientNumbers is not null)
						{
							paper.Numbers = clientNumbers
								.Where(n => !string.IsNullOrWhiteSpace(n.Value))
								.Select((n, i) => new Number
								{
									Id = i + 1,
									Value = n.Value?.Trim(),
									Amount = n.Amount,
									Busted = n.Busted
								}).ToList();
						}
					}
					catch { }
				}

				if(!selectedNames.Any())
				{
					TempData["ErrorMessage"] = "Debe seleccionar al menos un sorteo antes de crear el papelito.";
					TempData.Put("Paper", paper);
					return RedirectToAction(nameof(Create), new { cc = true, selectedLotteries = string.Join(",", selectedNames), dateString = drawDate?.ToString("yyyy-MM-dd"), clientId });
				}

				if(!drawDate.HasValue)
				{
					TempData["ErrorMessage"] = "La fecha de sorteo es obligatoria.";
					TempData.Put("Paper", paper);
					return RedirectToAction(nameof(Create), new { cc = true, selectedLotteries = string.Join(",", selectedNames), clientId });
				}

				UpdateLotteries(drawDate.Value);
				var availableLotteryNames = _lotteries.Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
				var unavailableLotteries = selectedNames
					.Where(name => !availableLotteryNames.Contains(name))
					.ToList();

				if(unavailableLotteries.Any())
				{
					TempData["ErrorMessage"] = $"Estos sorteos ya no están disponibles para la fecha seleccionada: {string.Join(", ", unavailableLotteries)}. Revise la selección antes de crear el papelito.";
					paper.SelectedLotteries = selectedNames.Except(unavailableLotteries, StringComparer.OrdinalIgnoreCase).ToList();
					paper.Lottery = string.Join(", ", paper.SelectedLotteries);
					TempData.Put("Paper", paper);
					return RedirectToAction(nameof(Create), new { cc = true, selectedLotteries = string.Join(",", paper.SelectedLotteries), dateString = drawDate?.ToString("yyyy-MM-dd"), clientId });
				}

				var validNumbers = paper.Numbers
					.Where(n => !string.IsNullOrWhiteSpace(n.Value))
					.Where(n => n.Amount > 0 || n.Busted > 0)
					.ToList();

				if(!validNumbers.Any())
				{
					TempData["ErrorMessage"] = "Debe agregar al menos una línea de números con monto válido.";
					TempData.Put("Paper", paper);
					return RedirectToAction(nameof(Create), new { cc = true, selectedLotteries = string.Join(",", selectedNames), dateString = drawDate?.ToString("yyyy-MM-dd"), clientId });
				}

				if(validNumbers.Any(n => n.Busted > n.Amount))
				{
					TempData["ErrorMessage"] = "El reventado no puede ser mayor que el monto en ninguna línea.";
					TempData.Put("Paper", paper);
					return RedirectToAction(nameof(Create), new { cc = true, selectedLotteries = string.Join(",", selectedNames), dateString = drawDate?.ToString("yyyy-MM-dd"), clientId });
				}

				paper.Numbers = validNumbers;
				var ids = new List<int>();
				if(paper != null)
				{
					var lotteryNames = (paper.SelectedLotteries?.Any() ?? false)
						? paper.SelectedLotteries.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList()
						: (paper.Lottery ?? "").Split(",", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
					foreach(var name in lotteryNames)
					{
						var lottery = _lotteries.FirstOrDefault(l => l.Name == name);
						if(lottery == null) continue;

						var id = _papers.Any() ? _papers.Max(p => p.Id) + 1 : 1;
						ids.Add(id);
						var newPaper = new Paper
						{
							Id = id,
							Numbers = paper.Numbers.Select(n => new Number
							{
								Id = n.Id,
								Amount = n.Amount,
								Busted = n.Busted,
								Value = n.Value
							}).ToList(),
							Lottery = name,
							ClientId = paper.ClientId,
							DrawDate = new DateTime(
								paper.DrawDate.Year,
								paper.DrawDate.Month,
								paper.DrawDate.Day,
								lottery.Hour.Hours,
								lottery.Hour.Minutes,
								lottery.Hour.Seconds
							),
							CreationDate = DateTime.Now
						};

						_papers.Add(newPaper);

						if(newPaper.ClientId != null && newPaper.ClientId != -1)
						{
							var amount = newPaper.Numbers.Sum(p => p.Amount) + newPaper.Numbers.Sum(p => p.Busted);
							Add((int)newPaper.ClientId, name, amount);
						}
					}
					SetPapers(_papers);
				}
				TempData.Put<Paper>("Paper", null);
				return RedirectToAction(nameof(Print), new { ids = string.Join(",", ids) });
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
			}
		}

		public ActionResult Print(string? ids = null, int? id = null)
		{
			var selectedIds = new List<int>();
			if(!string.IsNullOrWhiteSpace(ids))
			{
				selectedIds = ids
					.Split(",", StringSplitOptions.RemoveEmptyEntries)
					.Select(x => int.TryParse(x.Trim(), out var parsed) ? parsed : (int?)null)
					.Where(x => x.HasValue)
					.Select(x => x!.Value)
					.Distinct()
					.ToList();
			}
			else if(id.HasValue)
			{
				selectedIds.Add(id.Value);
			}

			if(selectedIds == null || !selectedIds.Any()) return BadRequest("No se especificaron IDs válidos.");

			var papers = _papers.Where(p => selectedIds.Contains(p.Id)).ToList();
			if(!papers.Any()) return NotFound();

			var paper = papers.First();
			paper.Lottery = string.Join(", ", papers.Select(p => p.Lottery).Distinct());

			ViewData["Date"] = DateTime.Now.ToShortDateString();
			ViewData["Client"] = _credits.Select(c => c.Client).FirstOrDefault(c => c.Id == paper.ClientId)?.Name;
			ViewData["Cant"] = papers.Count;
			ViewData["Ids"] = string.Join(", ", papers.Select(p => "#" + p.Id));

			return View(paper);
		}

		// POST: LotteryController/Copy/5
		[HttpPost]
		[ValidateAntiForgeryToken]
        public ActionResult Copy(int id)
        {
            try
            {
                var sourcePaper = _papers.FirstOrDefault(p => p.Id == id);

                if (sourcePaper == null)
                {
                    TempData["ErrorMessage"] = $"No existe el papelito #{id}.";
                    return RedirectToAction(nameof(Create), new { cc = true });
                }

                var paper = TempData.Get<Paper>("Paper") ?? new Paper { CreationDate = DateTime.Now };

                paper.Numbers = sourcePaper.Numbers
                    .Select(n => new Number
                    {
                        Id = n.Id,
                        Value = n.Value,
                        Amount = n.Amount,
                        Busted = n.Busted
                    })
                    .ToList();

                TempData.Put("Paper", paper);
                TempData["ClearLotteryDraft"] = true;

                return RedirectToAction(nameof(Create), new { cc = true });
            }
            catch (Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
            }
        }

        // POST: CreditController/Delete/5
        [HttpPost]
		[ValidateAntiForgeryToken]
		public ActionResult Delete(int id, string lottery, string fromDate, string toDate)
		{
			try
			{
				var paper = _papers.First(x => x.Id == id);
				_papers.Remove(paper);
				SetPapers(_papers);
				TempData["SuccessMessage"] = $"Papelito #{id} eliminado correctamente.";
				DateTime? parsedFromDate = DateTime.TryParse(fromDate, out var tempFromDate) ? tempFromDate : null;
				DateTime? parsedToDate = DateTime.TryParse(toDate, out var tempToDate) ? tempToDate : null;

				return RedirectToAction(nameof(Index), new { lottery, fromDate = parsedFromDate, toDate = parsedToDate });
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
			}
		}

		#region Lottery Line
		// GET: LotteryController/Add/
		public ActionResult Add(IFormCollection collection, string? selectedLotteries)
		{
			try
			{
				double amount;
				double busted;

				var amountParsed = double.TryParse(collection["amount"], out amount);
				var bustedParsed = double.TryParse(collection["busted"], out busted);

				var numberList = ParseNumberTokens(collection["value"].ToString());
				if(!numberList.Any())
				{
					return RedirectToAction(nameof(Create), new { cc = true });
				}

				var paper = TempData.Get<Paper>("Paper") ?? new Paper();
				var selectedNames = (selectedLotteries ?? string.Empty)
					.Split(",", StringSplitOptions.RemoveEmptyEntries)
					.Select(x => x.Trim())
					.Where(x => !string.IsNullOrWhiteSpace(x))
					.Distinct()
					.ToList();
				if(selectedNames.Any())
				{
					paper.SelectedLotteries = selectedNames;
					paper.Lottery = string.Join(", ", selectedNames);
				}
				var count = paper.Numbers.Max(p => p.Id) ?? 0;

				foreach(var number in numberList)
				{
					var value = number;
					var numbers = paper.Numbers.Find(x => x.Value == value);
					if(numbers != null)
					{
						numbers.Amount += amount;
						numbers.Busted += busted;
					}
					else
					{
						paper.Numbers.Add(new Number()
						{
							Id = ++count,
							Amount = amount,
							Busted = busted,
							Value = number
						});
					}
				}
				paper.Numbers = paper.Numbers.OrderBy(x => x.Value).ToList();
				TempData.Put("Paper", paper);
				return RedirectToAction(nameof(Create), new { selectedLotteries = string.Join(",", paper.SelectedLotteries), cc = true });
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
			}
		}

		private static List<string> ParseNumberTokens(string rawValue)
		{
			return NumberSplitPattern.Split(rawValue ?? string.Empty)
				.Select(number => number.Trim())
				.Where(number => !string.IsNullOrWhiteSpace(number))
				.SelectMany(NormalizeNumberToken)
				.Where(number => NumberTokenPattern.IsMatch(number))
				.ToList();
		}

		private static IEnumerable<string> NormalizeNumberToken(string token)
		{
			if(token.Length == 1)
			{
				return new[] { token.PadLeft(2, '0') };
			}

			if(CompactNumberPattern.IsMatch(token) && token.Length % 2 == 0)
			{
				return Enumerable.Range(0, token.Length / 2)
					.Select(index => token.Substring(index * 2, 2));
			}

			return new[] { token };
		}

		// GET: LotteryController/Clear/
		public ActionResult Clear()
		{
			try
			{
				TempData.Put<Paper>("Paper", null);
				return RedirectToAction(nameof(Create));
			}
			catch(Exception ex)
			{
				return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
			}
		}

		#endregion

        [HttpGet]
        public IActionResult GetAvailableLotteries(DateTime drawDate)
        {
            UpdateLotteries(drawDate);

            var lotteries = _lotteries.Select(l => new
            {
                name = l.Name,
                order = l.Order,
                busted = l.Busted ?? false
            });

            return Json(lotteries);
        }

        private static List<Lottery> GetLotteries()
		{
			var lotteries = JsonFile.Read("Lotteries", new LotteryModel());
			return lotteries.Lotteries;
		}

		private static List<Paper> GetPapers()
		{
			var papers = JsonFile.Read("Papers", new PaperModel());
			return papers.Papers;
		}

		private static void SetPapers(List<Paper> papers)
		{
			var paperModel = new PaperModel()
			{
				Papers = papers
			};
			JsonFile.Write("Papers", paperModel);
		}

		private static List<Credit> GetCredits()
		{
			var credits = JsonFile.Read("Credits", new CreditModel());
			return credits.Credits.ToList();
		}

		private static void SetCredits(List<Credit> credits)
		{
			var creditModel = new CreditModel()
			{
				Credits = credits
			};
			JsonFile.Write("Credits", creditModel);
		}

		private List<Award> GetAwards()
		{
			var award = JsonFile.Read("Awards", new AwardModel());
			return award.Awards.ToList();
		}

		private void Add(int id, string lottery, double amount)
		{
			var credit = _credits.FirstOrDefault(x => x.Client.Id == id);
			if(credit is not null)
			{
				var creditLine = new CreditLine()
				{
					Id = credit?.CreditLines.LastOrDefault()?.Id + 1 ?? 1,
					CreatedDate = DateTime.Now,
					Description = "SORTEO: " + lottery,
					Amount = amount
				};

				if(credit != null && credit.CreditSummary != null)
				{
					credit.CreditLines.Add(creditLine);
					credit.CreditSummary.Total = credit.CreditSummary.Total + creditLine.Amount;
				}

				SetCredits(_credits);
			};
			return;
		}

		private void UpdateLotteries(DateTime date)
		{
			_lotteries = _lotteries
				.Where(l => (date.ToShortDateString() == DateTime.Today.ToShortDateString() ?
							 l.Hour.Add(TimeSpan.FromMinutes(-10)) > DateTime.Now.TimeOfDay : true) &&
							(l.Days?.Contains(date.DayOfWeek.ToString()) ?? true))
				.OrderBy(l => l.Hour)
				.ToList();
		}

		private void UpdatePaper(Paper paper, string selectedLotteries, int? clientId, DateTime date)
		{
			paper.Id = _papers.LastOrDefault()?.Id + 1 ?? 1;
			paper.Lottery = selectedLotteries ?? paper.Lottery;
			paper.ClientId = clientId ?? paper.ClientId;
			paper.DrawDate = date;
			paper.CreationDate = date;
		}
	}
}
