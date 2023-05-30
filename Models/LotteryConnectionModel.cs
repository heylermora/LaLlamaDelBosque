using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;

namespace LaLlamaDelBosque.Models
{
    public class LotteryConnectionModel
    {
        public int PaperId { get; set; }
        public int LineId { get; set; }
	}

	public class LotterySearchModel
	{
		[Display(Name = "Papelito")]
		public int? Id { get; set; }

		[Display(Name = "Desde")]
		[DataType(DataType.Date)]
		public DateTime? FromDate { get; set; }

		[Display(Name = "Hasta")]
		[DataType(DataType.Date)]
		public DateTime? ToDate { get; set; }

		[Display(Name = "Sorteo")]
		public string? Lottery { get; set; } = string.Empty;

		public IList<Lottery> Lotteries { get; set; } = new List<Lottery>();
	}
}
