using System.ComponentModel.DataAnnotations;

namespace LaLlamaDelBosque.Models
{
    public class NoteModel
    {
        public List<Note> Notes { get; set; } = new List<Note>();
    }

    public class CashRegisterModel
    {
        public int DataVersion { get; set; }
        public List<CashRegisterClose> CashClosings { get; set; } = new List<CashRegisterClose>();
    }

    public class CashRegisterClose
    {
        public int Id { get; set; }
        public DateTime ShiftDate { get; set; } = DateTime.Today;
        public DateTime ClosedAt { get; set; }
        public string ClosedBy { get; set; } = "";

        public double InitialCash { get; set; }
        public double CashReceived { get; set; }
        public double FinalCash { get; set; }
        public double BankDeposit { get; set; }
        public double PrizePayments { get; set; }
        public double AccountsReceivable { get; set; }
        public double ProviderInitialCash { get; set; }
        public double ProviderFinalCash { get; set; }
        public List<ProviderExpense> Providers { get; set; } = new List<ProviderExpense>();

        public double ProviderTotal => Math.Round(Providers.Sum(p => p.Amount), 2);
        public double ExpectedCash => Math.Round(InitialCash + CashReceived - PrizePayments, 2);
        public double Difference => Math.Round(FinalCash - ExpectedCash, 2);
        public double ExpectedProviderCash => Math.Round(ProviderInitialCash - ProviderTotal, 2);
        public double ProviderDifference => Math.Round(ProviderFinalCash - ExpectedProviderCash, 2);
        public bool IsBalanced => Math.Abs(Difference) < 0.01 && Math.Abs(ProviderDifference) < 0.01;
    }

    public class ProviderExpense
    {
        public string Name { get; set; } = "";
        public double Amount { get; set; }
    }

    public enum NotePaymentMethod
    {
        SINPE = 1,
        Tarjeta = 2
    }

    public class Note
    {
        public int Id { get; set; }
        public int? CashCloseId { get; set; }

        [Required(ErrorMessage = "El campo es requerido")]
        [StringLength(50, ErrorMessage = "La longitud del título debe estar entre {2} y {1}.", MinimumLength = 2)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "El campo es requerido")]
        [Range(10, 1000000, ErrorMessage = "El valor debe estar entre {1} y {2}.")]
        public double Value { get; set; }

        [Required(ErrorMessage = "El método de pago es requerido")]
        [Display(Name = "Método de pago")]
        public NotePaymentMethod PaymentMethod { get; set; } = NotePaymentMethod.SINPE;

        [Required(ErrorMessage = "El día de turno es requerido")]
        [DataType(DataType.Date)]
        [Display(Name = "Día de turno")]
        public DateTime ShiftDate { get; set; } = DateTime.Today;

        public bool IsVerified { get; set; }
    }
}
