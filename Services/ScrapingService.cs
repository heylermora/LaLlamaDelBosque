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
			var configuredProcesses = _repository.Read<ScrapingConfiguration>("ScrapingLotteries").Processes
				.Where(x => x.Enabled)
				.GroupBy(x => x.Type, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
			var lotteries = _repository.Read<LotteryModel>("Lotteries").Lotteries;
			var papers = _repository.Read<PaperModel>("Papers").Papers;
			var awardLinesByOrder = new Dictionary<int, AwardLine>();
			var sortOrderByLottery = configuredProcesses.Values
				.SelectMany(x => x.Draws)
				.GroupBy(x => x.Order)
				.ToDictionary(x => x.Key, x => x.First().SortOrder);

			var successfulScrapers = 0;
			foreach(var scraper in _scrapers)
			{
				if(!configuredProcesses.TryGetValue(scraper.LotteryType, out var process))
					continue;

				try
				{
					var configuredDraws = process.Draws
						.Where(x => x.Enabled)
						.OrderBy(x => x.SortOrder)
						.ToList();
					var awardLines = await scraper.ScrapeAwards(configuredDraws, lotteries, papers);
					foreach(var awardLine in awardLines)
						awardLinesByOrder.TryAdd(awardLine.Order, awardLine);
					successfulScrapers++;
				}
				catch(Exception ex)
				{
					_warnings.Add(ex.Message);
				}
			}

			if(successfulScrapers == 0 && _warnings.Count > 0)
				throw new InvalidOperationException("No fue posible actualizar resultados desde ninguna fuente.", new AggregateException(_warnings.Select(x => new InvalidOperationException(x))));

			award.AwardLines = awardLinesByOrder.Values
				.OrderBy(x => sortOrderByLottery.GetValueOrDefault(x.Order, x.Order))
				.ToList();
			return award;
		}
	}
}
