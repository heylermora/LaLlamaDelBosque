using System.Collections.ObjectModel;

namespace LaLlamaDelBosque.Models
{
	public class FamilyModel
	{
		public IList<Family> Families { get; set; } = new List<Family>();
	}

	public class Family
	{
		public int Id { get; set; } // Primary key
		public string Name { get; set; } = string.Empty;
		public ICollection<Article> Articles { get; set; } = new Collection<Article>(); // One-to-many relationship with Article entity
	}
}
