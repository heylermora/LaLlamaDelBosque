using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Models;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public abstract class BaseScraper: IScraperStrategy
	{
		protected readonly HttpClient _httpClient;
		protected readonly string _url;

		protected BaseScraper(HttpClient httpClient, string url)
		{
			_httpClient = httpClient;
			_url = url;
		}

		public async Task<List<AwardLine>> ScrapeAwards(
				   List<ScrapingLottery> scrapingLotteries,
				   List<Lottery> lotteries,
				   List<Paper> papers)
		{
			try
			{
				var response = await _httpClient.GetAsync(_url);
				response.EnsureSuccessStatusCode();
				var htmlContent = await response.Content.ReadAsStringAsync();

				return ProcessHtml(htmlContent, scrapingLotteries, lotteries, papers);
			}
			catch(Exception ex)
			{
				Console.WriteLine($"Error en ScrapeAwards: {ex.Message}");
				return new List<AwardLine>();
			}
		}

		protected abstract List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers);
	}
}
