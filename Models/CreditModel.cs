using System.ComponentModel.DataAnnotations;

namespace LaLlamaDelBosque.Models
{
	public class CreditModel
	{
		public IList<Credit> Credits { get; set; } = new List<Credit>();

	}
	public class Client
	{
		public int Id { get; set; }
		public string Name { get; set; } = "";

		[RegularExpression(@"506[0-9]{8}", ErrorMessage = "Este número no es válido.")]
		public string Phone { get; set; } = "";

	}
	public class CreditLine
	{
		public int Id { get; set; }
		public DateTime CreatedDate { get; set; }
		public string Description { get; set; } = "";
		public double Amount { get; set; }

	}
	public class CreditSummary
	{
		public double Total { get; set; }
		public int CountLines { get; set; }
		public int CountDays { get; set; }

	}
	public class Credit
	{
		public Client Client { get; set; } = new Client();
		public IList<CreditLine> CreditLines { get; set; } = new List<CreditLine>();
		public CreditSummary CreditSummary { get; set; } = new CreditSummary();

		public string Message
		{
			get
			{
				var text = "";
				foreach(var creditLine in CreditLines)
				{
					text += "%0A";
					if(creditLine.Amount > 0)
						text += "➕ ";
					else
						text += "➖ ";
					text += $"{creditLine.CreatedDate} {creditLine.Description.ToLower()} : ₡ {creditLine.Amount} ";
				}
				return text;
			}
		}

	}

}
