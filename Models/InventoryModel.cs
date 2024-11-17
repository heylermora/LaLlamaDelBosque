namespace LaLlamaDelBosque.Models
{
	public class InventoryModel
	{
		public IList<Article> Inventory { get; set; } = new List<Article>();
	}

	public class Article
	{
		public int Id { get; set; } // Primary key
		public string Code { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public int FamilyId { get; set; }
		public Family Family { get; set; } = new Family(); // Navigation to Family entity
		public int DepartmentId { get; set; }
		public Department Department { get; set; } = new Department();  // Navigation to Department entity
		public int SupplierId { get; set; }
		public Supplier Supplier { get; set; } = new Supplier(); // Navigation to Supplier entity
		public decimal CostPrice { get; set; }
		public decimal NetPrice { get; set; }
		public decimal SalePrice { get; set; }
		public int Stock { get; set; }
		public decimal Discount { get; set; }
		public decimal VAT { get; set; }
		public decimal Utility { get; set; }
	}
}
