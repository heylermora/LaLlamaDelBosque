using LaLlamaDelBosque.Interfaces;
using LaLlamaDelBosque.Models;
using System.Net.Sockets;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public abstract class BaseScraper: IScraperStrategy
	{
		public abstract string LotteryType { get; }
		protected readonly HttpClient _httpClient;
		protected readonly string _url;
		protected readonly TimeProvider _timeProvider;

		protected BaseScraper(HttpClient httpClient, string url, TimeProvider timeProvider)
		{
			_httpClient = httpClient;
			_url = url;
			_timeProvider = timeProvider;
		}

		public virtual async Task<List<AwardLine>> ScrapeAwards(
				   List<ScrapingDrawConfiguration> scrapingLotteries,
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
			catch(HttpRequestException httpEx)
			{
				var errorMessage = $"Error HTTP al realizar la solicitud a la URL {_url}. Detalles: {httpEx.Message}";
				throw new InvalidOperationException(errorMessage, httpEx);
			}
			catch(TaskCanceledException timeoutEx)
			{
				var errorMessage = $"La solicitud HTTP excedió el tiempo de espera para la URL {_url}. Detalles: {timeoutEx.Message}";
				throw new InvalidOperationException(errorMessage, timeoutEx);
			}
			catch(Exception ex)
			{
				var errorMessage = $"Error inesperado al procesar la solicitud HTTP para la URL {_url}. Detalles: {ex.Message}";
				throw new InvalidOperationException(errorMessage, ex);
			}
		}

		protected abstract List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingDrawConfiguration> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers);

		protected AwardLine? CreateAwardLine(
			int order,
			string description,
			string number,
			bool isBusted,
			List<Paper> papers,
			double? timesBusted = null)
		{
			var filteredPapers = papers
				.Where(x => x.Lottery == description
							&& x.DrawDate.Date == _timeProvider.GetLocalNow().Date
							&& x.Numbers.Any(n => n.Value == number))
				.ToList();

			if(!filteredPapers.Any())
				return new AwardLine
				{
					Order = order,
					Description = description,
					Number = number,
					TimesBusted = timesBusted ?? 200,
					IsBusted = isBusted
				};

			var amount = filteredPapers.Sum(x => x.Numbers.Sum(n => n.Value == number ? n.Amount : 0));
			var busted = filteredPapers.Sum(x => x.Numbers.Sum(n => n.Value == number ? n.Busted : 0));

            var matchCount = filteredPapers.Sum(x => x.Numbers.Count(n => n.Value == number));
            var effectiveTimesBusted = timesBusted ?? 200;

			var award = isBusted ? (85 * amount) + (effectiveTimesBusted * busted) : 85 * amount;

			return new AwardLine
			{
				Order = order,
				Description = description,
				Number = number,
				Amount = amount,
				Busted = busted,
				TimesBusted = effectiveTimesBusted,
				Award = award,
				IsBusted = isBusted,
				MatchCount = matchCount,
			};
		}
	}
}
