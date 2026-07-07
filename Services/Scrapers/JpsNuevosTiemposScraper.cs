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
