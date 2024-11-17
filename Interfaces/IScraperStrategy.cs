using LaLlamaDelBosque.Models;

namespace LaLlamaDelBosque.Interfaces
{
	public interface IScraperStrategy
	{
		Task<List<AwardLine>> ScrapeAwards(List<ScrapingLottery> scrapingLotteries, List<Lottery> lotteries, List<Paper> papers);
	}
}
