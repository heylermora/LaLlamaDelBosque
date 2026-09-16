using LaLlamaDelBosque.Models;

namespace LaLlamaDelBosque.Interfaces
{
	public interface IScrapingService
	{
		IReadOnlyList<string> Warnings { get; }
		Task<Award> Add();
	}
}
