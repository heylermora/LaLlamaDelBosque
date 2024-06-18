using System.Collections.ObjectModel;

namespace LaLlamaDelBosque.Models
{
	public class SupplierModel
	{
		public IList<Supplier> Suppliers { get; set; } = new List<Supplier>();
	}

	public class Supplier
	{
		public int Id { get; set; } // Primary key
		public string Name { get; set; } = string.Empty;
		public ICollection<Article> Articles { get; set; } = new Collection<Article>(); // One-to-many relationship with Article entity
	}
}
