using System.ComponentModel.DataAnnotations;

namespace LaLlamaDelBosque.Models
{

	public class ScheduleModel {
		public IList<Schedule> Schedules { get; set; } = new List<Schedule>();
	}
	public class Schedule
	{
		public int Id { get; set; }

		[Required(ErrorMessage = "El campo es requerido.")]
		public string Name { get; set; } = "";

		[Required(ErrorMessage = "El campo es requerido.")]
		public DateTime CreatedDate { get; set; } = DateTime.Today.AddHours(8);

		[Required(ErrorMessage = "El campo es requerido")]
		public DateTime FinishDate { get; set; } = DateTime.Today.AddHours(20);

		public double Hours 
		{
			get
			{
				TimeSpan ts = FinishDate.Subtract(CreatedDate);
				return Math.Round(ts.TotalHours, 2);
			}
		}

		public double Salary
		{
			get
			{
				return Math.Round(Hours * ConfigurationModel.Price, 2);
			}
		}

		public bool IsCompleted { get; set; } = false;
	}
}
