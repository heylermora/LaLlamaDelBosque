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
			var response = await _httpClient.GetAsync(_url);
			response.EnsureSuccessStatusCode();
			var htmlContent = await response.Content.ReadAsStringAsync();

			return ProcessHtml(htmlContent, scrapingLotteries, lotteries, papers);
		}

		protected abstract List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers);

		protected AwardLine? CreateAwardLine(
			int order,
			string description,
			string number,
			bool isBusted,
			List<Paper> papers)
		{
			var filteredPapers = papers
				.Where(x => x.Lottery == description
							&& x.DrawDate.ToShortDateString() == DateTime.Today.ToShortDateString()
							&& x.Numbers.Any(n => n.Value == number))
				.ToList();

			if(!filteredPapers.Any())
				return new AwardLine
				{
					Order = order,
					Description = description,
					Number = number,
					IsBusted = isBusted
				};

			var amount = filteredPapers.Sum(x => x.Numbers.Sum(n => n.Value == number ? n.Amount : 0));
			var busted = filteredPapers.Sum(x => x.Numbers.Sum(n => n.Value == number ? n.Busted : 0));

			var award = isBusted ? (85 * amount) + (200 * busted) : 85 * amount;

			return new AwardLine
			{
				Order = order,
				Description = description,
				Number = number,
				Amount = amount,
				Busted = busted,
				Award = award,
				IsBusted = isBusted
			};
		}
	}
}
