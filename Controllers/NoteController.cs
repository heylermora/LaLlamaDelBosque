using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaLlamaDelBosque.Controllers
{
    [Authorize]
    public class NoteController: Controller
    {
        private readonly NoteModel _notes;
        private readonly CashRegisterModel _cashRegister;

        public NoteController()
        {
            _notes = GetNotes();
            _cashRegister = GetCashRegister();
        }

        public IActionResult Index(string? searchText, DateTime? shiftDate, NotePaymentMethod? paymentMethod, int? closeId)
        {
            var selectedDate = shiftDate?.Date ?? DateTime.Today;
            var dayNotes = _notes.Notes.Where(n => n.ShiftDate.Date == selectedDate).ToList();
            var notes = dayNotes.AsEnumerable();

            if(!string.IsNullOrWhiteSpace(searchText))
                notes = notes.Where(n => n.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));

            if(paymentMethod.HasValue)
                notes = notes.Where(n => n.PaymentMethod == paymentMethod.Value);

            var filteredNotes = notes
                .OrderBy(n => n.PaymentMethod)
                .ThenBy(n => n.Title)
                .ToList();

            ViewBag.SearchText = searchText;
            ViewBag.ShiftDate = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.PaymentMethod = paymentMethod;
            // The close summary must always represent the complete shift, even when the
            // user is temporarily filtering the table.
            ViewBag.SinpeTotal = dayNotes.Where(n => n.PaymentMethod == NotePaymentMethod.SINPE).Sum(n => n.Value);
            ViewBag.CardTotal = dayNotes.Where(n => n.PaymentMethod == NotePaymentMethod.Tarjeta).Sum(n => n.Value);
            ViewBag.GrandTotal = dayNotes.Sum(n => n.Value);
            ViewBag.PendingVerificationCount = dayNotes.Count(n => !n.IsVerified);
            ViewBag.AllPaymentsVerified = dayNotes.All(n => n.IsVerified);
            var dailyClosings = _cashRegister.CashClosings
                .Where(close => close.ShiftDate.Date == selectedDate)
                .OrderBy(close => close.ClosedAt == default ? close.ShiftDate : close.ClosedAt)
                .ToList();
            var selectedClose = closeId.HasValue
                ? dailyClosings.FirstOrDefault(close => close.Id == closeId.Value)
                : null;

            ViewBag.CashClose = selectedClose ?? new CashRegisterClose { ShiftDate = selectedDate };
            ViewBag.DailyClosings = dailyClosings;
            ViewBag.SelectedCloseId = selectedClose?.Id;
            ViewBag.ClosingDates = _cashRegister.CashClosings
                .Select(c => c.ShiftDate.Date)
                .Distinct()
                .OrderByDescending(date => date)
                .ToList();
            ViewBag.ClosingCounts = _cashRegister.CashClosings
                .GroupBy(close => close.ShiftDate.Date)
                .ToDictionary(group => group.Key, group => group.Count());
            return View(filteredNotes);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePayments(IFormCollection collection)
        {
            if(!DateTime.TryParse(collection["shiftDate"], out var shiftDate))
                return BadRequest("La fecha del turno no es válida.");

            var submitted = new List<Note>();
            submitted.AddRange(BuildPayments(collection, "sinpe", NotePaymentMethod.SINPE, shiftDate.Date));
            submitted.AddRange(BuildPayments(collection, "card", NotePaymentMethod.Tarjeta, shiftDate.Date));

            var nextId = _notes.Notes.Any() ? _notes.Notes.Max(note => note.Id) + 1 : 1;
            foreach(var note in submitted.Where(note => note.Id <= 0))
                note.Id = nextId++;

            _notes.Notes.RemoveAll(note => note.ShiftDate.Date == shiftDate.Date);
            _notes.Notes.AddRange(submitted);
            SetNotes(_notes);
            TempData["SuccessMessage"] = "Movimientos guardados correctamente.";
            return RedirectToAction(nameof(Index), new { shiftDate = shiftDate.ToString("yyyy-MM-dd") });
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new Note { ShiftDate = DateTime.Today });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Note model)
        {
            if(!ModelState.IsValid)
                return View(model);

            model.Id = _notes.Notes.Any() ? _notes.Notes.Max(n => n.Id) + 1 : 1;
            _notes.Notes.Add(model);
            SetNotes(_notes);
            TempData["SuccessMessage"] = "Nota creada correctamente.";
            return RedirectToAction(nameof(Index), new { shiftDate = model.ShiftDate.ToString("yyyy-MM-dd") });
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var note = _notes.Notes.FirstOrDefault(n => n.Id == id);
            if(note is null)
                return NotFound();

            return View(note);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Note model)
        {
            if(!ModelState.IsValid)
                return View(model);

            var note = _notes.Notes.FirstOrDefault(n => n.Id == model.Id);
            if(note is null)
                return NotFound();

            note.Title = model.Title;
            note.Value = model.Value;
            note.PaymentMethod = model.PaymentMethod;
            note.ShiftDate = model.ShiftDate.Date;
            note.IsVerified = model.IsVerified;
            SetNotes(_notes);
            TempData["SuccessMessage"] = "Nota actualizada correctamente.";
            return RedirectToAction(nameof(Index), new { shiftDate = model.ShiftDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var removed = _notes.Notes.RemoveAll(n => n.Id == id) > 0;
            if(!removed)
                return NotFound();

            SetNotes(_notes);
            TempData["SuccessMessage"] = "Nota eliminada correctamente.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleVerified(int id, DateTime shiftDate)
        {
            var note = _notes.Notes.FirstOrDefault(n => n.Id == id);
            if(note is null)
                return NotFound();

            note.IsVerified = !note.IsVerified;
            SetNotes(_notes);
            return RedirectToAction(nameof(Index), new { shiftDate = shiftDate.ToString("yyyy-MM-dd") });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveCashClose(IFormCollection collection)
        {
            var shiftDate = DateTime.Parse(collection["shiftDate"]);
            _ = int.TryParse(collection["closeId"], out var closeId);
            var shiftPayments = _notes.Notes.Where(n => n.ShiftDate.Date == shiftDate.Date).ToList();
            if(shiftPayments.Any(n => !n.IsVerified))
            {
                TempData["ErrorMessage"] = "No puede hacer cambio de turno: hay pagos SINPE/Tarjeta sin check de comprobación.";
                return RedirectToAction(nameof(Index), new { shiftDate = shiftDate.ToString("yyyy-MM-dd"), closeId = closeId > 0 ? closeId : null });
            }

            var cashClose = closeId > 0
                ? _cashRegister.CashClosings.FirstOrDefault(close => close.Id == closeId && close.ShiftDate.Date == shiftDate.Date)
                : null;
            if(cashClose is null)
            {
                cashClose = new CashRegisterClose
                {
                    Id = _cashRegister.CashClosings.Any() ? _cashRegister.CashClosings.Max(c => c.Id) + 1 : 1,
                    ShiftDate = shiftDate.Date,
                    ClosedAt = DateTime.Now
                };
                _cashRegister.CashClosings.Add(cashClose);
            }

            cashClose.InitialCash = ParseMoney(collection["initialCash"]);
            cashClose.CashReceived = ParseMoney(collection["cashReceived"]);
            cashClose.FinalCash = ParseMoney(collection["finalCash"]);
            cashClose.BankDeposit = ParseMoney(collection["bankDeposit"]);
            cashClose.PrizePayments = ParseMoney(collection["prizePayments"]);
            cashClose.AccountsReceivable = ParseMoney(collection["accountsReceivable"]);
            cashClose.ProviderInitialCash = ParseMoney(collection["providerInitialCash"]);
            cashClose.ProviderFinalCash = ParseMoney(collection["providerFinalCash"]);
            cashClose.Providers = BuildProviders(collection);

            if(!cashClose.IsBalanced)
            {
                TempData["ErrorMessage"] = $"El cierre no cuadra. Diferencia de caja: {cashClose.Difference.ToCRC()}. Diferencia de proveedores: {cashClose.ProviderDifference.ToCRC()}.";
                return RedirectToAction(nameof(Index), new { shiftDate = shiftDate.ToString("yyyy-MM-dd"), closeId = closeId > 0 ? closeId : null });
            }

            SetCashRegister(_cashRegister);
            TempData["SuccessMessage"] = "Caja guardada correctamente. Ya puede hacer el cambio de turno.";
            return RedirectToAction(nameof(Index), new { shiftDate = shiftDate.ToString("yyyy-MM-dd"), closeId = cashClose.Id });
        }

        public IActionResult Search(SearchModel srcModel)
        {
            return RedirectToAction(nameof(Index), new { searchText = srcModel.SearchText });
        }

        private static List<ProviderExpense> BuildProviders(IFormCollection collection)
        {
            var names = collection["providerNames"];
            var amounts = collection["providerAmounts"];
            var providers = new List<ProviderExpense>();

            for(var index = 0; index < Math.Max(names.Count, amounts.Count); index++)
            {
                var name = index < names.Count ? names[index] ?? "" : "";
                var amount = index < amounts.Count ? ParseMoney(amounts[index]) : 0;
                if(!string.IsNullOrWhiteSpace(name) || amount > 0)
                {
                    providers.Add(new ProviderExpense { Name = name, Amount = amount });
                }
            }

            return providers;
        }

        private static List<Note> BuildPayments(IFormCollection collection, string prefix, NotePaymentMethod method, DateTime shiftDate)
        {
            var ids = collection[$"{prefix}Ids"];
            var titles = collection[$"{prefix}Titles"];
            var amounts = collection[$"{prefix}Amounts"];
            var verifiedIds = collection[$"{prefix}Verified"].Select(value => value ?? "").ToHashSet(StringComparer.OrdinalIgnoreCase);
            var payments = new List<Note>();

            for(var index = 0; index < Math.Max(ids.Count, Math.Max(titles.Count, amounts.Count)); index++)
            {
                var title = index < titles.Count ? titles[index]?.Trim() ?? "" : "";
                var amount = index < amounts.Count ? ParseMoney(amounts[index]) : 0;
                if(string.IsNullOrWhiteSpace(title) && amount <= 0)
                    continue;

                var idText = index < ids.Count ? ids[index] ?? "0" : "0";
                _ = int.TryParse(idText, out var id);
                payments.Add(new Note
                {
                    Id = id,
                    Title = title,
                    Value = amount,
                    PaymentMethod = method,
                    ShiftDate = shiftDate,
                    IsVerified = verifiedIds.Contains(idText)
                });
            }

            return payments;
        }

        private static double ParseMoney(string? value)
        {
            if(string.IsNullOrWhiteSpace(value))
                return 0;

            var sanitized = value.Replace("₡", "").Replace(",", "").Trim();
            return double.TryParse(sanitized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var amount)
                ? amount
                : 0;
        }

        private static NoteModel GetNotes()
        {
            var notes = JsonFile.Read<NoteModel>("Notes", new NoteModel());
            foreach(var note in notes.Notes.Where(n => n.ShiftDate == default))
            {
                note.ShiftDate = DateTime.Today;
            }
            return notes;
        }

        private static void SetNotes(NoteModel notes)
        {
            JsonFile.Write<NoteModel>("Notes", notes);
        }

        private static CashRegisterModel GetCashRegister()
        {
            return JsonFile.Read<CashRegisterModel>("CashRegister", new CashRegisterModel());
        }

        private static void SetCashRegister(CashRegisterModel cashRegister)
        {
            JsonFile.Write<CashRegisterModel>("CashRegister", cashRegister);
        }
    }
}
