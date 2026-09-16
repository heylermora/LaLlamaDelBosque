using LaLlamaDelBosque.Models;

namespace LaLlamaDelBosque.Interfaces
{
	public interface IScrapingService
	{
		Task<Award> Add();
	}
}
