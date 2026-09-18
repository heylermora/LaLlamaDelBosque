using LaLlamaDelBosque.Models;

namespace LaLlamaDelBosque.Interfaces
{
	public interface IScraperStrategy
	{
		string LotteryType { get; }
		Task<List<AwardLine>> ScrapeAwards(List<ScrapingDrawConfiguration> scrapingLotteries, List<Lottery> lotteries, List<Paper> papers);
	}
}
