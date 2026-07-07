using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Utils;
using System.Globalization;
using System.Text.Json;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class JpsNuevosTiemposScraper: BaseScraper
	{
		public JpsNuevosTiemposScraper(HttpClient httpClient)
			: base(httpClient, "https://integration.jps.go.cr/api/App/nuevostiempos/page")
		{
		}

		public override async Task<List<AwardLine>> ScrapeAwards(
			List<ScrapingLottery> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			try
			{
				using var request = new HttpRequestMessage(HttpMethod.Get, _url);
				request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
				request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
				request.Headers.TryAddWithoutValidation("Accept-Language", "es-CR,es;q=0.9,en;q=0.8");
				request.Headers.TryAddWithoutValidation("Origin", "https://www.jps.go.cr");
				request.Headers.Referrer = new Uri("https://www.jps.go.cr/resultados/nuevos-tiempos-reventados");

				var response = await _httpClient.SendAsync(request);
				response.EnsureSuccessStatusCode();
				var jsonContent = await response.Content.ReadAsStringAsync();

				return ProcessHtml(jsonContent, scrapingLotteries, lotteries, papers);
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

		protected override List<AwardLine> ProcessHtml(string htmlContent, List<ScrapingLottery> scrapingLotteries, List<Lottery> lotteries, List<Paper> papers)
		{
			var awardLines = new List<AwardLine>();
			var todayResult = GetTodayResult(htmlContent);

			if(todayResult.ValueKind == JsonValueKind.Undefined)
				return awardLines;

			foreach(var scrapingLottery in scrapingLotteries.Where(HasJpsSourceKey))
			{
				var sourceKey = NormalizeSourceKey(scrapingLottery.SourceKey);
				if(!todayResult.TryGetProperty(sourceKey, out var draw) || draw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
					continue;

				var number = GetStringValue(draw, "numero");
				if(string.IsNullOrWhiteSpace(number) || number == "--")
					continue;

				var description = lotteries.First(x => x.Order == scrapingLottery.Order).Name;
				var reventado = GetStringValue(draw, "in_reventado");
				var isBusted = Constants.BustedList.Contains(reventado) || reventado == "1";
				var awardLine = CreateAwardLine(scrapingLottery.Order, description, number, isBusted, papers);

				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines;
		}

		private static JsonElement GetTodayResult(string jsonContent)
		{
			using var doc = JsonDocument.Parse(jsonContent);

			if(doc.RootElement.ValueKind != JsonValueKind.Array)
				return default;

			foreach(var result in doc.RootElement.EnumerateArray())
			{
				if(!result.TryGetProperty("dia", out var dayElement))
					continue;

				var dayText = dayElement.GetString();
				if(DateTime.TryParse(dayText, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) && day.Date == DateTime.Today)
					return result.Clone();
			}

			return default;
		}

		private static bool HasJpsSourceKey(ScrapingLottery scrapingLottery)
		{
			var sourceKey = NormalizeSourceKey(scrapingLottery.SourceKey);
			return sourceKey is "manana" or "tarde" or "noche";
		}

		private static string NormalizeSourceKey(string sourceKey)
		{
			return sourceKey
				.Trim()
				.ToLowerInvariant()
				.Replace("ñ", "n");
		}

		private static string GetStringValue(JsonElement element, string propertyName)
		{
			if(!element.TryGetProperty(propertyName, out var property))
				return string.Empty;

			return property.ValueKind switch
			{
				JsonValueKind.Number => property.GetRawText(),
				JsonValueKind.String => property.GetString() ?? string.Empty,
				JsonValueKind.True => "1",
				JsonValueKind.False => "0",
				_ => string.Empty
			};
		}
	}
}
