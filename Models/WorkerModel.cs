namespace LaLlamaDelBosque.Models
{
	public class WorkerModel
	{
		public List<Worker> Workers { get; set; } = new List<Worker>();
	}

	public class Worker
	{
		public string Name { get; set; } = "";
	}


}
