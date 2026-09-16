using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Models;

namespace LaLlamaDelBosque.Services
{
	public sealed class ScrapingService: IScrapingService
	{
		private readonly IReadOnlyList<IScraperStrategy> _scrapers;
		private readonly IJsonRepository _repository;
		private readonly TimeProvider _timeProvider;
		private readonly List<string> _warnings = new();

		public IReadOnlyList<string> Warnings => _warnings;

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
			_warnings.Clear();
			var award = new Award
			{
				Date = _timeProvider.GetLocalNow().Date,
				AwardLines = new List<AwardLine>()
			};
			var scrapingLotteries = _repository.Read<ScrapingLotteryModel>("ScrapingLotteries").Lotteries;
			var lotteries = _repository.Read<LotteryModel>("Lotteries").Lotteries;
			var papers = _repository.Read<PaperModel>("Papers").Papers;

			var successfulScrapers = 0;
			foreach(var scraper in _scrapers)
			{
				try
				{
					var awardLines = await scraper.ScrapeAwards(scrapingLotteries, lotteries, papers);
					foreach(var awardLine in awardLines)
						award.AwardLines.Add(awardLine);
					successfulScrapers++;
				}
				catch(Exception ex)
				{
					_warnings.Add(ex.Message);
				}
			}

			if(successfulScrapers == 0 && _warnings.Count > 0)
				throw new InvalidOperationException("No fue posible actualizar resultados desde ninguna fuente.", new AggregateException(_warnings.Select(x => new InvalidOperationException(x))));

			award.AwardLines = award.AwardLines.OrderBy(x => x.Order).ToList();
			return award;
		}
	}
}
