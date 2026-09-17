using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Models;

namespace LaLlamaDelBosque.Services.Scrapers
{
	internal static class ScrapingSourceCatalog
	{
		public static IReadOnlyList<ScrapingSource> GetEnabled(IJsonRepository repository, string lotteryType)
		{
			var process = repository.Read<ScrapingConfiguration>("ScrapingLotteries").Processes
				.FirstOrDefault(x => x.Type.Equals(lotteryType, StringComparison.OrdinalIgnoreCase));
			if(process == null)
				return Array.Empty<ScrapingSource>();

			return process.Sources
				.Where(x => x.Enabled
					&& !string.IsNullOrWhiteSpace(x.Url))
				.Select(x => new ScrapingSource(
					x.Key,
					x.Url,
					string.IsNullOrWhiteSpace(x.Referrer) ? x.Url : x.Referrer,
					x.IsDedicatedCostaRicaPage,
					TimeSpan.FromSeconds(x.TimeoutSeconds > 0 ? x.TimeoutSeconds : 15)))
				.ToList();
		}

		public static ScrapingSource GetPrimary(IJsonRepository repository, string lotteryType)
		{
			return GetEnabled(repository, lotteryType).FirstOrDefault()
				?? new ScrapingSource("disabled", "about:blank", "about:blank");
		}
	}
}
