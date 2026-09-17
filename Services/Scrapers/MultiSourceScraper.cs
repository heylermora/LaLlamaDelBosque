using LaLlamaDelBosque.Models;

namespace LaLlamaDelBosque.Services.Scrapers
{
	/// <summary>
	/// Template Method for scrapers that can complete one result set from several sources.
	/// Sources are consulted in priority order and an earlier result is never overwritten.
	/// </summary>
	public abstract class MultiSourceScraper: BaseScraper
	{
		private readonly IReadOnlyList<ScrapingSource> _sources;

		protected MultiSourceScraper(HttpClient httpClient, IReadOnlyList<ScrapingSource> sources, TimeProvider timeProvider)
			: base(httpClient, GetPrimaryUrl(sources), timeProvider)
		{
			_sources = sources;
		}

		public sealed override async Task<List<AwardLine>> ScrapeAwards(
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			var errors = new List<Exception>();
			var awardLinesByOrder = new Dictionary<int, AwardLine>();
			var expectedOrders = GetExpectedOrders(scrapingLotteries).ToHashSet();

			foreach(var source in _sources)
			{
				try
				{
					var htmlContent = await DownloadSource(source);
					var sourceAwardLines = ProcessHtml(htmlContent, scrapingLotteries, lotteries, papers, source);

					foreach(var awardLine in sourceAwardLines)
						awardLinesByOrder.TryAdd(awardLine.Order, awardLine);

					if(expectedOrders.Count > 0 && expectedOrders.All(awardLinesByOrder.ContainsKey))
						return OrderResults(awardLinesByOrder);

					if(sourceAwardLines.Count == 0)
						errors.Add(new InvalidOperationException($"La fuente {source.Url} respondió correctamente, pero no contenía resultados reconocibles."));
				}
				catch(Exception ex)
				{
					errors.Add(ex);
				}
			}

			if(awardLinesByOrder.Count > 0)
				return OrderResults(awardLinesByOrder);

			throw new InvalidOperationException(GetAllSourcesFailedMessage(), new AggregateException(errors));
		}

		protected abstract IEnumerable<int> GetExpectedOrders(List<ScrapingLottery> scrapingLotteries);

		protected abstract List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers,
			ScrapingSource source);

		protected virtual string GetAllSourcesFailedMessage()
		{
			return "No fue posible obtener resultados desde ninguna de las fuentes configuradas.";
		}

		protected sealed override List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			return ProcessHtml(htmlContent, scrapingLotteries, lotteries, papers, _sources[0]);
		}

		protected virtual async Task<string> DownloadSource(ScrapingSource source)
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, source.Url);
			request.Headers.Referrer = new Uri(source.Referrer);
			using var timeout = new CancellationTokenSource(source.Timeout);
			using var response = await _httpClient.SendAsync(request, timeout.Token);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsStringAsync(timeout.Token);
		}

		private static List<AwardLine> OrderResults(Dictionary<int, AwardLine> awardLinesByOrder)
		{
			return awardLinesByOrder.Values.OrderBy(x => x.Order).ToList();
		}

		private static string GetPrimaryUrl(IReadOnlyList<ScrapingSource> sources)
		{
			if(sources.Count == 0)
				throw new ArgumentException("Debe configurar al menos una fuente.", nameof(sources));

			return sources[0].Url;
		}
	}

	public sealed record ScrapingSource(
		string Url,
		string Referrer,
		bool IsDedicatedCostaRicaPage = false,
		TimeSpan? RequestTimeout = null)
	{
		public TimeSpan Timeout => RequestTimeout ?? TimeSpan.FromSeconds(15);
	}
}
