using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace LaLlamaDelBosque.Models
{
	public class Setting
	{
		public List<Item> Values { get; set; } = new List<Item>();

	}

	public class Item
	{
		public int Id { get; set; }
		public string Name { get; set; } = "";
		public double Value { get; set; } = 0;
	}

	public class AwardSetting
	{
		public Setting TicaSetting { get; set; } = new Setting();
		public Setting NicaSetting { get; set; } = new Setting();
	}
}
