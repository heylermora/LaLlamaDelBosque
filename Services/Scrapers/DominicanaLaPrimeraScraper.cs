using HtmlAgilityPack;
using LaLlamaDelBosque.Models;
using LaLlamaDelBosque.Interfaces;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LaLlamaDelBosque.Services.Scrapers
{
	public class DominicanaLaPrimeraScraper: MultiSourceScraper
	{
		public override string LotteryType => "LA PRIMERA";
		private const string ApiUserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 18_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.5 Mobile/15E148 Safari/604.1";
		private static readonly Regex TwoDigits = new(@"^\d{2}$", RegexOptions.Compiled);
		public DominicanaLaPrimeraScraper(HttpClient httpClient, TimeProvider timeProvider, IJsonRepository repository)
			: base(httpClient, ScrapingSourceCatalog.GetEnabled(repository, "LA PRIMERA"), timeProvider)
		{
		}

		protected override IEnumerable<int> GetExpectedOrders(List<ScrapingDrawConfiguration> scrapingLotteries)
		{
			return scrapingLotteries
				.Where(IsDrawAvailable)
				.Select(x => x.Order)
				.Distinct();
		}

		protected override string GetAllSourcesFailedMessage()
		{
			return "No fue posible obtener los resultados de La Primera desde ninguna de las fuentes configuradas.";
		}

		protected override List<AwardLine> ProcessHtml(
			string htmlContent,
			List<ScrapingDrawConfiguration> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers,
			ScrapingSource source)
		{
			if(source.Key.Equals("official-api", StringComparison.OrdinalIgnoreCase))
				return ProcessOfficialApi(htmlContent, scrapingLotteries, lotteries, papers);

			var drawToLottery = scrapingLotteries
				.Where(IsDrawAvailable)
				.GroupBy(GetConfiguredDrawKey)
				.ToDictionary(x => x.Key, x => x.First());
			var orderToName = lotteries
				.GroupBy(x => x.Order)
				.ToDictionary(x => x.Key, x => x.First().Name);
			var document = new HtmlDocument();
			document.LoadHtml(htmlContent);
			var textLines = ExtractTextLines(document);
			var awardLines = new List<AwardLine>();

			for(var index = 0; index < textLines.Count; index++)
			{
				var drawKey = GetDrawKey(textLines[index]);
				if(string.IsNullOrWhiteSpace(drawKey) && Normalize(textLines[index]).Contains("primera", StringComparison.Ordinal))
					drawKey = GetDrawKey(string.Join(" ", textLines.Skip(index).Take(3)));
				if(string.IsNullOrWhiteSpace(drawKey) || !drawToLottery.TryGetValue(drawKey, out var configuredLottery))
					continue;

				var number = FindNextNumber(textLines, index + 1);
				if(string.IsNullOrWhiteSpace(number))
					continue;

				var description = orderToName.TryGetValue(configuredLottery.Order, out var lotteryName)
					? lotteryName
					: string.Empty;
				var awardLine = CreateAwardLine(configuredLottery.Order, description, number, false, papers);
				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines
				.GroupBy(x => x.Order)
				.Select(x => x.First())
				.ToList();
		}

		private List<AwardLine> ProcessOfficialApi(
			string jsonContent,
			List<ScrapingDrawConfiguration> scrapingLotteries,
			List<Lottery> lotteries,
			List<Paper> papers)
		{
			using var document = JsonDocument.Parse(jsonContent);
			if(!TryFindProperty(document.RootElement, "la_primera", out var laPrimeraResults))
				return new List<AwardLine>();

			var configuredDraws = scrapingLotteries
				.Where(IsDrawAvailable)
				.GroupBy(GetConfiguredDrawKey)
				.ToDictionary(x => x.Key, x => x.First());
			var orderToName = lotteries
				.GroupBy(x => x.Order)
				.ToDictionary(x => x.Key, x => x.First().Name);
			var resultObjects = new List<JsonElement>();
			CollectJsonObjects(laPrimeraResults, resultObjects);
			var awardLines = new List<AwardLine>();

			foreach(var resultObject in resultObjects)
			{
				if(!TryGetString(resultObject, "juego_nombre", out var gameName)
					|| !TryGetString(resultObject, "loteria_nombre", out var lotteryName)
					|| !TryGetString(resultObject, "hora_sorteo", out var drawHour)
					|| !TryGetProperty(resultObject, "resultado", out var resultValue))
					continue;

				var configuredLottery = configuredDraws.Values.FirstOrDefault(x =>
					x.ScrapingNames.Any(name => Normalize(name) == Normalize(gameName))
					&& x.ScrapingLotteryNames.Any(name => Normalize(name) == Normalize(lotteryName))
					&& x.ScrapingHours.Any(hour => NormalizeApiHour(hour) == NormalizeApiHour(drawHour)));
				if(configuredLottery == null)
					continue;

				var number = GetFirstResultValue(resultValue);
				if(string.IsNullOrWhiteSpace(number))
					continue;

				var description = orderToName.TryGetValue(configuredLottery.Order, out var configuredName)
					? configuredName
					: string.Empty;
				var awardLine = CreateAwardLine(configuredLottery.Order, description, number, false, papers);
				if(awardLine != null)
					awardLines.Add(awardLine);
			}

			return awardLines
				.GroupBy(x => x.Order)
				.Select(x => x.First())
				.ToList();
		}

		protected override async Task<string> DownloadSource(ScrapingSource source)
		{
			if(!source.Key.Equals("official-api", StringComparison.OrdinalIgnoreCase))
				return await base.DownloadSource(source);

			var nonce = await DownloadNonce(source.Referrer, source.Timeout);
			using var content = new MultipartFormDataContent
			{
				{ new StringContent("get_lotteries_results"), "action" },
				{ new StringContent(nonce), "nonce" },
				{ new StringContent(_timeProvider.GetLocalNow().Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "date" }
			};
			using var request = new HttpRequestMessage(HttpMethod.Post, source.Url) { Content = content };
			request.Headers.Referrer = new Uri(source.Referrer);
			request.Headers.UserAgent.ParseAdd(ApiUserAgent);
			using var timeout = new CancellationTokenSource(source.Timeout);
			using var response = await _httpClient.SendAsync(request, timeout.Token);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsStringAsync(timeout.Token);
		}

		private async Task<string> DownloadNonce(string officialUrl, TimeSpan requestTimeout)
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, officialUrl);
			request.Headers.Referrer = new Uri(officialUrl);
			request.Headers.UserAgent.ParseAdd(ApiUserAgent);
			using var timeout = new CancellationTokenSource(requestTimeout);
			using var response = await _httpClient.SendAsync(request, timeout.Token);
			response.EnsureSuccessStatusCode();
			var homepage = await response.Content.ReadAsStringAsync(timeout.Token);
			var nonceMatch = Regex.Match(homepage, @"(?:data-)?nonce[""']?\s*(?:=|:)\s*[""'](?<nonce>[a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
			if(!nonceMatch.Success)
				throw new InvalidOperationException("La página oficial de La Primera no publicó el nonce requerido por su API.");

			return nonceMatch.Groups["nonce"].Value;
		}

		private static string NormalizeApiHour(string drawHour)
		{
			return Regex.Replace(drawHour, @"[\s.]", string.Empty)
				.TrimStart('0')
				.ToLowerInvariant();
		}

		private static string GetFirstResultValue(JsonElement result)
		{
			if(result.ValueKind == JsonValueKind.Array)
			{
				var first = result.EnumerateArray().FirstOrDefault();
				return GetFirstResultValue(first);
			}
			if(result.ValueKind == JsonValueKind.Object)
			{
				var properties = result.EnumerateObject().ToList();
				var indexedValue = properties
					.Where(x => int.TryParse(x.Name, out _))
					.OrderBy(x => int.Parse(x.Name, CultureInfo.InvariantCulture))
					.Select(x => x.Value)
					.FirstOrDefault();
				if(indexedValue.ValueKind != JsonValueKind.Undefined)
					return GetFirstResultValue(indexedValue);

				foreach(var propertyName in new[] { "numero", "number", "value" })
					if(TryGetProperty(result, propertyName, out var namedValue))
						return GetFirstResultValue(namedValue);

				return string.Empty;
			}

			return NormalizeResultNumber(result);
		}

		private static string NormalizeResultNumber(JsonElement result)
		{
			var rawValue = result.ValueKind == JsonValueKind.String ? result.GetString() : result.ToString();
			var firstValue = Regex.Match(rawValue ?? string.Empty, @"\d{1,2}");
			return firstValue.Success ? firstValue.Value.PadLeft(2, '0') : string.Empty;
		}

		private static bool TryFindProperty(JsonElement element, string propertyName, out JsonElement value)
		{
			if(TryGetProperty(element, propertyName, out value))
				return true;

			if(element.ValueKind == JsonValueKind.Object)
			{
				foreach(var property in element.EnumerateObject())
					if(TryFindProperty(property.Value, propertyName, out value))
						return true;
			}
			else if(element.ValueKind == JsonValueKind.Array)
			{
				foreach(var child in element.EnumerateArray())
					if(TryFindProperty(child, propertyName, out value))
						return true;
			}

			value = default;
			return false;
		}

		private static bool TryGetString(JsonElement element, string propertyName, out string value)
		{
			if(TryGetProperty(element, propertyName, out var property)
				&& property.ValueKind is JsonValueKind.String or JsonValueKind.Number)
			{
				value = property.ToString();
				return true;
			}

			value = string.Empty;
			return false;
		}

		private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
		{
			if(element.ValueKind == JsonValueKind.Object)
			{
				foreach(var property in element.EnumerateObject())
				{
					if(property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
					{
						value = property.Value;
						return true;
					}
				}
			}

			value = default;
			return false;
		}

		private static void CollectJsonObjects(JsonElement element, List<JsonElement> objects)
		{
			if(element.ValueKind == JsonValueKind.Object)
			{
				objects.Add(element);
				foreach(var property in element.EnumerateObject())
					CollectJsonObjects(property.Value, objects);
			}
			else if(element.ValueKind == JsonValueKind.Array)
			{
				foreach(var child in element.EnumerateArray())
					CollectJsonObjects(child, objects);
			}
		}

		private static List<string> ExtractTextLines(HtmlDocument document)
		{
			return document.DocumentNode
				.Descendants()
				.Where(x => !x.HasChildNodes && !x.Ancestors("script").Any() && !x.Ancestors("style").Any())
				.SelectMany(x => HtmlEntity.DeEntitize(x.InnerText ?? string.Empty)
					.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries))
				.Select(Clean)
				.Where(x => !string.IsNullOrWhiteSpace(x))
				.ToList();
		}

		private string FindNextNumber(List<string> textLines, int startIndex)
		{
			var number = string.Empty;
			DateTime? resultDate = null;

			for(var index = startIndex; index < textLines.Count && index < startIndex + 30; index++)
			{
				if(!string.IsNullOrWhiteSpace(GetDrawKey(textLines[index])))
					break;

				if(TryParseResultDate(textLines[index], out var parsedDate))
				{
					resultDate = parsedDate.Date;
					continue;
				}

				if(!string.IsNullOrWhiteSpace(number))
					continue;

				if(TwoDigits.IsMatch(textLines[index]))
				{
					number = textLines[index];
					continue;
				}
				if(Regex.IsMatch(textLines[index], @"^\d{1,2}:\d{2}\s*[ap]\.?\s*m\.?$", RegexOptions.IgnoreCase))
					continue;

				var twoDigitNumber = Regex.Match(textLines[index], @"\b\d{2}\b");
				if(twoDigitNumber.Success)
				{
					number = twoDigitNumber.Value;
					continue;
				}

				var separatedDigits = Regex.Match(textLines[index], @"(?:^|\D)(\d)\s+(\d)(?:\s+\d)?(?:\D|$)");
				if(separatedDigits.Success)
					number = $"{separatedDigits.Groups[1].Value}{separatedDigits.Groups[2].Value}";
			}

			return resultDate.HasValue && resultDate.Value != _timeProvider.GetLocalNow().Date
				? string.Empty
				: number;
		}

		private bool IsDrawAvailable(ScrapingDrawConfiguration lottery)
		{
			return DateTime.TryParseExact(
				lottery.Hour,
				new[] { "h:mm tt", "hh:mm tt" },
				CultureInfo.InvariantCulture,
				DateTimeStyles.AllowWhiteSpaces,
				out var drawTime)
				&& drawTime.TimeOfDay <= _timeProvider.GetLocalNow().TimeOfDay;
		}

		private static string GetConfiguredDrawKey(ScrapingDrawConfiguration lottery)
		{
			if(!string.IsNullOrWhiteSpace(lottery.SourceKey))
				return Normalize(lottery.SourceKey);

			return Normalize(lottery.Name).Contains("noche", StringComparison.Ordinal) ? "noche" : "dia";
		}

		private static string GetDrawKey(string value)
		{
			var normalized = Normalize(value);
			if(!normalized.Contains("primera", StringComparison.Ordinal))
				return string.Empty;

			if(normalized.Contains("noche", StringComparison.Ordinal))
				return "noche";
			if(normalized.Contains("dia", StringComparison.Ordinal)
				|| normalized.Contains("mediodia", StringComparison.Ordinal)
				|| normalized.Contains("matutina", StringComparison.Ordinal))
				return "dia";

			return string.Empty;
		}

		private static bool TryParseResultDate(string value, out DateTime resultDate)
		{
			resultDate = default;
			var dateMatch = Regex.Match(value, @"\b(?:\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4}|\d{1,2}\s+de\s+[a-záéíóúñ]+\s+(?:de\s+)?\d{4})\b", RegexOptions.IgnoreCase);
			return dateMatch.Success
				&& DateTime.TryParse(dateMatch.Value, CultureInfo.GetCultureInfo("es-DO"), DateTimeStyles.AllowWhiteSpaces, out resultDate);
		}

		private static string Normalize(string value)
		{
			var decomposed = Clean(value).ToLowerInvariant().Normalize(NormalizationForm.FormD);
			return new string(decomposed.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark).ToArray())
				.Normalize(NormalizationForm.FormC);
		}

		private static string Clean(string? value)
		{
			return Regex.Replace(HtmlEntity.DeEntitize(value ?? string.Empty).Replace('\u00A0', ' '), @"\s+", " ").Trim();
		}
	}
}
