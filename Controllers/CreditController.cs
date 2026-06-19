using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Rotativa.AspNetCore;
using Rotativa.AspNetCore.Options;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Controllers
{
    [Authorize]

    public class CreditController: Controller
    {
        private static readonly Regex CostaRicaPhonePattern = new(@"^506[0-9]{8}$", RegexOptions.Compiled);
        private static readonly Regex IdentificationPattern = new(@"^[0-9]{9,12}$", RegexOptions.Compiled);
        private CreditModel _credits;

        public CreditController()
        {
            _credits = GetCredits();

        }

        // GET: CreditController
        public ActionResult Index(string searchString, int clientId, int currentPage = 1)
        {
            var credits = _credits.Credits;

			foreach(var credit in credits)
			{
				credit.CreditSummary.CalculateStatus(credit.Client.Limit);
			}

			if(!string.IsNullOrEmpty(searchString))
            {
                if(searchString == "__limit__")
                {
                    credits = credits.Where(s => s.CreditSummary.Status == 4).ToList();
                }
                else
                {
					credits = credits.Where(s =>
                        s.Client.Name.ToLower().Contains(searchString.ToLower()) ||
                        s.Client.Phone.Contains(searchString) ||
                        (!string.IsNullOrWhiteSpace(s.Client.Identification) && s.Client.Identification.Contains(searchString))
                    ).ToList();
				}
			}
            credits = credits.OrderBy(c => c.Client.Name).ToList();

            int totalItems = credits.Count();
            int totalPages = (int)Math.Ceiling((double)totalItems / 20);
            currentPage = Math.Max(1, Math.Min(currentPage, totalPages));
            int startIndex = (currentPage - 1) * 20;
            int endIndex = Math.Min(startIndex + 20 - 1, totalItems - 1);

            credits = credits.Where((item, index) => index >= startIndex && index <= endIndex).ToList();

            ViewBag.LimitExceededClientName = TempData["LimitExceededClientName"] as string;
            ViewBag.LimitExceededClientTotal = TempData["LimitExceededClientTotal"] as string;
            ViewBag.LimitExceededClientLimit = TempData["LimitExceededClientLimit"] as string;
            ViewBag.ShowFortnightReminder = IsFortnightCollectionWindow(DateTime.Today);

            ViewBag.TotalPages = totalPages;
            ViewBag.CurrentPage = currentPage;
            ViewBag.ClientId = clientId;
            ViewBag.SearchString = searchString;

            return View(credits);
        }

        public ActionResult IndexPdf()
        {
            return new ViewAsPdf("_Report", _credits.Credits)
            {
                PageSize = Size.A4,
                FileName = $"Resumen del {DateTime.Today.ToShortDateString()}.pdf",
                PageMargins = new Margins(10, 20, 10, 20)
            };
        }

        // GET: CreditController/Create
        public ActionResult Create()
        {
            return View(new Client { Phone = "506" });
        }

        // POST: CreditController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                var identification = collection["identification"].ToString().Trim();
                var phone = collection["phone"].ToString().Trim();
                var name = collection["name"].ToString().Trim();
                var address = collection["address"].ToString().Trim();
                var limitValue = collection["limit"].ToString().Trim();
                var orderProduct = collection["orderProduct"].ToString().Trim();
                var orderQuantityValue = collection["orderQuantity"].ToString().Trim();
                var orderUnitPriceValue = collection["orderUnitPrice"].ToString().Trim();
                var orderNotes = collection["orderNotes"].ToString().Trim();
                ViewData["OrderProduct"] = orderProduct;
                ViewData["OrderQuantity"] = orderQuantityValue;
                ViewData["OrderUnitPrice"] = orderUnitPriceValue;
                ViewData["OrderNotes"] = orderNotes;

                var client = new Client
                {
                    Identification = identification,
                    Phone = phone,
                    Name = name,
                    Address = address
                };

                if(!IdentificationPattern.IsMatch(identification))
                {
                    ModelState.AddModelError(nameof(Client.Identification), "La cédula debe tener entre 9 y 12 dígitos.");
                }

                if(!CostaRicaPhonePattern.IsMatch(phone))
                {
                    ModelState.AddModelError(nameof(Client.Phone), "El teléfono debe iniciar con 506 y tener 11 dígitos.");
                }

                if(_credits.Credits.Any(c => !string.IsNullOrWhiteSpace(c.Client.Identification) && c.Client.Identification == identification))
                {
                    ModelState.AddModelError(nameof(Client.Identification), "Ya existe un cliente con esta cédula.");
                }

                if(_credits.Credits.Any(c => c.Client.Phone == phone))
                {
                    ModelState.AddModelError(nameof(Client.Phone), "Ya existe un cliente con este teléfono.");
                }

                if(string.IsNullOrWhiteSpace(name))
                {
                    ModelState.AddModelError(nameof(Client.Name), "El nombre es obligatorio.");
                }

                if(string.IsNullOrWhiteSpace(address))
                {
                    ModelState.AddModelError(nameof(Client.Address), "La dirección es obligatoria.");
                }

                if(!double.TryParse(limitValue, out var limit))
                {
                    ModelState.AddModelError(nameof(Client.Limit), "El límite debe ser un monto válido.");
                }

                var hasOrderData = !string.IsNullOrWhiteSpace(orderProduct)
                    || !string.IsNullOrWhiteSpace(orderQuantityValue)
                    || !string.IsNullOrWhiteSpace(orderUnitPriceValue)
                    || !string.IsNullOrWhiteSpace(orderNotes);
                var orderQuantity = 0;
                var orderUnitPrice = 0D;

                if(hasOrderData)
                {
                    if(string.IsNullOrWhiteSpace(orderProduct))
                    {
                        ModelState.AddModelError("OrderProduct", "El producto del pedido es obligatorio.");
                    }

                    if(!int.TryParse(orderQuantityValue, out orderQuantity) || orderQuantity <= 0)
                    {
                        ModelState.AddModelError("OrderQuantity", "La cantidad debe ser mayor a 0.");
                    }

                    if(!double.TryParse(orderUnitPriceValue, out orderUnitPrice) || orderUnitPrice <= 0)
                    {
                        ModelState.AddModelError("OrderUnitPrice", "El precio unitario debe ser mayor a 0.");
                    }
                }

                if(!ModelState.IsValid)
                {
                    return View(client);
                }

                var credit = new Credit()
                {
                    Client = new Client()
                    {
                        Id = _credits.Credits.LastOrDefault()?.Client.Id + 1 ?? 1,
                        Identification = identification,
                        Name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name),
                        Phone = phone,
                        Address = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(address),
                        Limit = limit
                    }
                };

                if(hasOrderData)
                {
                    var description = $"Pedido: {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(orderProduct)} x {orderQuantity}";
                    if(!string.IsNullOrWhiteSpace(orderNotes))
                    {
                        description += $" - {orderNotes}";
                    }

                    credit.CreditLines.Add(new CreditLine
                    {
                        Id = 1,
                        CreatedDate = DateTime.Now,
                        Description = description,
                        Amount = orderQuantity * orderUnitPrice
                    });
                    credit.CreditSummary.Total = orderQuantity * orderUnitPrice;
                }

                _credits.Credits.Add(credit);
                SetCredits(_credits);
                return RedirectToAction(nameof(Index));
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message, errorStack = ex.StackTrace });
            }
        }

        // POST: CreditController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, IFormCollection collection)
        {
            try
            {
                var credit = _credits.Credits.First(x => x.Client.Id == int.Parse(id));
                credit.Client = new Client()
                {
                    Id = int.Parse(id),
                    Identification = collection["identification"],
                    Name = collection["name"],
                    Phone = collection["phone"],
                    Address = collection["address"],
                    Limit = !string.IsNullOrEmpty(collection["limit"]) ? double.Parse(collection["limit"]) : null
                };
                SetCredits(_credits);
                return RedirectToAction(nameof(Index));
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
            }
        }

        // POST: CreditController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id)
        {
            try
            {
                var credit = _credits.Credits.First(x => x.Client.Id == int.Parse(id));
                _credits.Credits.Remove(credit);
                SetCredits(_credits);
                return RedirectToAction(nameof(Index));
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
            }
        }

        #region Credit Line

        // POST: CreditController/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Add(int id, string searchString, int currentPage, IFormCollection collection)
        {
            try
            {
                var credit = _credits.Credits.FirstOrDefault(x => x.Client.Id == id);
                if(double.Parse(collection["amount"]) > 0)
                {
                    var previousTotal = credit?.CreditSummary?.Total ?? 0;
                    var creditLine = new CreditLine()
                    {
                        Id = credit?.CreditLines.LastOrDefault()?.Id + 1 ?? 1,
                        CreatedDate = DateTime.Now,
                        Description = collection["description"],
                        Amount = double.Parse(collection["amount"])
                    };

                    if(credit != null && credit.CreditSummary != null)
                    {
                        credit.CreditLines.Add(creditLine);
                        credit.CreditSummary.Total = credit.CreditSummary.Total + creditLine.Amount;

                        if(credit.Client.Limit.HasValue
                            && previousTotal <= credit.Client.Limit.Value
                            && credit.CreditSummary.Total > credit.Client.Limit.Value)
                        {
                            SetLimitExceededTempData(credit);
                        }
                    }

                    SetCredits(_credits);
                }
                return RedirectToAction(nameof(Index), new { searchString = searchString, clientId = id, currentPage = currentPage, });
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
            }
        }

        // POST: CreditController/Fee
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Fee(int id, string searchString, int currentPage, IFormCollection collection)
        {
            try
            {
                var credit = _credits.Credits.FirstOrDefault(x => x.Client.Id == id);
                if(credit is not null)
                {
                    var creditLine = new CreditLine()
                    {
                        Id = credit?.CreditLines.LastOrDefault()?.Id + 1 ?? 1,
                        CreatedDate = DateTime.Now,
                        Description = collection["description"],
                        Amount = -(double.Parse(collection["amount"]))
                    };

                    if(credit != null && credit.CreditSummary != null)
                    {
                        credit.CreditLines.Add(creditLine);
                        credit.CreditSummary.Total = credit.CreditSummary.Total + creditLine.Amount;
                    }

                    SetCredits(_credits);
                }
                return RedirectToAction(nameof(Index), new { searchString = searchString, clientId = id, currentPage = currentPage });
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
            }
        }

        // POST: CreditController/Refresh/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Refresh(int clientId, string searchString, int currentPage, IFormCollection collection)
        {
            try
            {
                var credit = _credits.Credits.First(x => x.Client.Id == clientId);
                var line = credit.CreditLines.First(l => l.Id == int.Parse(collection["CreditLine.Id"]));

                var previousTotal = credit.CreditSummary.Total;
                credit.CreditSummary.Total -= line.Amount;

                line.Description = collection["CreditLine.description"];
                line.Amount = double.Parse(collection["CreditLine.amount"]);

                credit.CreditSummary.Total += line.Amount;

                if(credit.Client.Limit.HasValue
                    && previousTotal <= credit.Client.Limit.Value
                    && credit.CreditSummary.Total > credit.Client.Limit.Value)
                {
                    SetLimitExceededTempData(credit);
                }

                SetCredits(_credits);
                return RedirectToAction(nameof(Index), new { searchString = searchString, clientId = clientId, currentPage = currentPage });
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
            }
        }

        // POST: CreditController/Remove/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Remove(string clientId, string searchString, int currentPage, string lineId)
        {
            try
            {
                var credit = _credits.Credits.First(c => c.Client.Id == int.Parse(clientId));
                var line = credit.CreditLines.First(l => l.Id == int.Parse(lineId));
                credit.CreditSummary.Total -= line.Amount;
                credit.CreditLines.Remove(line);
                SetCredits(_credits);
                return RedirectToAction(nameof(Index), new { searchString = searchString, clientId = clientId, currentPage = currentPage });
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
            }
        }

        // POST: CreditController/Clear/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Clear(string? Id, string searchString, int currentPage)
        {
            try
            {
                var credit = _credits.Credits.First(c => c.Client.Id == int.Parse(Id ?? "0"));
                credit.CreditSummary.Total = 0;
                credit.CreditLines.Clear();
                SetCredits(_credits);
                return RedirectToAction(nameof(Index), new { searchString = searchString, clientId = Id, currentPage = currentPage });
            }
            catch(Exception ex)
            {
                return RedirectToAction("Error", "Home", new { errorMsg = ex.Message });
            }
        }
        #endregion



        private static bool IsFortnightCollectionWindow(DateTime date)
        {
            var day = date.Day;
            var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);

            var firstWindowStart = 14;
            var firstWindowEnd = 16;

            var secondWindowStart = Math.Max(daysInMonth - 2, 1);
            var secondWindowEnd = daysInMonth;

            return (day >= firstWindowStart && day <= firstWindowEnd)
                || (day >= secondWindowStart && day <= secondWindowEnd);
        }

        private CreditModel GetCredits()
        {
            var credits = JsonFile.Read("Credits", new CreditModel());
            return credits;
        }

        private void SetCredits(CreditModel credits)
        {
            JsonFile.Write("Credits", credits);
        }

        private string GetMessage(IList<Credit> credits)
        {
            var text = "";
            foreach(var credit in credits)
            {
                text += "%0A";
                text += $"✅ {credit.Client.Name.ToUpper()}: *₡ {credit.CreditSummary.Total}*. ";
            }
            return text;
        }

        private void SetLimitExceededTempData(Credit credit)
        {
            TempData["LimitExceededClientName"] = credit.Client.Name;
            TempData["LimitExceededClientTotal"] = credit.CreditSummary.Total.ToCRC();
            TempData["LimitExceededClientLimit"] = credit.Client.Limit?.ToCRC();
        }
    }
}
