using System.Collections.ObjectModel;

namespace LaLlamaDelBosque.Models
{
	public class DepartmentModel
	{
		public IList<Department> Departments { get; set; } = new List<Department>();
	}

	public class Department
	{
		public int Id { get; set; } // Primary key
		public string Name { get; set; } = string.Empty;
		public ICollection<Article> Articles { get; set; } = new Collection<Article>(); // One-to-many relationship with Article entity
	}
}
