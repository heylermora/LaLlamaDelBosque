using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Models;

namespace LaLlamaDelBosque.Services.Scrapers
{
	internal static class ScrapingSourceCatalog
	{
		public static IReadOnlyList<ScrapingSource> GetEnabled(IJsonRepository repository, string lotteryType)
		{
			return repository.Read<ScrapingLotteryModel>("ScrapingLotteries").Sources
				.Where(x => x.Enabled
					&& x.LotteryType.Equals(lotteryType, StringComparison.OrdinalIgnoreCase)
					&& !string.IsNullOrWhiteSpace(x.Url))
				.Select(x => new ScrapingSource(
					x.Key,
					x.Url,
					string.IsNullOrWhiteSpace(x.Referrer) ? x.Url : x.Referrer,
					x.IsDedicatedCostaRicaPage))
				.ToList();
		}

		public static ScrapingSource GetPrimary(IJsonRepository repository, string lotteryType)
		{
			return GetEnabled(repository, lotteryType).FirstOrDefault()
				?? throw new InvalidOperationException($"No hay fuentes habilitadas para {lotteryType} en ScrapingLotteries.json.");
		}
	}
}
