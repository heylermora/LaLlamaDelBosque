using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace LaLlamaDelBosque.Models
{
	public class AwardModel
	{
		public IList<Award> Awards { get; set; } = new List<Award>();
	}
	public class AwardLines
	{
		public int Order { get; set; }
		public string Description { get; set; } = "";
		public int Number { get; set; }
		public int Busted { get; set; }
		public double Amount { get; set; }
		public double Award { get; set; }
	}
	public class Award
	{
		public int Id { get; set; }
		public DateTime Date { get; set; } = new DateTime();
		public IList<AwardLines> AwardLines { get; set; } = new List<AwardLines>();

	}
}
