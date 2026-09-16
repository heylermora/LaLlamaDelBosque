using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Models;

namespace LaLlamaDelBosque.Services
{
	public sealed class ScrapingService: IScrapingService
	{
		private readonly IReadOnlyList<IScraperStrategy> _scrapers;
		private readonly IJsonRepository _repository;
		private readonly TimeProvider _timeProvider;

		public ScrapingService(
			IEnumerable<IScraperStrategy> scrapers,
			IJsonRepository repository,
			TimeProvider timeProvider)
		{
			_scrapers = scrapers.ToList();
			_repository = repository;
			_timeProvider = timeProvider;
		}

		public async Task<Award> Add()
		{
			var award = new Award
			{
				Date = _timeProvider.GetLocalNow().Date,
				AwardLines = new List<AwardLine>()
			};
			var scrapingLotteries = _repository.Read<ScrapingLotteryModel>("ScrapingLotteries").Lotteries;
			var lotteries = _repository.Read<LotteryModel>("Lotteries").Lotteries;
			var papers = _repository.Read<PaperModel>("Papers").Papers;

			foreach(var scraper in _scrapers)
			{
				var awardLines = await scraper.ScrapeAwards(scrapingLotteries, lotteries, papers);
				award.AwardLines.AddRange(awardLines);
			}

			award.AwardLines = award.AwardLines.OrderBy(x => x.Order).ToList();
			return award;
		}
	}
}
